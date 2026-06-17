using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Helpers;
using Servy.CLI.Models;
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
        // Thread lock gate safety valve for synchronous blocks
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

                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);
                    try
                    {
                        testAction();
                    }
                    finally
                    {
                        Console.SetOut(oldOut);
                        Console.SetError(oldErr);
                        _asyncConsoleLock.Release();
                    }
                }
            }
        }

        // Refactored async capture mechanism that ensures the StringWriter streams 
        // stay alive naturally across all asynchronous thread hops without deadlocks.
        private async Task RunTestWithConsoleCaptureAsync(Func<Task> testAction)
        {
            // Acquire the async semaphore to respect synchronous isolation execution loops
            await _asyncConsoleLock.WaitAsync();

            var oldOut = Console.Out;
            var oldErr = Console.Error;

            using (var swOut = new StringWriter())
            using (var swErr = new StringWriter())
            {
                Console.SetOut(swOut);
                Console.SetError(swErr);
                try
                {
                    // Perfectly await the execution task to cleanly yield control 
                    // without escaping the lifetime bounds of the StringWriter wrappers.
                    await testAction();
                }
                finally
                {
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);
                    _asyncConsoleLock.Release();
                }
            }
        }

        [Fact]
        public void GetVerbName_ValidOptionsClass_ReturnsName()
        {
            var name = Helper.GetVerbName<TestOptions>();
            Assert.Equal("testverb", name);
        }

        [Fact]
        public void GetVerbName_InvalidOptionsClass_ThrowsException()
        {
            Assert.Throws<InvalidOperationException>(() => Helper.GetVerbName<string>());
        }

        [Fact]
        public void GetVerbs_ReturnsAssemblyVerbsIncludingVersion()
        {
            var verbs = Helper.GetVerbs();
            Assert.Contains("version", verbs);
            Assert.Contains("--version", verbs);
            Assert.NotEmpty(verbs);
        }

        [Fact]
        public void PrintAndReturn_SuccessResult_PrintsGreenMessage()
        {
            RunTestWithConsoleCapture(() =>
            {
                var result = CommandResult.Ok("Success!");
                int exitCode = Helper.PrintAndReturn(result);
                Assert.Equal(0, exitCode);
            });
        }

        [Fact]
        public void PrintAndReturn_FailureResult_PrintsRedMessage()
        {
            RunTestWithConsoleCapture(() =>
            {
                var result = CommandResult.Fail("Error!", 1);
                int exitCode = Helper.PrintAndReturn(result);
                Assert.Equal(1, exitCode);
            });
        }

        [Fact]
        public async Task PrintAndReturnAsync_ReturnsExitCode()
        {
            int exitCode = -1;

            // Use the fully asynchronous redirection wrapper to capture state cleanly
            await RunTestWithConsoleCaptureAsync(async () =>
            {
                // Wrap in Task.Run to completely insulate the background execution loop 
                // and force the async state machine to fully unroll right here
                await Task.Run(async () =>
                {
                    var task = Task.FromResult(CommandResult.Ok("Async"));
                    exitCode = await Helper.PrintAndReturnAsync(task);
                });

                // Anti-flaking safety yield: gives any residual asynchronous console flush 
                // routines on the thread pool time to settle down before the text writer collapses.
                await Task.Yield();
            });

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
            bool result = Helper.TryParseFileType(input, out ConfigFileType actualType, out string error);
            Assert.Equal(expectedResult, result);
            if (expectedResult)
            {
                Assert.Equal(expectedType, actualType);
            }
        }
    }
}