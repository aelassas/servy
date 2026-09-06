using Servy.Core.Common;
using System.Reflection;

namespace Servy.Core.UnitTests.Common
{
    public class OperationResultTests
    {
        [Fact]
        public void OperationResult_ExposesNoConstructorThatCanBypassTheErrorInvariant()
        {
            // Arrange
            var type = typeof(OperationResult);

            // Act & Assert
            // sealed: no subclass can reach a widened constructor (the other half of the #4265 fix)
            Assert.True(type.IsSealed, "OperationResult must stay sealed.");

            // Exactly one constructor, and it must be private. GetConstructors() without flags
            // returns public instance constructors only, so it reports empty for the protected
            // constructor #4265 was about, and for an internal one that any Servy.Core caller
            // could use to build a failure with no error message.
            var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var ctor = Assert.Single(ctors);
            Assert.True(ctor.IsPrivate,
                $"OperationResult's constructor is {(ctor.IsAssembly ? "internal" : ctor.IsFamily ? "protected" : "public")}; " +
                "only Success()/Failure() may construct a result.");
        }

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
        public void Failure_WithInvalidMessage_ShouldThrowArgumentException(string? invalidMessage)
        {
            // Act & Assert
            // Note: Using ! to suppress null warning as we are explicitly testing the runtime guard
            var exception = Assert.Throws<ArgumentException>(() =>
                OperationResult.Failure(invalidMessage!));

            Assert.Equal("error", exception.ParamName);
            Assert.Contains("Failure result must include an error message.", exception.Message);
        }
    }
}
