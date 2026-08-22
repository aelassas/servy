using Servy.Core.Logging;
using Servy.Infrastructure.Data;
using System.Data.SQLite;

namespace Servy.Restarter.UnitTests
{
    [Collection("RestarterProgramTests")]
    public class ProgramTests : IDisposable
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
        private readonly string _tempLogDir;
        private readonly string _expectedLogFilePath;
        private readonly SQLiteConnection _dbKeepAliveConnection;

        public ProgramTests()
        {
            // Reset exit code before each execution run
            Environment.ExitCode = 0;

            // Generate isolated test-run directories for configuration and log storage
            _tempConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            _tempLogDir = Path.Combine(Path.GetTempPath(), "ServyTestLogs", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempLogDir);

            _expectedLogFilePath = Path.Combine(_tempLogDir, LogFileName);

            // Pre-seed the static logger so empty/missing argument calls route to the isolated temp directory
            Logger.Initialize(LogFileName, logDirectory: _tempLogDir);

            File.WriteAllText(_tempConfigPath, BuildConfigJson("30"));

            // Open the persistent handle to anchor the shared memory segment lifecycle
            _dbKeepAliveConnection = new SQLiteConnection(SharedInMemoryConnectionString);
            _dbKeepAliveConnection.Open();

            // Bootstrap the schema table directly into the shared memory segment
            SQLiteDbInitializer.Initialize(_dbKeepAliveConnection);
        }

        private static string BuildConfigJson(string restartTimeoutSeconds) =>
            "{\r\n" +
            "  \"ConnectionStrings\": {\r\n" +
            "    \"DefaultConnection\": \"" + SharedInMemoryConnectionString + "\"\r\n" +
            "  },\r\n" +
            "  \"Security\": {\r\n" +
            "    \"AESKeyFilePath\": \"" + KeyFileName + "\",\r\n" +
            "    \"AESIVFilePath\": \"" + IvFileName + "\"\r\n" +
            "  },\r\n" +
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
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        public void Main_EmptyOrWhitespaceServiceName_SetsExitCodeTo1AndExitsEarly(string invalidName)
        {
            // Arrange
            string[] args = new string[] { invalidName, _tempLogDir }; // Triggers if (string.IsNullOrWhiteSpace(serviceName))

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Service name cannot be empty.");
        }

        #endregion

        #region Event Log Fallback & Security Guard Coverage

        /*
         * Note on Helper.EnsureEventSourceExists Exception Fallback Branch:
         * The catch block around Helper.EnsureEventSourceExists() (lines 57-66) catches EventLog creation or
         * access failures and falls back to file-only logging (EventLogLogger(..., isEventLogEnabled: false)).
         * Because AppConfig.EventSource is a compile-time constant ("Servy") and Helper delegates directly to
         * static System.Diagnostics.EventLog calls, triggering this exception in an integration test requires
         * running in an environment without Windows Event Log registry access.
         *
         * Note on DatabaseValidator.IsSqliteVersionSafe False Path:
         * The false branch of DatabaseValidator.IsSqliteVersionSafe inside Program.Main (lines 92-108)
         * triggers a fatal exit when the loaded System.Data.SQLite library version is below AppConfig.MinRequiredSqliteVersion.
         * Forcing this condition at the Program.Main integration level requires substituting the loaded native/managed
         * SQLite provider assembly at runtime. The underlying version validation rules are fully covered in DatabaseValidatorTests.cs.
         */

        #endregion

        #region Operational Pipeline & Validation Exceptions

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We provide a dummy service name that doesn't exist in our initialized memory database.
            // This triggers the serviceRepository.GetByName(...) == null failure branch cleanly.
            string serviceName = "GhostUnmanagedService";
            string[] args = new string[] { serviceName, _tempLogDir };

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

                string[] args = new string[] { serviceName, _tempLogDir };

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

        #endregion

        #region Fatal Exception Resilience Blocks

        [Fact]
        public void Main_BrokenConnectionString_HitsCatchAllViaScopedLogger()
        {
            // Arrange
            // Provide a malformed layout containing an unparseable connection string.
            // This safely simulates database driver crashes while remaining completely isolated.
            string brokenConnectionConfigJson = "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"Data Source=||InvalidPath||:?\"\r\n" +
                "  },\r\n" +
                "  \"Security\": {\r\n" +
                "    \"AESKeyFilePath\": \"" + KeyFileName + "\",\r\n" +
                "    \"AESIVFilePath\": \"" + IvFileName + "\"\r\n" +
                "  }\r\n" +
                "}";
            File.WriteAllText(_tempConfigPath, brokenConnectionConfigJson);

            // Pass a target service name argument. The broken DefaultConnection string makes
            // the SQLite open fail inside GetByName, after the scoped logger exists - exercising
            // the scoped-logger arm of the catch-all block.
            string[] args = new string[] { "Invalid\\Service/Path:Characters", _tempLogDir };

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

        #endregion

        public void Dispose()
        {
            // Force logger teardown first to unlock active files
            Logger.Shutdown();

            // Explicitly unlock and drop the keep-alive memory connection reference
            _dbKeepAliveConnection?.Dispose();

            // Clean dynamic runtime artifacts cleanly
            try
            {
                if (File.Exists(_tempConfigPath))
                {
                    File.Delete(_tempConfigPath);
                }

                if (Directory.Exists(_tempLogDir))
                {
                    Directory.Delete(_tempLogDir, true);
                }

                if (File.Exists(KeyFileName)) File.Delete(KeyFileName);
                if (File.Exists(IvFileName)) File.Delete(IvFileName);
            }
            catch
            {
                // Suppress disposal file-locks
            }
        }
    }
}
