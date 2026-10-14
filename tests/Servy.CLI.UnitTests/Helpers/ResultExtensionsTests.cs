using Servy.CLI.Helpers;
using Servy.CLI.Resources;
using Servy.Core.Common;

namespace Servy.CLI.UnitTests.Helpers
{
    public class ResultExtensionsTests
    {
        [Fact]
        public void ToFailure_NullResult_ReturnsUnknownError()
        {
            // Arrange
            OperationResult? res = null;

            // Act
            var result = res!.ToFailure();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Strings.Msg_UnknownError, result.Message);
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public void ToFailure_ResultWithoutErrorMessage_ReturnsUnknownError()
        {
            // Arrange: Success() carries a null ErrorMessage, the only way to reach the blank arm
            var res = OperationResult.Success();

            // Act
            var result = res.ToFailure();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Strings.Msg_UnknownError, result.Message);
        }

        [Fact]
        public void ToFailure_ResultWithErrorMessage_KeepsTheMessage()
        {
            // Arrange
            var res = OperationResult.Failure("boom");

            // Act
            var result = res.ToFailure();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("boom", result.Message);
        }
    }
}
