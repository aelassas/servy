using Moq;
using Xunit;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Core.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class ConsoleViewModelTests
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<ILogger> _loggerMock;

        public ConsoleViewModelTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _loggerMock = new Mock<ILogger>();
        }

        private ConsoleViewModel CreateViewModel()
        {
            return new ConsoleViewModel(
                _serviceRepoMock.Object,
                _serviceCommandsMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task SetSelectionActive_WhenFalse_ClearsBufferForReload()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                var service = new ConsoleService { Name = "TestService", StdoutPath = "out.log" };
                vm.SelectedService = service;
                vm.RawLines.Add(new LogLine("Existing Log", LogType.StdOut));

                vm.SetSelectionActive(true);
                Assert.True(vm.IsPaused);

                // Act - User clears selection (Resume)
                vm.SetSelectionActive(false);

                // Assert
                Assert.False(vm.IsPaused);
                // SwitchService clears RawLines immediately to prepare for fresh history
                Assert.Empty(vm.RawLines);
            }, createApp: true);
        }

        [Fact]
        public async Task ConsoleSearchText_Filter_FiltersVisibleLines()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                var line1 = new LogLine("Operation successful", LogType.StdOut);
                var line2 = new LogLine("System Crash", LogType.StdErr);

                vm.RawLines.Add(line1);
                vm.RawLines.Add(line2);

                // Act
                vm.ConsoleSearchText = "Crash";

                // Assert
                var filtered = vm.VisibleLines.Cast<LogLine>().ToList();
                Assert.Single(filtered);
                Assert.Contains("Crash", filtered[0].Text);
            }, createApp: true);
        }

        [Fact]
        public async Task ConsoleSearchText_WhenCleared_TriggersForcedScroll()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                bool scrollInvokedWithForce = false;
                vm.RequestScroll += (isForced) => scrollInvokedWithForce = isForced;
                vm.ConsoleSearchText = "filter text";

                // Act
                vm.ConsoleSearchText = string.Empty;

                // Assert
                Assert.True(scrollInvokedWithForce);
            }, createApp: true);
        }

        [Fact]
        public async Task SearchServicesAsync_PopulatesServicesCollection()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                var searchResults = new List<Service>
                {
                    new Service { Name = "AuthService" },
                    new Service { Name = "Gateway" }
                };

                _serviceCommandsMock
                    .Setup(x => x.SearchServicesAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(searchResults);

                // Act
                await vm.SearchCommand.ExecuteAsync("query");

                // Assert
                Assert.Equal(2, vm.Services.Count);
                Assert.Contains(vm.Services, s => s.Name == "AuthService");
                Assert.False(vm.IsBusy);
            }, createApp: true);
        }

        [Fact]
        public async Task SelectedService_Change_ResetsState()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                var service = new ConsoleService
                {
                    Name = "AppService",
                    StdoutPath = "C:\\out.log"
                };

                // Act
                vm.SelectedService = service;

                // Assert
                Assert.Equal("AppService", vm.SelectedService.Name);
                Assert.Empty(vm.RawLines);
            }, createApp: true);
        }

        [Fact]
        public async Task StopMonitoring_WithClearPoints_ResetsPidAndHistory()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                vm.Pid = "1234";
                vm.RawLines.Add(new LogLine("Test", LogType.StdOut));

                // Act
                vm.StopMonitoring(true);

                // Assert
                Assert.Equal("N/A", vm.Pid);
                Assert.Empty(vm.RawLines);
            }, createApp: true);
        }
    }
}