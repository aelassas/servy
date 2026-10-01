using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Restarter.Bootstrap;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    [Collection(ProgramTestsCollection.Name)]
    public class ProgramTests : TempDirectoryTestBase
    {
        // CONSTANT STRINGS HOISTING: Centralize artifact filenames to prevent cleanup drift
        private const string LogFileName = "Servy.Restarter.log";

        private readonly string _expectedLogFilePath;
        private readonly SQLiteConnection _dbKeepAliveConnection;
        private readonly string _restartTimeoutSeconds;

        public ProgramTests()
        {
            // Reset global exit code before each test execution block
            Environment.ExitCode = 0;

            // Isolate logging writes directly into a dynamic, unique temporary folder per test run
            _expectedLogFilePath = Path.Combine(TempDirectory, LogFileName);

            // Pre-seed the static logger so empty/missing argument calls route to the isolated temp directory
            Logger.Initialize(LogFileName, logDirectory: TempDirectory);

            // Capture the baseline restart timeout to allow perfect recovery state rollback during Dispose
            _restartTimeoutSeconds = ConfigurationManager.AppSettings["RestartTimeoutSeconds"];

            // Supply an absolute file-backed SQLite database path and key paths in TempDirectory to satisfy AppFoldersHelper
            string fileDbPath = Path.Combine(TempDirectory, "RestarterTestDbNet48.db");
            string fileConnString = $"Data Source={fileDbPath};Version=3;";

            // The core settings are read-only in production (always the vault under ProgramData), so
            // the database and key are injected through the test-only override instead of AppSettings.
            CoreSettingsLoader.TestOverride = new CoreSettings(
                fileConnString,
                Path.Combine(TempDirectory, "test_restarter.key"),
                Path.Combine(TempDirectory, "test_restarter.iv"));

            // Open the persistent handle and initialize database schema
            _dbKeepAliveConnection = new SQLiteConnection(fileConnString);
            _dbKeepAliveConnection.Open();
            SQLiteDbInitializer.Initialize(_dbKeepAliveConnection);
        }

        #region Guard Conditions Branch Coverage

        [Fact]
        public void Main_MissingArguments_SetsExitCodeTo1AndExitsEarly()
        {
            // Arrange
            string[] args = new string[0]; // Triggers: if (args.Length == 0)

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
            string[] args = new string[] { invalidName, TempDirectory }; // Triggers: if (string.IsNullOrWhiteSpace(serviceName))

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
        public void Run_EventSourceRegistered_AsksForAnEventLogEnabledRootLogger()
        {
            // Arrange
            // The event source registers, so Step 1's try must ask for a logger with the Windows Event
            // Log ON. #7262 added the fake and read its request list from the failure test only, which
            // left the primary arm's argument unpinned.
            string serviceName = "GhostServiceWithRegisteredEventSource";
            var environment = new FakeRestarterBootstrapEnvironment();

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            // One request, from the try, asking for the event log: flipping the primary argument, or
            // taking the fallback without a failure, changes this sequence. In production that argument
            // is what puts every restarter Warn and Error on the Windows Event Log.
            Assert.Equal(new[] { true }, environment.EventLogLoggerRequests);
            AssertLogDoesNotContainMessage("Event Log source unavailable; continuing with file logging only.");

            // Best-effort start-up continues to the validation step, as in the fallback test above.
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public void Run_EventSourceRegistersButEventLogLoggerFails_FallsBackToFileOnlyLogging()
        {
            // Arrange
            // The second way into Step 1's catch: the source registers, so EnsureEventSourceExists
            // returns, and the primary CreateEventLogLogger(true) is what throws while the fallback
            // CreateEventLogLogger(false) still builds (FailOnlyEventLogEnabledLogger).
            string serviceName = "GhostServiceWithUnbuildableEventLogLogger";
            var environment = new FakeRestarterBootstrapEnvironment
            {
                EventLogLoggerFailure = new InvalidOperationException("the event log logger cannot be built"),
                FailOnlyEventLogEnabledLogger = true
            };

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            AssertLogContainsMessage("Event Log source unavailable; continuing with file logging only.");

            // Both requests in call order are the branch: the try asked for the event log and failed,
            // then the catch asked for file-only logging and succeeded.
            Assert.Equal(new[] { true, false }, environment.EventLogLoggerRequests);

            // The fallback logger was built, so start-up carries on rather than reaching the catch-all.
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to initialize or execute.");
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

        #region Operational Pipeline & Validation Branches

        [Fact]
        public void Main_SettingsFileStillRelocatesTheDatabase_WarnsThatTheSettingIsIgnored()
        {
            // Arrange
            // A pre-10.2 .exe.config that still points the database elsewhere. The setting has no
            // effect (the test-only override supplies the database), and the restarter must say so
            // once the scoped logger exists.
            ConfigurationManager.AppSettings["DefaultConnection"] = "Data Source=D:\\old\\Servy.db";
            try
            {
                // Act
                Program.Main(new string[] { "UnmanagedNet48ServiceWithOldSettings", TempDirectory });

                // Assert
                AssertLogContainsMessage("Servy.Restarter.Net48.exe.config sets DefaultConnection, which Servy ignores since v10.2");
            }
            finally
            {
                // AppSettings accepts a new value in memory but refuses Remove ("The configuration is read only"),
                // so blank the key instead: a blank value is what WarnAboutIgnoredSettings treats as unset.
                ConfigurationManager.AppSettings["DefaultConnection"] = string.Empty;
            }
        }

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We pass an unmanaged service identifier string. Since the database is fresh and empty,
            // serviceRepository.GetByName(...) will return null, exercising the managed validation check.
            string serviceName = "UnmanagedNet48Service";
            string[] args = new string[] { serviceName, TempDirectory };

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public async Task Main_FallbackConfigurationParsing_HandlesInvalidTimeoutGracefully()
        {
            // Arrange
            // Inject an unparseable non-integer token directly into the runtime configuration matrix
            ConfigurationManager.AppSettings["RestartTimeoutSeconds"] = "NotAnInteger";

            string connString = CoreSettingsLoader.TestOverride.ConnectionString;
            string keyPath = CoreSettingsLoader.TestOverride.AESKeyFilePath;
            string ivPath = CoreSettingsLoader.TestOverride.AESIVFilePath;

            string serviceName = "UnmanagedNet48Service";
            string[] args = new string[] { serviceName, TempDirectory };

            // Ensure application key files and folder environment exist for Program.Main
            AppFoldersHelper.EnsureFolders(connString, keyPath, ivPath);

            // Seed a valid managed service via ServiceRepository so GetByName() can deserialize it properly
            using (var dbContext = new AppDbContext(connString))
            using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
            using (var secureData = new SecureData(protectedKeyProvider))
            {
                var dapperExecutor = new DapperExecutor(dbContext);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();
                var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                var service = new ServiceDto
                {
                    Name = serviceName,
                    ExecutablePath = @"C:\MockPath\Service.exe"
                };
                await repository.AddAsync(service, CancellationToken.None);
            }

            try
            {
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
                using (var dbContext = new AppDbContext(connString))
                using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
                using (var secureData = new SecureData(protectedKeyProvider))
                {
                    var dapperExecutor = new DapperExecutor(dbContext);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();
                    var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                    var existing = repository.GetByName(serviceName, decrypt: false);
                    if (existing != null && existing.Id.HasValue)
                    {
                        await repository.DeleteAsync(existing.Id.Value, CancellationToken.None);
                    }
                }
            }
        }

        [Fact]
        public async Task Main_ServiceRestarted_SetsExitCodeTo0AndLogsSuccess()
        {
            // Arrange
            string connString = CoreSettingsLoader.TestOverride.ConnectionString;
            string keyPath = CoreSettingsLoader.TestOverride.AESKeyFilePath;
            string ivPath = CoreSettingsLoader.TestOverride.AESIVFilePath;

            string serviceName = "ManagedNet48ServiceForSuccessfulRestart";
            string[] args = new string[] { serviceName, TempDirectory };

            AppFoldersHelper.EnsureFolders(connString, keyPath, ivPath);

            using (var dbContext = new AppDbContext(connString))
            using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
            using (var secureData = new SecureData(protectedKeyProvider))
            {
                var dapperExecutor = new DapperExecutor(dbContext);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();
                var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                var service = new ServiceDto
                {
                    Name = serviceName,
                    ExecutablePath = @"C:\MockPath\Service.exe"
                };
                await repository.AddAsync(service, CancellationToken.None);
            }

            var mockRestarter = new Mock<IServiceRestarter>();
            mockRestarter
                .Setup(r => r.RestartService(serviceName, It.IsAny<TimeSpan>()))
                .Returns(RestartResult.Restarted);

            try
            {
                // Act
                Program.Run(args, mockRestarter.Object);

                // Assert
                Assert.Equal(0, Environment.ExitCode);
                AssertLogContainsMessage($"Successfully restarted service '{serviceName}'.");
            }
            finally
            {
                using (var dbContext = new AppDbContext(connString))
                using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
                using (var secureData = new SecureData(protectedKeyProvider))
                {
                    var dapperExecutor = new DapperExecutor(dbContext);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();
                    var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                    var existing = repository.GetByName(serviceName, decrypt: false);
                    if (existing != null && existing.Id.HasValue)
                    {
                        await repository.DeleteAsync(existing.Id.Value, CancellationToken.None);
                    }
                }
            }
        }

        [Fact]
        public async Task Main_RestartTimeoutExceedsHostWaitLimit_LogsWarning()
        {
            // Arrange
            // 300s is above the 240s host service execution wait limit
            // (AppConfig.RestarterExeMaxWaitMs / AppConfig.MillisecondsPerSecond) and well inside
            // AppConfig.MaxRestarterTimeoutSeconds, so ConfigParser.GetConfigInt passes it through
            // unclamped and the over-budget warning branch is taken.
            ConfigurationManager.AppSettings["RestartTimeoutSeconds"] = "300";

            string connString = CoreSettingsLoader.TestOverride.ConnectionString;
            string keyPath = CoreSettingsLoader.TestOverride.AESKeyFilePath;
            string ivPath = CoreSettingsLoader.TestOverride.AESIVFilePath;

            string serviceName = "ManagedNet48ServiceForTimeoutWarning";
            string[] args = new string[] { serviceName, TempDirectory };

            AppFoldersHelper.EnsureFolders(connString, keyPath, ivPath);

            using (var dbContext = new AppDbContext(connString))
            using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
            using (var secureData = new SecureData(protectedKeyProvider))
            {
                var dapperExecutor = new DapperExecutor(dbContext);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();
                var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                var service = new ServiceDto
                {
                    Name = serviceName,
                    ExecutablePath = @"C:\MockPath\Service.exe"
                };
                await repository.AddAsync(service, CancellationToken.None);
            }

            var mockRestarter = new Mock<IServiceRestarter>();
            mockRestarter
                .Setup(r => r.RestartService(serviceName, It.IsAny<TimeSpan>()))
                .Returns(RestartResult.Restarted);

            try
            {
                // Act
                Program.Run(args, mockRestarter.Object);

                // Assert
                Assert.Equal(0, Environment.ExitCode);
                AssertLogContainsMessage("Configured RestartTimeoutSeconds (300s) exceeds the host service execution wait limit (240s).");
            }
            finally
            {
                using (var dbContext = new AppDbContext(connString))
                using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
                using (var secureData = new SecureData(protectedKeyProvider))
                {
                    var dapperExecutor = new DapperExecutor(dbContext);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();
                    var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                    var existing = repository.GetByName(serviceName, decrypt: false);
                    if (existing != null && existing.Id.HasValue)
                    {
                        await repository.DeleteAsync(existing.Id.Value, CancellationToken.None);
                    }
                }
            }
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
            public Exception EventSourceFailure { get; set; }

            /// <summary>Gets or sets the exception <see cref="CreateEventLogLogger"/> raises, if any.</summary>
            public Exception EventLogLoggerFailure { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether <see cref="EventLogLoggerFailure"/> is raised only
            /// for the event-log-enabled request, which is what lets a test reach Step 1's catch through a
            /// failing primary logger while the fallback logger still builds.
            /// </summary>
            public bool FailOnlyEventLogEnabledLogger { get; set; }

            /// <summary>Gets or sets the answer <see cref="IsSqliteVersionSafe"/> gives.</summary>
            public bool SqliteVersionIsSafe { get; set; } = true;

            /// <summary>Gets or sets the version <see cref="IsSqliteVersionSafe"/> reports.</summary>
            public string DetectedSqliteVersion { get; set; } = AppConfig.MinRequiredSqliteVersion.ToString();

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
            /// <exception cref="Exception">
            /// The configured <see cref="EventLogLoggerFailure"/>, for every request unless
            /// <see cref="FailOnlyEventLogEnabledLogger"/> narrows it to the event-log-enabled one.
            /// </exception>
            public IServyLogger CreateEventLogLogger(bool isEventLogEnabled)
            {
                EventLogLoggerRequests.Add(isEventLogEnabled);

                if (EventLogLoggerFailure != null && (isEventLogEnabled || !FailOnlyEventLogEnabledLogger))
                {
                    throw EventLogLoggerFailure;
                }

                return new EventLogLogger(AppConfig.EventSource, isEventLogEnabled: false);
            }

            /// <summary>Answers from <see cref="SqliteVersionIsSafe"/>.</summary>
            /// <param name="detectedVersion">Receives <see cref="DetectedSqliteVersion"/>.</param>
            /// <returns><see cref="SqliteVersionIsSafe"/>.</returns>
            public bool IsSqliteVersionSafe(out string detectedVersion)
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

            // Rollback AppSettings matrix states to maintain complete isolation integrity across sibling execution tracks
            ConfigurationManager.AppSettings["RestartTimeoutSeconds"] = _restartTimeoutSeconds;
            CoreSettingsLoader.TestOverride = null;

            // Clean up temporary local workspace state file markers if generated
            try
            {
                string keyPath = Path.Combine(TempDirectory, "test_restarter.key");
                string ivPath = Path.Combine(TempDirectory, "test_restarter.iv");
                if (File.Exists(keyPath)) File.Delete(keyPath);
                if (File.Exists(ivPath)) File.Delete(ivPath);
            }
            catch
            {
                // Suppress lock warnings on ephemeral files cleanup
            }

            base.Dispose();
        }
    }
}
