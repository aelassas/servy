using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests
{
    [Collection("SequentialConsoleTests")]
    public class ProgramTests : IDisposable
    {
        // CONSTANT STRINGS HOISTING: Centralize artifact filenames to prevent cleanup drift
        private const string AesKeyFileName = "test_aes.key";
        private const string AesIvFileName = "test_aes.iv";
        private const string DatabaseFileName = "Test_Servy.db";

        private readonly TextWriter _originalConsoleOut;
        private readonly TextWriter _originalConsoleError;

        public ProgramTests()
        {
            // Arrange
            _originalConsoleOut = Console.Out;
            _originalConsoleError = Console.Error;
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
            if (Environment.UserInteractive
                && !Console.IsOutputRedirected
                && !Console.IsErrorRedirected)
            {
                return;
            }

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
        public async Task Main_QuietFlagProvided_AltersExecutionToQuietPath()
        {
            // Arrange
            // FIX: Supply the service name via the required explicit option switch (-n) 
            // to satisfy the CommandLineParser constraints and successfully route into the quiet logic path.
            string[] args = { "status", "-n", "NonExistentServiceForTestingOnly", "--quiet" };

            // Act
            var result = await RunWithConsoleCaptureAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            // The parser succeeds, but the execution layer yields Error (1) due to the missing database entry,
            // proving the application operational pipeline ran while maintaining complete silence.
            Assert.Equal((int)CliExitCode.Error, result.ExitCode);

            // Verify that no loading animation frames or status text fragments were written to the console buffer
            Assert.True(string.IsNullOrEmpty(result.Output), "Console output should be completely suppressed when the --quiet flag is supplied.");
        }

        [Fact]
        public async Task RunWithLoadingAnimation_WhenOutputIsRedirectedOrQuiet_BypassesAnimationLoop()
        {
            // Arrange
            var outputWriter = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(outputWriter);

            try
            {
                // Access the internal test seam via reflection to explicitly force an un-redirected state scenario 
                // to verify that the internal evaluation flag catches the runtime configuration bypass blocks.
                var field = typeof(CLI.Helpers.ConsoleHelper).GetField("_isOutputRedirectedOverride", BindingFlags.Static | BindingFlags.NonPublic);
                field?.SetValue(null, true); // Force out-of-bounds skip branch simulation

                bool executionCompleted = false;

                // Act
                await CLI.Helpers.ConsoleHelper.RunWithLoadingAnimation(async () =>
                {
                    await Task.Delay(10);
                    executionCompleted = true;
                }, "Testing Quiet Mode Animation");

                // Assert
                Assert.True(executionCompleted);

                // An un-redirected normal console would print frame strings. Proving it is blank confirms the bypass executed successfully.
                string capturedText = outputWriter.ToString();
                Assert.True(string.IsNullOrEmpty(capturedText) || capturedText == Environment.NewLine,
                    "The loading animation mechanism should skip writing animation frames when quiet conditions are enforced.");
            }
            finally
            {
                // Clean up console redirection states to preserve host environment test runner stability
                Console.SetOut(originalOut);
                var field = typeof(CLI.Helpers.ConsoleHelper).GetField("_isOutputRedirectedOverride", BindingFlags.Static | BindingFlags.NonPublic);
                field?.SetValue(null, null);
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
                if (File.Exists(AesKeyFileName)) File.Delete(AesKeyFileName);
                if (File.Exists(AesIvFileName)) File.Delete(AesIvFileName);

                string testDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);
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