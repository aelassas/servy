using Moq;
using Servy.Core.Data;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Constants;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class PerformanceViewModelTests
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;

        public PerformanceViewModelTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _serviceCommandsMock = new Mock<IServiceCommands>();

            // 1. Setup AppConfig Mock
            _appConfigMock = new Mock<IAppConfiguration>();
            // Ensure the timer has a valid interval during initialization
            _appConfigMock.Setup(c => c.PerformanceRefreshIntervalInMs).Returns(1000);
        }

        private PerformanceViewModel CreateViewModel()
        {
            // 2. Inject the mock configuration
            return new PerformanceViewModel(
                _serviceRepoMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object);
        }

        [Fact]
        public async Task SearchCommand_ShouldPopulateServices()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                var mockData = new List<Service>
                {
                    new Service { Name = "TestSvc", Pid = 123 }
                };

                _serviceCommandsMock
                    .Setup(x => x.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockData);

                // Act
                await vm.SearchCommand.ExecuteAsync("Test");

                // Assert
                Assert.Single(vm.Services);
                Assert.Equal("TestSvc", vm.Services[0].Name);
            }, createApp: true);
        }

        [Fact]
        public async Task SelectedService_Change_ShouldResetUsageTexts()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                vm.CpuUsage = "50%";
                vm.RamUsage = "100 MB";

                // Act
                vm.SelectedService = new PerformanceService { Name = "NewSvc", Pid = 456 };

                // Assert
                Assert.Equal(UiConstants.NotAvailable, vm.CpuUsage);
                Assert.Equal(UiConstants.NotAvailable, vm.RamUsage);
            }, createApp: true);
        }

        [Fact]
        public async Task StopMonitoring_WithClearPoints_ShouldEmptyCollections()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();

                // Simulate existing points
                vm.CpuPointCollection = new PointCollection { new Point(0, 0), new Point(10, 10) };
                vm.RamPointCollection = new PointCollection { new Point(0, 0) };

                // Act
                vm.StopMonitoring(true);

                // Assert
                Assert.Empty(vm.CpuPointCollection);
                Assert.Empty(vm.RamPointCollection);
                Assert.Empty(vm.RamFillPoints);
            }, createApp: true);
        }

        [Fact]
        public async Task SelectedService_SetToNull_ShouldStopTimerAndNotCrash()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                vm.SelectedService = new PerformanceService { Name = "Svc", Pid = 123 };

                // Act & Assert
                var exception = Record.Exception(() => vm.SelectedService = null);

                Assert.Null(exception);
                Assert.Null(vm.SelectedService);
            }, createApp: true);
        }
    }
}