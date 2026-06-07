using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Helpers;
using Servy.CLI.Models;

namespace Servy.CLI.UnitTests.Helpers
{
    [Verb("testverb", HelpText = "Test verb")]
    internal class TestOptions { }

    // FIX 1: Enforce sequential execution down a single thread apartment channel 
    // across the entire suite run pass to stop cross-thread Console static corruption.
    [Collection("SequentialConsoleTests")]
    public class HelperTests
    {
        // Thread lock gate safety valve
        private static readonly object _consoleLock = new object();

        // FIX 2: Consolidated, robust console capture mechanism with synchronized lock bounds
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
            // FIX 3: Re-route through the thread-safe locked execution pipeline sequence 
            // by calling the wrapped delegate synchronously within the apartment lock state.
            int exitCode = -1;

            RunTestWithConsoleCapture(() =>
            {
                var task = Task.FromResult(CommandResult.Ok("Async"));

                // Unroll the task value synchronously using GetAwaiter().GetResult() 
                // since the stream context is fully bound inside the active sync lock.
                exitCode = Helper.PrintAndReturnAsync(task).GetAwaiter().GetResult();
            });

            Assert.Equal(0, exitCode);
            await Task.CompletedTask;
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