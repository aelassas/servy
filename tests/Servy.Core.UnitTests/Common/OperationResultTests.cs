using Servy.Core.Common;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Common
{
    public class OperationResultTests
    {
        [Fact]
        public void Success_ShouldReturnSuccessfulResult()
        {
            // Act
            var result = OperationResult.Success();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);
        }

        [Theory]
        [InlineData("Operation failed due to timeout")]
        [InlineData("Access denied")]
        public void Failure_WithValidMessage_ShouldReturnFailedResult(string errorMessage)
        {
            // Act
            var result = OperationResult.Failure(errorMessage);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(errorMessage, result.ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Failure_WithInvalidMessage_ShouldThrowArgumentException(string invalidMessage)
        {
            // Act & Assert
            // Note: Using ! to suppress null warning as we are explicitly testing the runtime guard
            var exception = Assert.Throws<ArgumentException>(() =>
                OperationResult.Failure(invalidMessage));

            Assert.Equal("error", exception.ParamName);
            Assert.Contains("Failure result must include an error message.", exception.Message);
        }

        [Fact]
        public void Success_WithFailureMessage_ShouldSetProperties()
        {
            // Act
            var result = OperationResult.Success();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);
        }
    }
}
