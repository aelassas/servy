using Moq;
using Servy.Core.Services;

namespace Servy.Core.UnitTests.Services
{
    public class ServiceControllerProviderTests
    {
        private const string StandardTestService = "LanmanServer";

        [Fact]
        public void Constructor_NullFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceControllerProvider(null!));
        }

        [Fact]
        public void ParameterlessConstructor_ValidState_DoesNotThrow()
        {
            // The production factory wires up ServiceControllerWrapper, but building the delegate
            // does not execute its body, so no Service Control Manager handle is opened here.

            // Act
            var exception = Record.Exception(() => new ServiceControllerProvider());

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void GetService_NullName_ThrowsArgumentNullException()
        {
            // Arrange
            var provider = new ServiceControllerProvider(_ => throw new InvalidOperationException("The factory must not be invoked for an invalid name."));

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => provider.GetService(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void GetService_EmptyOrWhitespaceName_ThrowsArgumentNullException(string serviceName)
        {
            // Arrange
            var provider = new ServiceControllerProvider(_ => throw new InvalidOperationException("The factory must not be invoked for an invalid name."));

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => provider.GetService(serviceName));
        }

        [Fact]
        public void GetService_ValidName_PassesNameToFactoryAndReturnsItsResult()
        {
            // Arrange
            var wrapperMock = new Mock<IServiceControllerWrapper>();
            string? receivedName = null;
            var provider = new ServiceControllerProvider(name =>
            {
                receivedName = name;
                return wrapperMock.Object;
            });

            // Act
            var result = provider.GetService(StandardTestService);

            // Assert
            Assert.Equal(StandardTestService, receivedName);
            Assert.Same(wrapperMock.Object, result);
        }
    }
}
