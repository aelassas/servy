using Moq;
using Servy.Core;
using System;
using Xunit;

namespace Servy.Service.UnitTests
{
    public class TestableServiceTests
    {
        [Fact]
        public void OnStart_CallsInitializeStartup_AndEnsureValidWorkingDirectory()
        {
            // Arrange
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<ILogger>();
            var expectedOptions = new StartOptions();

            mockHelper
                .Setup(h => h.InitializeStartup(mockLogger.Object))
                .Returns(expectedOptions);

            var service = new TestableService(mockHelper.Object, mockLogger.Object);

            // Act
            service.TestOnStart(new string[0]);

            // Assert
            mockHelper.Verify(h => h.InitializeStartup(mockLogger.Object), Times.Once);
            mockHelper.Verify(h => h.EnsureValidWorkingDirectory(expectedOptions, mockLogger.Object), Times.Once);
        }

        [Fact]
        public void OnStart_WhenInitializeStartupReturnsNull_DoesNotCallEnsureValidWorkingDirectory()
        {
            // Arrange
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<ILogger>();

            mockHelper
                .Setup(h => h.InitializeStartup(mockLogger.Object))
                .Returns((StartOptions)null);

            var service = new TestableService(mockHelper.Object, mockLogger.Object);

            // Act
            service.TestOnStart(new string[0]);

            // Assert
            mockHelper.Verify(h => h.InitializeStartup(mockLogger.Object), Times.Once);
            mockHelper.Verify(h => h.EnsureValidWorkingDirectory(It.IsAny<StartOptions>(), mockLogger.Object), Times.Never);
        }

        [Fact]
        public void OnStart_WhenExceptionThrown_LogsError()
        {
            // Arrange
            var mockHelper = new Mock<IServiceHelper>();
            var mockLogger = new Mock<ILogger>();

            var exception = new InvalidOperationException("Test exception");

            mockHelper
                .Setup(h => h.InitializeStartup(mockLogger.Object))
                .Throws(exception);

            var service = new TestableService(mockHelper.Object, mockLogger.Object);

            // Act
            service.TestOnStart(new string[0]);

            // Assert
            mockLogger.Verify(l => l.Error(
                It.Is<string>(s => s.Contains("Exception in OnStart")),
                It.IsAny<Exception>()
                ), Times.Once);
        }
    }

    // Exposes protected OnStart for testing
    public static class TestableServiceExtensions
    {
        public static void TestOnStart(this TestableService service, string[] args)
        {
            typeof(TestableService)
                .GetMethod("OnStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(service, new object[] { args });
        }
    }
}
