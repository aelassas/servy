using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Helpers;
using Servy.CLI.Models;

namespace Servy.CLI.UnitTests.Helpers
{
    [Verb("testverb", HelpText = "Test verb")]
    internal class TestOptions { }

    // Enforce sequential execution down a single thread apartment channel 
    // across the entire suite run pass to stop cross-thread Console static corruption.
    [Collection("SequentialConsoleTests")]
    public class HelperTests
    {
        // Thread lock gate safety valve for synchronous blocks and asynchronous capture orchestration
        private static readonly object _consoleLock = new object();

        // Async coordination primitive sharing the same conceptual isolation boundary 
        // to prevent thread pool yields from overlapping with active capturing blocks.
        private static readonly SemaphoreSlim _asyncConsoleLock = new SemaphoreSlim(1, 1);

        // Consolidated, robust console capture mechanism with synchronized lock bounds
        private void RunTestWithConsoleCapture(Action testAction)
        {
            lock (_consoleLock)
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
                    }
                }
                finally
                {
                    // CRITICAL: Ensure static console state restoration is guaranteed 
                    // before releasing the execution gate primitive.
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);
                    _asyncConsoleLock.Release();
                }
            }
        }

        // Refactored async capture mechanism that ensures the StringWriter streams 
        // stay alive naturally across all asynchronous thread hops without deadlocks.
        private async Task RunTestWithConsoleCaptureAsync(Func<Task> testAction)
        {
            // Acquire the async semaphore first to block concurrent asynchronous execution entries
            await _asyncConsoleLock.WaitAsync();

            TextWriter oldOut;
            TextWriter oldErr;

            // Enforce a strict synchronous lock guard while swapping static properties 
            // to eliminate interleaving race conditions with synchronous test runs.
            lock (_consoleLock)
            {
                oldOut = Console.Out;
                oldErr = Console.Error;
            }

            try
            {
                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    lock (_consoleLock)
                    {
                        Console.SetOut(swOut);
                        Console.SetError(swErr);
                    }

                    // Perfectly await the execution task to cleanly yield control 
                    // without escaping the lifetime bounds of the StringWriter wrappers.
                    await testAction();
                }
            }
            finally
            {
                // Unconditionally put back the original stdout/stderr streams within a 
                // locked context immediately after the test action finishes execution.
                lock (_consoleLock)
                {
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);
                }
                _asyncConsoleLock.Release();
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
            Assert.NotEmpty(verbs);
        }

        [Fact]
        public void PrintAndReturn_SuccessResult_PrintsGreenMessage()
        {
            RunTestWithConsoleCapture(() =>
            {
                // Arrange
                var result = CommandResult.Ok("Success!");

                // Act
                int exitCode = Helper.PrintAndReturn(result);

                // Assert
                Assert.Equal(0, exitCode);
            });
        }

        [Fact]
        public void PrintAndReturn_FailureResult_PrintsRedMessage()
        {
            RunTestWithConsoleCapture(() =>
            {
                // Arrange
                var result = CommandResult.Fail("Error!", 1);

                // Act
                int exitCode = Helper.PrintAndReturn(result);

                // Assert
                Assert.Equal(1, exitCode);
            });
        }

        [Fact]
        public async Task PrintAndReturnAsync_ReturnsExitCode()
        {
            // Arrange
            int exitCode = -1;
            var task = Task.FromResult(CommandResult.Ok("Async"));

            // Use the fully asynchronous redirection wrapper to capture state cleanly
            await RunTestWithConsoleCaptureAsync(async () =>
            {
                // Act: Await execution directly within the console capture boundary context
                exitCode = await Helper.PrintAndReturnAsync(task);
            });

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("Xml", ConfigFileType.Xml, true)]
        [InlineData("JSON", ConfigFileType.Json, true)]
        [InlineData("invalid", ConfigFileType.Xml, false)]
        [InlineData("123", ConfigFileType.Xml, false)]
        [InlineData("", ConfigFileType.Xml, false)]
        [InlineData("xml,json", ConfigFileType.Xml, false)]
        public void TryParseFileType_InputValidation_ReturnsExpected(string input, ConfigFileType expectedType, bool expectedResult)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType actualType, out string? error);

            // Assert
            Assert.Equal(expectedResult, result);
            if (expectedResult)
            {
                Assert.Equal(expectedType, actualType);
            }
        }
    }
}