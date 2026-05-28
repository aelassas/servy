using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Helpers;
using Servy.CLI.Models;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Helpers
{
    [Verb("testverb", HelpText = "Test verb")]
    internal class TestOptions { }

    public class HelperTests
    {
        // Helper to capture Console output
        private void RunTestWithConsoleCapture(Action testAction)
        {
            using (var swOut = new StringWriter())
            using (var swErr = new StringWriter())
            {
                var oldOut = Console.Out;
                var oldErr = Console.Error;
                try
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);
                    testAction();
                }
                finally
                {
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);
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
            // Use local capture within the async method
            using (var swOut = new StringWriter())
            using (var swErr = new StringWriter())
            {
                var oldOut = Console.Out;
                var oldErr = Console.Error;
                Console.SetOut(swOut);
                Console.SetError(swErr);

                try
                {
                    var task = Task.FromResult(CommandResult.Ok("Async"));
                    int exitCode = await Helper.PrintAndReturnAsync(task);
                    Assert.Equal(0, exitCode);
                }
                finally
                {
                    Console.SetOut(oldOut);
                    Console.SetError(oldErr);
                }
            }
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