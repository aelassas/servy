using CommandLine;
using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.Testing;
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
            Assert.Contains("install", verbs);   // Discovered via [Verb] reflection
            Assert.Contains("version", verbs);   // Hardcoded addition
            Assert.Contains("--version", verbs); // Hardcoded addition
        }

        [Fact]
        public void PrintAndReturn_SuccessResult_PrintsGreenMessage()
        {
            // Arrange
            var result = CommandResult.Ok("Success!");

            // Act
            var consoleOutput = ConsoleCapture.Run(() => Helper.PrintAndReturn(result));

            // Assert
            // Validate message content is natively dispatched to standard output stream.
            Assert.Equal(0, consoleOutput.Result);
            Assert.Contains("Success!", consoleOutput.StdOut);
            Assert.Empty(consoleOutput.StdErr);
        }

        [Fact]
        public void PrintAndReturn_FailureResult_PrintsRedMessage()
        {
            // Arrange
            var result = CommandResult.Fail("Error!", 1);

            // Act
            var consoleOutput = ConsoleCapture.Run(() => Helper.PrintAndReturn(result));

            // Assert
            // Validate message content is natively dispatched to standard error stream.
            Assert.Equal(1, consoleOutput.Result);
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

        [Fact]
        public void PrintAndReturn_NullResult_ReturnsOneWithoutPrinting()
        {
            // Arrange & Act
            var consoleOutput = ConsoleCapture.Run(() => Helper.PrintAndReturn(null!));

            // Assert
            // The null guard returns the literal 1, not result.ExitCode, so it is a distinct
            // contract rather than an alias of the failure path.
            Assert.Equal(1, consoleOutput.Result);
            Assert.Empty(consoleOutput.StdOut);
            Assert.Empty(consoleOutput.StdErr);
        }

        [Fact]
        public void PrintAndReturn_BlankMessage_PrintsNothingButKeepsExitCode()
        {
            // Arrange
            var result = CommandResult.Fail("   ", 3);

            // Act
            var consoleOutput = ConsoleCapture.Run(() => Helper.PrintAndReturn(result));

            // Assert
            // The blank-message arm is the only path where a non-zero exit code arrives with
            // nothing written to either stream.
            Assert.Equal(3, consoleOutput.Result);
            Assert.Empty(consoleOutput.StdOut);
            Assert.Empty(consoleOutput.StdErr);
        }

        [Fact]
        public async Task PrintAndReturnAsync_FailureResult_RoutesToStdErrAndReturnsExitCode()
        {
            // Arrange
            var task = Task.FromResult(CommandResult.Fail("Async Error", 1));

            // Act: the direct mirror of PrintAndReturn_FailureResult_PrintsRedMessage, so the
            // "behavior is identical across both paths" claim is verified on both outcomes.
            var consoleOutput = await ConsoleCapture.RunAsync(async () =>
            {
                return await Helper.PrintAndReturnAsync(task);
            });

            // Assert
            Assert.Equal(1, consoleOutput.Result);
            Assert.Contains("Async Error", consoleOutput.StdErr);
            Assert.Empty(consoleOutput.StdOut);
        }

        [Theory]
        [InlineData("Xml", ConfigFileType.Xml)]
        [InlineData("JSON", ConfigFileType.Json)]
        [InlineData("  xml  ", ConfigFileType.Xml)]
        public void TryParseFileType_ValidInputs_ReturnsTrueAndMapsCorrectly(string input, ConfigFileType expectedType)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType actualType, out string? error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
            Assert.Equal(expectedType, actualType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void TryParseFileType_NullOrWhitespaceInputs_ReturnsFalseAndSetsError(string? input)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType _, out string? error);

            // Assert
            Assert.False(result);
            Assert.Equal(Strings.Msg_ConfigFileTypeRequired, error);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("123")]
        [InlineData("xml,json")]
        [InlineData(" bogus ")]
        public void TryParseFileType_InvalidInputs_ReturnsFalseAndSetsError(string? input)
        {
            // Arrange & Act
            bool result = Helper.TryParseFileType(input, out ConfigFileType _, out string? error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_UnsupportedFileType, input!.Trim()), error);
        }
    }
}
