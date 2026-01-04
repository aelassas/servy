using Moq;
using Servy.Core.Data;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using System.Windows;
using System.Windows.Media;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class PerformanceViewModelTests
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;

        public PerformanceViewModelTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
        }

        [Fact]
        public async Task Constructor_ShouldGenerateGridLines()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange & Act
                var vm = CreateViewModel();

                // Assert
                Assert.NotEmpty(vm.CpuHorizontalGridLines);
                Assert.NotEmpty(vm.RamHorizontalGridLines);
                // 10 rows/cols + 1 edge line = 11 lines
                Assert.Equal(11, vm.CpuHorizontalGridLines.Count);
            }, createApp: false);
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
                    .Setup(x => x.SearchServicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockData);

                // Act
                await vm.SearchCommand.ExecuteAsync("Test");

                // Assert
                Assert.Single(vm.Services);
                Assert.Equal("TestSvc", vm.Services[0].Name);
            }, createApp: false);
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
            }, createApp: false);
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
            }, createApp: false);
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
            }, createApp: false);
        }

        private PerformanceViewModel CreateViewModel()
        {
            return new PerformanceViewModel(_serviceRepoMock.Object, _serviceCommandsMock.Object);
        }
    }
}