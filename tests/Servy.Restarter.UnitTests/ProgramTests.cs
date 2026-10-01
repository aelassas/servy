using Moq;
using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Infrastructure.Data;
using Servy.Restarter.Bootstrap;
using Servy.Testing;
using System.Data.SQLite;

namespace Servy.Restarter.UnitTests
{
    [Collection(ProgramTestsCollection.Name)]
    public class ProgramTests : TempDirectoryTestBase
    {
        // CONSTANT STRINGS HOISTING: Centralize artifact filenames to prevent cleanup drift
        private const string ConfigFileName = "appsettings.restarter.json";
        private const string KeyFileName = "test_restarter_local.key";
        private const string IvFileName = "test_restarter_local.iv";
        private const string LogFileName = "Servy.Restarter.log";

        // Use a named in-memory database string with shared cache. This forces SQLite
        // to share the exact same memory space across different connection instances instantiated
        // inside Program.Main as long as our _dbKeepAliveConnection handle remains open.
        private const string SharedInMemoryConnectionString = "Data Source=RestarterTestDb;Mode=Memory;Cache=Shared;Version=3;";

        private readonly string _tempConfigPath;
        private readonly string _configBackupPath;
        private readonly bool _hasConfigBackup;
        private readonly string _expectedLogFilePath;
        private readonly SQLiteConnection _dbKeepAliveConnection;

        public ProgramTests()
        {
            // Reset exit code before each execution run
            Environment.ExitCode = 0;

            // Generate isolated test-run directories for configuration and log storage
            _tempConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            _expectedLogFilePath = Path.Combine(TempDirectory, LogFileName);

            // Pre-seed the static logger so empty/missing argument calls route to the isolated temp directory
            Logger.Initialize(LogFileName, logDirectory: TempDirectory);

            // Program.Main reads its configuration strictly from the app directory, so the tests must
            // clobber the build-deployed appsettings.restarter.json. Back it up so Dispose can put the
            // build's own artifact back instead of leaving the output directory without it.
            _configBackupPath = _tempConfigPath + ".bak";
            _hasConfigBackup = File.Exists(_tempConfigPath);
            if (_hasConfigBackup)
            {
                File.Copy(_tempConfigPath, _configBackupPath, overwrite: true);
            }

            File.WriteAllText(_tempConfigPath, BuildConfigJson("30"));

            // The core settings are read-only in production (always the vault under ProgramData),
            // so the shared in-memory database and local key files are injected through the
            // test-only override rather than appsettings.restarter.json.
            CoreSettingsLoader.TestOverride = new CoreSettingsLoader.CoreSettings(SharedInMemoryConnectionString, KeyFileName, IvFileName);

            // Open the persistent handle to anchor the shared memory segment lifecycle
            _dbKeepAliveConnection = new SQLiteConnection(SharedInMemoryConnectionString);
            _dbKeepAliveConnection.Open();

            // Bootstrap the schema table directly into the shared memory segment
            SQLiteDbInitializer.Initialize(_dbKeepAliveConnection);
        }

        private static string BuildConfigJson(string restartTimeoutSeconds) =>
            "{\r\n" +
            "  \"RestartTimeoutSeconds\": \"" + restartTimeoutSeconds + "\"\r\n" +
            "}";

        #region Guard Conditions Branch Coverage

        [Fact]
        public void Main_MissingArguments_SetsExitCodeTo1AndExitsEarly()
        {
            // Arrange
            string[] args = new string[0]; // Triggers if (args.Length == 0)

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Missing required argument: service name.");

            // The ExitsEarly half: without the guard's return, args[0] throws on the empty array
            // and the catch-all logs the pre-scoped-logger failure instead.
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to initialize or execute.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        public void Main_EmptyOrWhitespaceServiceName_SetsExitCodeTo1AndExitsEarly(string invalidName)
        {
            // Arrange
            string[] args = new string[] { invalidName, TempDirectory }; // Triggers if (string.IsNullOrWhiteSpace(serviceName))

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Service name cannot be empty.");

            // The ExitsEarly half: without the guard's return, the blank name flows on to the
            // repository lookup and the not-managed branch logs and sets ExitCode = 1 as well.
            AssertLogDoesNotContainMessage("is not managed by Servy.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
        }

        #endregion

        #region Event Log Fallback & Security Guard Coverage

        /*
         * The event source, the event-log logger and the SQLite version check are reached through
         * IRestarterBootstrapEnvironment, so the three branches below no longer need a host without
         * Windows Event Log registry access or a substituted SQLite provider assembly. The rules the
         * version check itself applies stay covered by DatabaseValidatorTests.cs; what these tests pin
         * is what Program.Run does with its answer.
         */

        [Fact]
        public void Run_EventSourceRegistrationFails_FallsBackToFileOnlyLoggingAndContinues()
        {
            // Arrange
            // The event source cannot be registered, so Step 1's catch must take over: it warns and asks
            // for a logger with the Windows Event Log turned OFF, then start-up carries on.
            string serviceName = "GhostServiceWithUnavailableEventSource";
            var environment = new FakeRestarterBootstrapEnvironment
            {
                EventSourceFailure = new InvalidOperationException("event log registry is not reachable")
            };

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            AssertLogContainsMessage("Event Log source unavailable; continuing with file logging only.");

            // The fallback's argument is the branch. EnsureEventSourceExists threw before the primary
            // assignment ran, so the single request recorded is the catch's own, and it must ask for
            // file-only logging: deleting that assignment, or letting it ask for the event log instead,
            // changes this sequence.
            Assert.Equal(new[] { false }, environment.EventLogLoggerRequests);

            // Best-effort means the restart is not abandoned: the pipeline runs on to the validation step.
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public void Run_VulnerableSqliteVersion_LogsFatalAndExitsBeforeTouchingTheDatabase()
        {
            // Arrange
            // A detected version below AppConfig.MinRequiredSqliteVersion is the CVE-2025-6965 refusal.
            const string VulnerableVersion = "3.49.0";
            string serviceName = "ManagedServiceNeverReachedOnVulnerableSqlite";
            var environment = new FakeRestarterBootstrapEnvironment
            {
                SqliteVersionIsSafe = false,
                DetectedSqliteVersion = VulnerableVersion
            };

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"[FATAL] Vulnerable SQLite version detected: {VulnerableVersion}. " +
                                     $"Minimum required: {AppConfig.MinRequiredSqliteVersion} (CVE-2025-6965 mitigation).");

            // The refusal returns: neither the repository validation nor the restart attempt may run, so a
            // vulnerable engine is never asked to open the database.
            AssertLogDoesNotContainMessage($"Service '{serviceName}' is not managed by Servy.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
        }

        [Fact]
        public void Run_EventLogFallbackAlsoFails_ReportsThroughTheStaticLoggerArm()
        {
            // Arrange
            // When the file-only fallback cannot be built either, no root logger and no scoped logger ever
            // exist, so the catch-all has nothing but the static Logger: its else arm.
            var environment = new FakeRestarterBootstrapEnvironment
            {
                EventSourceFailure = new InvalidOperationException("event log registry is not reachable"),
                EventLogLoggerFailure = new InvalidOperationException("the event log logger cannot be built")
            };

            // Act
            Program.Run(new string[] { "AnyServiceName", TempDirectory }, restarter: null, environment: environment);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Servy.Restarter.exe failed to initialize or execute.");

            // The arm is the branch: with a logger available the catch-all reports the other message, so
            // this pins the else rather than the if.
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to restart the service.");
        }

        #endregion

        #region Operational Pipeline & Validation Exceptions

        [Fact]
        public void Main_SettingsFileStillRelocatesTheDatabase_WarnsThatTheSettingIsIgnored()
        {
            // Arrange
            // A pre-10.2 appsettings.restarter.json that still points the database elsewhere. The
            // setting has no effect (the test-only override supplies the database), and the
            // restarter must say so once the scoped logger exists.
            File.WriteAllText(_tempConfigPath,
                "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"Data Source=D:\\\\old\\\\Servy.db\"\r\n" +
                "  },\r\n" +
                "  \"RestartTimeoutSeconds\": \"30\"\r\n" +
                "}");
            string serviceName = "GhostUnmanagedServiceWithOldSettings";

            // Act
            Program.Main(new string[] { serviceName, TempDirectory });

            // Assert
            AssertLogContainsMessage("appsettings.restarter.json sets ConnectionStrings:DefaultConnection, which Servy ignores since v10.2");
        }

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We provide a dummy service name that doesn't exist in our initialized memory database.
            // This triggers the serviceRepository.GetByName(...) == null failure branch cleanly.
            string serviceName = "GhostUnmanagedService";
            string[] args = new string[] { serviceName, TempDirectory };

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public void Main_FallbackConfigurationParsing_HandlesInvalidTimeoutGracefully()
        {
            // Arrange
            string serviceName = "ManagedTestServiceForTimeoutValidation";

            // 1. Manually seed the shared test database using the available System.Data.SQLite engine
            // to ensure the service passes the unmanaged check cleanly.
            using (var connection = new System.Data.SQLite.SQLiteConnection(SharedInMemoryConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT OR IGNORE INTO Services (Name, ExecutablePath) VALUES (@name, @path);";
                    command.Parameters.AddWithValue("@name", serviceName);
                    command.Parameters.AddWithValue("@path", "C:\\MockPath\\Service.exe");
                    command.ExecuteNonQuery();
                }
            }

            try
            {
                // 2. Build a structurally complete configuration payload where only the timeout option is corrupted.
                File.WriteAllText(_tempConfigPath, BuildConfigJson("NotAnInteger"));

                string[] args = new string[] { serviceName, TempDirectory };

                // Act
                Program.Main(args);

                // Assert
                // The application successfully bypassed the corrupted token string and fell back
                // to standard timeout bounds. Because the service does not actually exist in the SCM,
                // it detects ServiceNotFound, logs a warning, and sets ExitCode = 1.
                Assert.Equal(1, Environment.ExitCode);
                AssertLogContainsMessage($"Service '{serviceName}' no longer exists in the SCM; nothing to restart.");
            }
            finally
            {
                // Clean up the seeded service entry from the shared database context to prevent
                // side-effects or collision state leaks on subsequent unit test runs.
                using (var connection = new SQLiteConnection(SharedInMemoryConnectionString))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM Services WHERE Name = @name;";
                        command.Parameters.AddWithValue("@name", serviceName);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        [Fact]
        public void Main_ServiceRestarted_SetsExitCodeTo0AndLogsSuccess()
        {
            // Arrange
            string serviceName = "ManagedServiceForSuccessfulRestart";

            using (var connection = new SQLiteConnection(SharedInMemoryConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT OR IGNORE INTO Services (Name, ExecutablePath) VALUES (@name, @path);";
                    command.Parameters.AddWithValue("@name", serviceName);
                    command.Parameters.AddWithValue("@path", "C:\\MockPath\\Service.exe");
                    command.ExecuteNonQuery();
                }
            }

            var mockRestarter = new Mock<IServiceRestarter>();
            mockRestarter
                .Setup(r => r.RestartService(serviceName, It.IsAny<TimeSpan>()))
                .Returns(RestartResult.Restarted);

            try
            {
                string[] args = new string[] { serviceName, TempDirectory };

                // Act
                Program.Run(args, mockRestarter.Object);

                // Assert
                Assert.Equal(0, Environment.ExitCode);
                AssertLogContainsMessage($"Successfully restarted service '{serviceName}'.");
            }
            finally
            {
                using (var connection = new SQLiteConnection(SharedInMemoryConnectionString))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM Services WHERE Name = @name;";
                        command.Parameters.AddWithValue("@name", serviceName);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        [Fact]
        public void Main_RestartTimeoutExceedsHostWaitLimit_LogsWarning()
        {
            // Arrange
            // 300s is above the 240s host service execution wait limit
            // (AppConfig.RestarterExeMaxWaitMs / AppConfig.MillisecondsPerSecond) and well inside
            // AppConfig.MaxRestarterTimeoutSeconds, so ConfigParser.GetConfigInt passes it through
            // unclamped and the over-budget warning branch is taken.
            string serviceName = "ManagedServiceForTimeoutWarning";

            using (var connection = new SQLiteConnection(SharedInMemoryConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT OR IGNORE INTO Services (Name, ExecutablePath) VALUES (@name, @path);";
                    command.Parameters.AddWithValue("@name", serviceName);
                    command.Parameters.AddWithValue("@path", "C:\\MockPath\\Service.exe");
                    command.ExecuteNonQuery();
                }
            }

            var mockRestarter = new Mock<IServiceRestarter>();
            mockRestarter
                .Setup(r => r.RestartService(serviceName, It.IsAny<TimeSpan>()))
                .Returns(RestartResult.Restarted);

            try
            {
                File.WriteAllText(_tempConfigPath, BuildConfigJson("300"));
                string[] args = new string[] { serviceName, TempDirectory };

                // Act
                Program.Run(args, mockRestarter.Object);

                // Assert
                Assert.Equal(0, Environment.ExitCode);
                AssertLogContainsMessage("Configured RestartTimeoutSeconds (300s) exceeds the host service execution wait limit (240s).");
            }
            finally
            {
                using (var connection = new SQLiteConnection(SharedInMemoryConnectionString))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM Services WHERE Name = @name;";
                        command.Parameters.AddWithValue("@name", serviceName);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        #endregion

        #region Fatal Exception Resilience Blocks

        [Fact]
        public void Main_BrokenConnectionString_HitsCatchAllViaScopedLogger()
        {
            // Arrange
            // Override the core settings with an unparseable connection string.
            // This safely simulates database driver crashes while remaining completely isolated.
            CoreSettingsLoader.TestOverride = new CoreSettingsLoader.CoreSettings("Data Source=||InvalidPath||:?", KeyFileName, IvFileName);

            // Pass a target service name argument. The broken connection string makes
            // the SQLite open fail inside GetByName, after the scoped logger exists - exercising
            // the scoped-logger arm of the catch-all block.
            string[] args = new string[] { "Invalid\\Service/Path:Characters", TempDirectory };

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            // Confirms that the catch-all execution path was hit using the initialized scoped logger
            AssertLogContainsMessage("Servy.Restarter.exe failed to restart the service.");
        }

        #endregion

        #region Verification Helpers

        /// <summary>
        /// Scans the physical log output stream for the expected diagnostic signatures to discriminate between crash paths.
        /// </summary>
        private void AssertLogContainsMessage(string expectedMessage)
        {
            // Force the static logger to flush its handle completely to disk
            Logger.Shutdown();

            Assert.True(File.Exists(_expectedLogFilePath), $"The diagnostic restarter log file was never initialized on disk at '{_expectedLogFilePath}'.");

            string logContent = File.ReadAllText(_expectedLogFilePath);
            Assert.Contains(expectedMessage, logContent);
        }

        /// <summary>
        /// Asserts the physical log output stream carries no signature from a later pipeline stage,
        /// which is what pins that a guard returned instead of logging and falling through.
        /// </summary>
        private void AssertLogDoesNotContainMessage(string unexpectedMessage)
        {
            // Force the static logger to flush its handle completely to disk
            Logger.Shutdown();

            Assert.True(File.Exists(_expectedLogFilePath), $"The diagnostic restarter log file was never initialized on disk at '{_expectedLogFilePath}'.");

            string logContent = File.ReadAllText(_expectedLogFilePath);
            Assert.DoesNotContain(unexpectedMessage, logContent);
        }

        #endregion

        #region Test Doubles

        /// <summary>
        /// An <see cref="IRestarterBootstrapEnvironment"/> that records what the start-up sequence asked of
        /// it and answers from memory, so no step touches the Windows event log or the loaded SQLite engine.
        /// </summary>
        private sealed class FakeRestarterBootstrapEnvironment : IRestarterBootstrapEnvironment
        {
            /// <summary>Gets or sets the exception <see cref="EnsureEventSourceExists"/> raises, if any.</summary>
            public Exception? EventSourceFailure { get; set; }

            /// <summary>Gets or sets the exception <see cref="CreateEventLogLogger"/> raises, if any.</summary>
            public Exception? EventLogLoggerFailure { get; set; }

            /// <summary>Gets or sets the answer <see cref="IsSqliteVersionSafe"/> gives.</summary>
            public bool SqliteVersionIsSafe { get; set; } = true;

            /// <summary>Gets or sets the version <see cref="IsSqliteVersionSafe"/> reports.</summary>
            public string? DetectedSqliteVersion { get; set; } = AppConfig.MinRequiredSqliteVersion.ToString();

            /// <summary>
            /// Gets the <c>isEventLogEnabled</c> arguments <see cref="CreateEventLogLogger"/> was called
            /// with, in call order.
            /// </summary>
            public List<bool> EventLogLoggerRequests { get; } = new List<bool>();

            /// <summary>Raises <see cref="EventSourceFailure"/> when one is configured.</summary>
            /// <exception cref="Exception">The configured <see cref="EventSourceFailure"/>.</exception>
            public void EnsureEventSourceExists()
            {
                if (EventSourceFailure != null)
                {
                    throw EventSourceFailure;
                }
            }

            /// <summary>
            /// Records the request and returns a logger with the Windows Event Log switched off, which keeps
            /// the test off the machine's event log whichever argument the start-up path passed.
            /// </summary>
            /// <param name="isEventLogEnabled">The argument the start-up path asked for; recorded, not honoured.</param>
            /// <returns>A file-only logger.</returns>
            /// <exception cref="Exception">The configured <see cref="EventLogLoggerFailure"/>.</exception>
            public IServyLogger CreateEventLogLogger(bool isEventLogEnabled)
            {
                EventLogLoggerRequests.Add(isEventLogEnabled);

                if (EventLogLoggerFailure != null)
                {
                    throw EventLogLoggerFailure;
                }

                return new EventLogLogger(AppConfig.EventSource, isEventLogEnabled: false);
            }

            /// <summary>Answers from <see cref="SqliteVersionIsSafe"/>.</summary>
            /// <param name="detectedVersion">Receives <see cref="DetectedSqliteVersion"/>.</param>
            /// <returns><see cref="SqliteVersionIsSafe"/>.</returns>
            public bool IsSqliteVersionSafe(out string? detectedVersion)
            {
                detectedVersion = DetectedSqliteVersion;
                return SqliteVersionIsSafe;
            }
        }

        #endregion

        public override void Dispose()
        {
            // Force logger teardown first to unlock active files
            Logger.Shutdown();

            // Explicitly unlock and drop the keep-alive memory connection reference
            _dbKeepAliveConnection?.Dispose();

            CoreSettingsLoader.TestOverride = null;

            // Clean dynamic runtime artifacts cleanly
            try
            {
                if (_hasConfigBackup && File.Exists(_configBackupPath))
                {
                    // Restore the build-deployed artifact rather than deleting it
                    File.Copy(_configBackupPath, _tempConfigPath, overwrite: true);
                    File.Delete(_configBackupPath);
                }
                else if (File.Exists(_tempConfigPath))
                {
                    File.Delete(_tempConfigPath);
                }

                if (File.Exists(KeyFileName)) File.Delete(KeyFileName);
                if (File.Exists(IvFileName)) File.Delete(IvFileName);
            }
            catch
            {
                // Suppress disposal file-locks
            }

            base.Dispose();
        }
    }
}
