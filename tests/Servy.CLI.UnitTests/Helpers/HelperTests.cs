using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Helpers
{
    [Verb("testverb", HelpText = "Test verb")]
    internal class TestOptions { }

    // Enforce sequential execution down a single thread apartment channel 
    // across the entire suite run pass to stop cross-thread Console static corruption.
    [Collection("SequentialConsoleTests")]
    public class HelperTests
    {
        // Single SemaphoreSlim(1, 1) to gate access to the static Console object.
        private static readonly SemaphoreSlim _consoleSemaphore = new SemaphoreSlim(1, 1);

        // Async coordination primitive sharing the same conceptual isolation boundary 
        // to prevent thread pool yields from overlapping with active capturing blocks.
        private static readonly SemaphoreSlim _asyncConsoleLock = new SemaphoreSlim(1, 1);

        // Consolidated, robust console capture mechanism with synchronized lock bounds
        // Returns a tuple containing captured stdout/stderr text for assertion.
        private (string StdOut, string StdErr) RunTestWithConsoleCapture(Action testAction)
        {
            // Synchronously drain the async gate to block multi-threaded interleaved tests
            _asyncConsoleLock.Wait();
            var oldOut = Console.Out;
            var oldErr = Console.Error;

            try
            {
                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);

                    testAction();

                    // Explicitly restore static console output paths BEFORE the StringWriter streams
                    // are disposed to prevent ObjectDisposedExceptions from trailing execution writes.
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);

                    return (swOut.ToString(), swErr.ToString());
                }
            }
            finally
            {
                // CRITICAL: Ensure static console state restoration is guaranteed fallback safety
                Console.SetOut(oldOut);
                Console.SetError(oldErr);
                _asyncConsoleLock.Release();
            }
        }

        /// <summary>
        /// Asynchronous capture mechanism that hijacks the console stream for the duration of the test action,
        /// allowing for non-blocking execution across thread hops without deadlocks.
        /// Returns a tuple containing captured stdout/stderr text for assertion.
        /// </summary>
        private async Task<(string StdOut, string StdErr)> RunTestWithConsoleCaptureAsync(Func<Task> testAction)
        {
            // Arrange: Acquire async semaphore to gate access to static Console state
            await _consoleSemaphore.WaitAsync();

            // Capture original streams
            var oldOut = Console.Out;
            var oldErr = Console.Error;

            try
            {
                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {

                    // Act: Redirect streams
                    Console.SetOut(swOut);
                    Console.SetError(swErr);

                    // Await the action, which cleanly yields control to the thread pool 
                    // while the hijacked console remains correctly redirected.
                    await testAction();

                    return (swOut.ToString(), swErr.ToString());
                }
            }
            finally
            {
                // Assert/Cleanup: Restore original console state
                Console.SetOut(oldOut);
                Console.SetError(oldErr);

                // Always release the semaphore, regardless of test pass/fail
                _consoleSemaphore.Release();
            }
        }

        [Fact]
        public void GetVerbName_ValidOptionsClass_ReturnsName()
        {
            // Arrange & Act
            var name = Helper.GetVerbName<TestOptions>();

            // Assert
            Assert.Equal("testverb", name);
        }

        [Fact]
        public void GetVerbName_InvalidOptionsClass_ThrowsException()
        {
            // Arrange & Act & Assert
            Assert.Throws<InvalidOperationException>(() => Helper.GetVerbName<string>());
        }

        [Fact]
        public void GetVerbs_ReturnsAssemblyVerbsIncludingVersion()
        {
            // Arrange & Act
            var verbs = Helper.GetVerbs();

            // Assert
            Assert.Contains("version", verbs);
            Assert.Contains("--version", verbs);
        }

        [Fact]
        public void PrintAndReturn_SuccessResult_PrintsGreenMessage()
        {
            // Arrange
            var result = CommandResult.Ok("Success!");
            int exitCode = -1;

            // Act
            var consoleOutput = RunTestWithConsoleCapture(() =>
            {
                exitCode = Helper.PrintAndReturn(result);
            });

            // Assert
            // Validate message content is natively dispatched to standard output stream.
            Assert.Equal(0, exitCode);
            Assert.Contains("Success!", consoleOutput.StdOut);
            Assert.Empty(consoleOutput.StdErr);
        }

        [Fact]
        public void PrintAndReturn_FailureResult_PrintsRedMessage()
        {
            // Arrange
            var result = CommandResult.Fail("Error!", 1);
            int exitCode = -1;

            // Act
            var consoleOutput = RunTestWithConsoleCapture(() =>
            {
                exitCode = Helper.PrintAndReturn(result);
            });

            // Assert
            // Validate message content is natively dispatched to standard error stream.
            Assert.Equal(1, exitCode);
            Assert.Contains("Error!", consoleOutput.StdErr);
            Assert.Empty(consoleOutput.StdOut);
        }

        [Fact]
        public async Task PrintAndReturnAsync_ReturnsExitCode()
        {
            // Arrange
            int exitCode = -1;
            var task = Task.FromResult(CommandResult.Ok("Async Success"));

            // Act: Use the fully asynchronous redirection wrapper to capture state cleanly
            var consoleOutput = await RunTestWithConsoleCaptureAsync(async () =>
            {
                exitCode = await Helper.PrintAndReturnAsync(task);
            });

            // Assert
            // Assert the content payload in the async wrapper context.
            Assert.Equal(0, exitCode);
            Assert.Contains("Async Success", consoleOutput.StdOut);
            Assert.Empty(consoleOutput.StdErr);
        }

        [Theory]
        [InlineData("Xml", ConfigFileType.Xml)]
        [InlineData("JSON", ConfigFileType.Json)]
        public void TryParseFileType_ValidInputs_ReturnsTrueAndMapsCorrectly(string input, ConfigFileType expectedType)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType actualType, out string error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
            Assert.Equal(expectedType, actualType);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("123")]
        [InlineData("")]
        [InlineData("xml,json")]
        public void TryParseFileType_InvalidInputs_ReturnsFalseAndAppendsErrorToken(string input)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType _, out string error);

            // Assert
            Assert.False(result);
            Assert.Equal(Strings.Msg_InvalidConfigFileType, error);
        }
    }
}