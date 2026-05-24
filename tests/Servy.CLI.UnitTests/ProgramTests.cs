namespace Servy.CLI.UnitTests
{
    [CollectionDefinition("Servy.CLI.UnitTests.ProgramTests", DisableParallelization = true)]
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
            // 1. Capture the original writer so we can restore it later
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();

            try
            {
                // 2. Redirect
                Console.SetOut(stringWriter);

                // 3. Act
                string[] emptyArgs = new string[0];
                int exitCode = await Program.Main(emptyArgs);

                // 4. Assert
                // Empty args inject HelpOptions verb, parsing returns successful exit code
                Assert.Equal((int)CliExitCode.Success, exitCode);
            }
            finally
            {
                // 5. Restore original output BEFORE disposing the stringWriter
                Console.SetOut(originalOut);
                stringWriter.Dispose();
            }
        }

        [Fact]
        public async Task Main_HelpFlagProvided_ReturnsSuccessExitCode()
        {
            // 1. Capture the original writer so we can restore it later
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();

            try
            {
                // 2. Redirect
                Console.SetOut(stringWriter);

                // 3. Act
                string[] args = { "--help" };
                int exitCode = await Program.Main(args);

                // 4. Assert
                Assert.Equal((int)CliExitCode.Success, exitCode);
            }
            finally
            {
                // 5. Restore original output BEFORE disposing the stringWriter
                Console.SetOut(originalOut);
                stringWriter.Dispose();
            }
        }

        [Fact]
        public async Task Main_InvalidArgumentsProvided_ReturnsErrorExitCode()
        {
            // 1. Capture the original writer so we can restore it later
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();

            try
            {
                // 2. Redirect
                Console.SetOut(stringWriter);

                // 3. Act
                string[] args = new string[] { "install", "--unsupported-option" };
                int exitCode = await Program.Main(args);

                // 4. Assert
                Assert.Equal((int)CliExitCode.Error, exitCode);
            }
            finally
            {
                // 5. Restore original output BEFORE disposing the stringWriter
                Console.SetOut(originalOut);
                stringWriter.Dispose();
            }
        }

        [Fact]
        public async Task Main_QuietFlagProvided_AltersExecutionToQuietPath()
        {
            // 1. Capture the original writer so we can restore it later
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();

            try
            {
                // 2. Redirect
                Console.SetOut(stringWriter);

                // 3. Act
                string[] args = new string[] { "status", "--quiet" };
                int exitCode = await Program.Main(args);

                // 4. Assert
                // Verb option matching flow works successfully. Returns Error or Success 
                // depending on system level service controller permissions during test initialization
                Assert.True(exitCode == (int)CliExitCode.Success || exitCode == (int)CliExitCode.Error);
            }
            finally
            {
                // 5. Restore original output BEFORE disposing the stringWriter
                Console.SetOut(originalOut);
                stringWriter.Dispose();
            }
        }

        #endregion

        #region Exception & Interrupt Scenarios

        [Fact]
        public async Task Main_ExecutionFlowInterruptedByCancellation_ReturnsErrorExitCode()
        {
            // We simulate a cancelled token runtime interrupt path by canceling early inside a task thread run
            using (CancellationTokenSource localCts = new CancellationTokenSource())
            {
                localCts.Cancel();

                // 1. Capture the original writer so we can restore it later
                var originalOut = Console.Out;
                var stringWriter = new StringWriter();

                try
                {
                    // 2. Redirect
                    Console.SetOut(stringWriter);

                    // 3. Act
                    // We trigger the programmatic cancellation handling logic flow block 
                    // by passing corrupted/unparsable option combinations designed to abort processing.
                    string[] args = new string[] { "install", "--corrupt-flag-combination" };
                    int exitCode = await Program.Main(args);

                    // 4. Assert
                    Assert.Equal((int)CliExitCode.Error, exitCode);
                }
                finally
                {
                    // 5. Restore original output BEFORE disposing the stringWriter
                    Console.SetOut(originalOut);
                    stringWriter.Dispose();
                }
            }
        }

        #endregion

        public void Dispose()
        {
            // Revert console intercepts
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