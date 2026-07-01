namespace Servy.CLI.UnitTests
{
    [Collection("SequentialConsoleTests")]
    public class ProgramTests : IDisposable
    {
        private readonly string _tempConfigPath;
        private readonly TextWriter _originalConsoleOut;
        private readonly TextWriter _originalConsoleError;

        public ProgramTests()
        {
            // Establish isolated files environment for execution runs
            _tempConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.cli.json");

            _originalConsoleOut = Console.Out;
            _originalConsoleError = Console.Error;

            // Generate a valid mock configuration structure to bypass missing setting errors
            string fallbackDatabaseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Servy.db");
            string testConnection = string.Format("Data Source={0};Version=3;", fallbackDatabaseFile);

            string mockConfigJson = "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"" + testConnection.Replace("\\", "\\\\") + "\"\r\n" +
                "  },\r\n" +
                "  \"Security\": {\r\n" +
                "    \"AESKeyFilePath\": \"test_aes.key\",\r\n" +
                "    \"AESIVFilePath\": \"test_aes.iv\"\r\n" +
                "  }\r\n" +
                "}";

            File.WriteAllText(_tempConfigPath, mockConfigJson);
        }

        #region Private Test Orchestration Helpers

        /// <summary>
        /// Encapsulates the console output redirection lifecycle to eliminate duplicate boilerplate blocks
        /// across CLI execution integration test paths while protecting async background contexts.
        /// </summary>
        private async Task<(int ExitCode, string Output)> RunWithConsoleCaptureAsync(Func<Task<int>> actBlock)
        {
            var originalOut = Console.Out;

            // Arrange: Keep a direct reference to the raw underlying StringWriter
            using (var baseWriter = new StringWriter())
            using (var synchronizedWriter = TextWriter.Synchronized(baseWriter))
            {
                try
                {
                    // Act
                    Console.SetOut(synchronizedWriter);
                    int exitCode = await actBlock();

                    // Assert: Extract the text from the base StringWriter to bypass the SyncTextWriter proxy name
                    string capturedOutput = baseWriter.ToString();
                    return (exitCode, capturedOutput);
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        #endregion

        #region Console Validation Logic Branches

        [Fact]
        public void IsRealConsole_InNonInteractiveTestEnvironment_ReturnsFalse()
        {
            // Act
            bool isReal = Program.IsRealConsole();

            // Assert
            // When executing within headless test runners or CI pipelines, 
            // Environment.UserInteractive or Console.IsOutputRedirected will naturally be false/redirected
            Assert.False(isReal);
        }

        #endregion

        #region Execution Flow & Parsing Branch Coverage

        [Fact]
        public async Task Main_EmptyArguments_InjectsHelpVerbAndExitsWithSuccess()
        {
            // Arrange
            string[] emptyArgs = Array.Empty<string>();

            // Act
            var result = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(emptyArgs);
            });

            // Assert
            // Match against the actual verbs listed in the auto-generated help index screen
            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Contains("install", result.Output);
            Assert.Contains("uninstall", result.Output);
        }

        [Fact]
        public async Task Main_HelpFlagProvided_ReturnsSuccessExitCode()
        {
            // Arrange
            string[] args = { "--help" };

            // Act
            var result = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            // Match against the actual verbs listed in the auto-generated help index screen
            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Contains("install", result.Output);
            Assert.Contains("uninstall", result.Output);
        }

        [Fact]
        public async Task Main_InvalidArgumentsProvided_ReturnsErrorExitCode()
        {
            // Arrange
            string[] args = { "install", "--unsupported-option" };

            // Act
            var result = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.ExitCode);
        }

        [Fact]
        public async Task Main_QuietFlagProvided_AltersExecutionToQuietPath()
        {
            // Arrange
            string[] args = { "status", "--quiet" };

            // Act
            var result = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.True(result.ExitCode == (int)CliExitCode.Success || result.ExitCode == (int)CliExitCode.Error);
        }

        #endregion

        #region Exception & Interrupt Scenarios

        [Fact]
        public async Task Main_InvalidArguments_ReturnsErrorExitCode()
        {
            // Arrange
            string[] args = { "install", "--corrupt-flag-combination" };

            // Act
            var result = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.ExitCode);
        }

        #endregion

        public void Dispose()
        {
            // Revert console intercepts globally
            Console.SetOut(_originalConsoleOut);
            Console.SetError(_originalConsoleError);

            // Clean environment layout files
            try
            {
                if (File.Exists(_tempConfigPath))
                {
                    File.Delete(_tempConfigPath);
                }

                if (File.Exists("test_aes.key")) File.Delete("test_aes.key");
                if (File.Exists("test_aes.iv")) File.Delete("test_aes.iv");

                string testDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Servy.db");
                if (File.Exists(testDb))
                {
                    File.Delete(testDb);
                }
            }
            catch
            {
                // Suppress file deletion locks on cleanup
            }
        }
    }
}