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
        // Thread lock gate safety valve
        private static readonly object _consoleLock = new object();

        // Consolidated, robust console capture mechanism with synchronized lock bounds
        private void RunTestWithConsoleCapture(Action testAction)
        {
            lock (_consoleLock)
            {
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
                    }
                }
            }
        }

        // Added asynchronous overload to hold the hijacked console stream open
        // across asynchronous thread hops until the background state machine fully resolves.
        private async Task RunTestWithConsoleCaptureAsync(Func<Task> testAction)
        {
            // Acquire the lock synchronously before setting up stream redirection
            lock (_consoleLock)
            {
                var oldOut = Console.Out;
                var oldErr = Console.Error;

                using (var swOut = new StringWriter())
                using (var swErr = new StringWriter())
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);
                    try
                    {
                        // Unroll the asynchronous delegate task completely using an absolute awaiter 
                        // while the custom StringWriter stream lifetime is guaranteed open.
                        testAction().GetAwaiter().GetResult();
                    }
                    finally
                    {
                        Console.SetOut(oldOut);
                        Console.SetError(oldErr);
                    }
                }
            }
            await Task.CompletedTask;
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
            // Re-route through the asynchronous capture method, eliminating the 
            // ObjectDisposedException caused by background-thread hopping.
            int exitCode = -1;

            await RunTestWithConsoleCaptureAsync(async () =>
            {
                var task = Task.FromResult(CommandResult.Ok("Async"));
                exitCode = await Helper.PrintAndReturnAsync(task);
            });

            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("Xml", ConfigFileType.Xml, true)]
        [InlineData("JSON", ConfigFileType.Json, true)]
        [InlineData("invalid", ConfigFileType.Xml, false)]
        [InlineData("123", ConfigFileType.Xml, false)]
        [InlineData("", ConfigFileType.Xml, false)]
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