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

        private readonly string _tempConfigPath;

        public ProgramTests()
        {
            // Reset exit code before each execution run
            Environment.ExitCode = 0;

            // Generate an appsettings layout in the local output context
            _tempConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

            string mockConfigJson = "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"Data Source=:memory:;Version=3;\"\r\n" +
                "  },\r\n" +
                "  \"Security\": {\r\n" +
                "    \"AESKeyFilePath\": \"" + KeyFileName + "\",\r\n" +
                "    \"AESIVFilePath\": \"" + IvFileName + "\"\r\n" +
                "  },\r\n" +
                "  \"RestartTimeoutSeconds\": \"30\"\r\n" +
                "}";

            File.WriteAllText(_tempConfigPath, mockConfigJson);
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
        }

        #endregion

        #region Operational Pipeline & Validation Exceptions

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We provide a dummy service name that doesn't exist in our volatile memory database.
            // This triggers the serviceRepository.GetByName(...) == null failure branch cleanly.
            string[] args = new string[] { "GhostUnmanagedService" };

            // Act
            Program.Main(args);

            // Assert
            // The code sets Environment.ExitCode = 1 and safely disposes elements in finally block
            Assert.Equal(1, Environment.ExitCode);
        }

        [Fact]
        public void Main_FallbackConfigurationParsing_HandlesInvalidTimeoutGracefully()
        {
            // Arrange
            // Retain ConnectionStrings block to preserve :memory: context sandbox and prevent production DB fall-through
            string corruptedConfigJson = "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"Data Source=:memory:;Version=3;\"\r\n" +
                "  },\r\n" +
                "  \"RestartTimeoutSeconds\": \"NotAnInteger\"\r\n" +
                "}";
            File.WriteAllText(_tempConfigPath, corruptedConfigJson);

            string[] args = new string[] { "GhostUnmanagedService" };

            // Act
            Program.Main(args);

            // Assert
            // Program continues validation utilizing fallback default configuration bounds instead of throwing
            Assert.Equal(1, Environment.ExitCode);
        }

        #endregion

        #region Fatal Exception Resilience Blocks

        [Fact]
        public void Main_CorruptedAppDirectoryContext_FailsInitializationAndTriggersCatchBlocks()
        {
            // Arrange
            // Instead of deleting the config file completely-which triggers a fallback 
            // to the host's physical production Servy.db-provide a malformed layout containing an unparseable 
            // connection string. This safely simulates database driver crashes while remaining completely isolated.
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
            // The catch block catches the error, assigns a diagnostic log fallback, and marks failure exit
            Assert.Equal(1, Environment.ExitCode);
        }

        #endregion

        public void Dispose()
        {
            // Clean dynamic runtime artifacts cleanly
            try
            {
                if (File.Exists(_tempConfigPath))
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
        }
    }
}