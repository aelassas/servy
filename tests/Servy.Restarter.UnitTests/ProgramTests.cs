using Servy.Core.Logging;
using Servy.Infrastructure.Data;
using System.Data.SQLite;

namespace Servy.Restarter.UnitTests
{
    [CollectionDefinition("RestarterProgramTests", DisableParallelization = true)]
    public class RestarterProgramTestsCollection
    {
        // Enforces strict sequential isolation across the execution suite
    }

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
        private readonly string _expectedLogFilePath;
        private readonly SQLiteConnection _dbKeepAliveConnection;

        public ProgramTests()
        {
            // Reset exit code before each execution run
            Environment.ExitCode = 0;

            // Generate an appsettings layout in the local output context
            _tempConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            _expectedLogFilePath = Path.Combine(Logger.LogsPath, LogFileName);

            string mockConfigJson = "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \" " + SharedInMemoryConnectionString + "\"\r\n" +
                "  },\r\n" +
                "  \"Security\": {\r\n" +
                "    \"AESKeyFilePath\": \"" + KeyFileName + "\",\r\n" +
                "    \"AESIVFilePath\": \"" + IvFileName + "\"\r\n" +
                "  },\r\n" +
                "  \"RestartTimeoutSeconds\": \"30\"\r\n" +
                "}";

            File.WriteAllText(_tempConfigPath, mockConfigJson);

            // Open the persistent handle to anchor the shared memory segment lifecycle
            _dbKeepAliveConnection = new SQLiteConnection(SharedInMemoryConnectionString);
            _dbKeepAliveConnection.Open();

            // Bootstrap the schema table directly into the shared memory segment
            SQLiteDbInitializer.Initialize(_dbKeepAliveConnection);
        }

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
            string[] args = new string[] { invalidName }; // Triggers if (string.IsNullOrWhiteSpace(serviceName))

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Service name cannot be empty.");
        }

        #endregion

        #region Operational Pipeline & Validation Exceptions

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We provide a dummy service name that doesn't exist in our initialized memory database.
            // This triggers the serviceRepository.GetByName(...) == null failure branch cleanly.
            string serviceName = "GhostUnmanagedService";
            string[] args = new string[] { serviceName };

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
                string corruptedConfigJson = "{\r\n" +
                    "  \"ConnectionStrings\": {\r\n" +
                    "    \"DefaultConnection\": \"" + SharedInMemoryConnectionString + "\"\r\n" +
                    "  },\r\n" +
                    "  \"Security\": {\r\n" +
                    "    \"EncryptionKey\": \"StandardKeyPlaceholderForTestingOnly\"\r\n" +
                    "  },\r\n" +
                    "  \"RestartTimeoutSeconds\": \"NotAnInteger\"\r\n" +
                    "}";
                File.WriteAllText(_tempConfigPath, corruptedConfigJson);

                string[] args = new string[] { serviceName };

                // Act
                Program.Main(args);

                // Assert
                // The application successfully bypassed the corrupted token string and fell back 
                // to standard timeout bounds, successfully finishing the operational lifecycle with ExitCode 0.
                Assert.Equal(0, Environment.ExitCode);
                AssertLogContainsMessage($"Successfully restarted service '{serviceName}'.");
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
        public void Main_CorruptedAppDirectoryContext_FailsInitializationAndTriggersCatchBlocks()
        {
            // Arrange
            // Provide a malformed layout containing an unparseable connection string string. 
            // This safely simulates database driver crashes while remaining completely isolated.
            string brokenConnectionConfigJson = "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"Data Source=||InvalidPath||:?\"\r\n" +
                "  }\r\n" +
                "}";
            File.WriteAllText(_tempConfigPath, brokenConnectionConfigJson);

            // Provide a corrupted path syntax that forces an immediate crash in underlying drivers
            // before loggers can even initialize, hitting the root Exception block.
            string[] args = new string[] { "Invalid\\Service/Path:Characters" };

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            // Confirms that the catch-all execution path was explicitly hit
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

            Assert.True(File.Exists(_expectedLogFilePath), "The diagnostic restarter log file was never initialized on disk.");

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

                if (File.Exists(_expectedLogFilePath))
                {
                    File.Delete(_expectedLogFilePath);
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