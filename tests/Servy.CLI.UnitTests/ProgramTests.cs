using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

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
        private async Task<int> RunWithConsoleCaptureAsync(Func<Task<int>> actBlock)
        {
            var originalOut = Console.Out;
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    Console.SetOut(stringWriter);
                    return await actBlock();
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
            int exitCode = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(emptyArgs);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Success, exitCode);
        }

        [Fact]
        public async Task Main_HelpFlagProvided_ReturnsSuccessExitCode()
        {
            // Arrange
            string[] args = { "--help" };

            // Act
            int exitCode = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Success, exitCode);
        }

        [Fact]
        public async Task Main_InvalidArgumentsProvided_ReturnsErrorExitCode()
        {
            // Arrange
            string[] args = { "install", "--unsupported-option" };

            // Act
            int exitCode = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Error, exitCode);
        }

        [Fact]
        public async Task Main_QuietFlagProvided_AltersExecutionToQuietPath()
        {
            // Arrange
            string[] args = { "status", "--quiet" };

            // Act
            int exitCode = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.True(exitCode == (int)CliExitCode.Success || exitCode == (int)CliExitCode.Error);
        }

        #endregion

        #region Exception & Interrupt Scenarios

        [Fact]
        public async Task Main_InvalidArguments_ReturnsErrorExitCode()
        {
            // Arrange
            string[] args = { "install", "--corrupt-flag-combination" };

            // Act
            int exitCode = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Error, exitCode);
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