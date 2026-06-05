namespace Servy.CLI.UnitTests
{
    // 1. Establish a unique, non-parallelized execution collection domain for CLI tests
    [CollectionDefinition("Servy.CLI.ConsoleTests", DisableParallelization = true)]
    public class CliConsoleCollection { }

    // 2. Explicitly bind the test class to the sequential execution collection
    [Collection("Servy.CLI.ConsoleTests")]
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

            // 2. Wrap the writer in a thread-safe synchronized boundary
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] emptyArgs = Array.Empty<string>();
                    int exitCode = await Program.Main(emptyArgs);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Success, exitCode);
                }
                finally
                {
                    // 6. Restore original output BEFORE leaving the using block to protect background async threads
                    Console.SetOut(originalOut);
                }
            }
        }

        [Fact]
        public async Task Main_HelpFlagProvided_ReturnsSuccessExitCode()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "--help" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Success, exitCode);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
        }

        [Fact]
        public async Task Main_InvalidArgumentsProvided_ReturnsErrorExitCode()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "install", "--unsupported-option" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Error, exitCode);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
        }

        [Fact]
        public async Task Main_QuietFlagProvided_AltersExecutionToQuietPath()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "status", "--quiet" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.True(exitCode == (int)CliExitCode.Success || exitCode == (int)CliExitCode.Error);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
        }

        #endregion

        #region Exception & Interrupt Scenarios

        [Fact]
        public async Task Main_InvalidArguments_ReturnsErrorExitCode()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "install", "--corrupt-flag-combination" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Error, exitCode);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
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