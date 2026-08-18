using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.Testing;
using System;
using System.Threading.Tasks;
using Xunit;
using Helper = Servy.CLI.Helpers.Helper;


namespace Servy.CLI.UnitTests.Helpers
{
    [Verb("testverb", HelpText = "Test verb")]
    internal class TestOptions { }

    // Enforce sequential execution across the entire suite run pass to stop cross-thread Console static corruption.
    [Collection("SequentialConsoleTests")]
    public class HelperTests
    {
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
            var consoleOutput = ConsoleCapture.Run(() =>
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
            var consoleOutput = ConsoleCapture.Run(() =>
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
            var task = Task.FromResult(CommandResult.Ok("Async Success"));

            // Act: Use the fully asynchronous redirection wrapper to capture state cleanly
            var consoleOutput = await ConsoleCapture.RunAsync(async () =>
            {
                return await Helper.PrintAndReturnAsync(task);
            });

            // Assert
            // Assert the content payload in the async wrapper context.
            Assert.Equal(0, consoleOutput.Result);
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
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void TryParseFileType_NullOrWhitespaceInputs_ReturnsFalseAndSetsError(string input)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType _, out string error);

            // Assert
            Assert.False(result);
            Assert.Equal(Strings.Msg_ConfigFileTypeRequired, error);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("123")]
        [InlineData("xml,json")]
        public void TryParseFileType_InvalidInputs_ReturnsFalseAndSetsError(string input)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType _, out string error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_UnsupportedFileType, input), error);
        }
    }
}
