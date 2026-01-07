using Moq;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
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
        private readonly Mock<ILogger> _loggerMock;

        public PerformanceViewModelTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _loggerMock = new Mock<ILogger>();
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
                    new Service{ Name = "TestSvc", Pid = 123 }
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
                Assert.Equal("N/A", vm.CpuUsage);
                Assert.Equal("N/A", vm.RamUsage);
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
                // Check internal lists via logic (RamFillPoints should be a new empty collection)
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

                // Act & Assert (Verification that no exception is thrown)
                var exception = Record.Exception(() => vm.SelectedService = null);

                Assert.Null(exception);
                Assert.Null(vm.SelectedService);
            }, createApp: true);
        }

        private PerformanceViewModel CreateViewModel()
        {
            return new PerformanceViewModel(_serviceRepoMock.Object, _serviceCommandsMock.Object, _loggerMock.Object);
        }
    }
}