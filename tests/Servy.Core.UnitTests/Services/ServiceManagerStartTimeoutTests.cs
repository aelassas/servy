using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Services;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System.ServiceProcess;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Covers the start-timeout budget <see cref="ServiceManager.StartServiceAsync"/> computes for a service
    /// that configures a pre-launch hook. The budget is only observable through the entry the method logs
    /// before issuing Start, so these tests drive the static <see cref="Logger"/> and therefore run
    /// sequentially with the rest of the logger suite.
    /// </summary>
    [Collection(LoggerCollection.Name)]
    public class ServiceManagerStartTimeoutTests
    {
        [Fact]
        public async Task StartServiceAsync_PreLaunchConfigured_LogsTimeoutIncludingResolvedPreLaunchWindow()
        {
            // Arrange
            const string serviceName = "PreLaunchService";
            const int preLaunchSeconds = 45;

            // The expected budget is the production formula fed with the pre-launch window the service
            // configures; a start path that resolved the window to 0 would log a strictly smaller number.
            int expectedTimeout = ServiceHelper.CalculateStartTimeout(null, preLaunchSeconds, 0);
            Assert.True(expectedTimeout > ServiceHelper.CalculateStartTimeout(null, 0, 0));

            var mockController = new Mock<IServiceControllerWrapper>();
            var mockControllerProvider = new Mock<IServiceControllerProvider>();
            var mockWindowsServiceApi = new Mock<IWindowsServiceApi>();
            var mockWin32ErrorProvider = new Mock<IWin32ErrorProvider>();
            var mockServiceRepository = new Mock<IServiceRepository>();

            // 1. Initial check (Stopped) 2. First poll (Stopped) 3. Second poll (Running) -> loop exits
            mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Stopped)
                .Returns(ServiceControllerStatus.Stopped)
                .Returns(ServiceControllerStatus.Running);

            mockServiceRepository.Setup(r => r.GetByNameAsync(serviceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(new ServiceDto
                                 {
                                     Name = serviceName,
                                     StartTimeout = null,
                                     PreLaunchExecutablePath = @"C:\Apps\pre-launch.exe",
                                     PreLaunchTimeoutSeconds = preLaunchSeconds
                                 });

            var serviceManager = new ServiceManager(
                _ => mockController.Object,
                mockControllerProvider.Object,
                mockWindowsServiceApi.Object,
                mockWin32ErrorProvider.Object,
                mockServiceRepository.Object);

            // Act
            var (result, textLogOutput) = await LogCapture.RunAsync(
                () => serviceManager.StartServiceAsync(serviceName, cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.True(result.IsSuccess);
            mockController.Verify(c => c.Start(), Times.Once);
            Assert.Contains($"with a timeout of {expectedTimeout} seconds", textLogOutput);
        }
    }
}
