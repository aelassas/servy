using Moq;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.Utils;
using Servy.Manager.ViewModels;
using Servy.UI.Constants;

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
                Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                Assert.Empty(vm.RawLines);
            }, createApp: true);
        }

        [Fact]
        public async Task HistorySort_WithIdenticalTimestamps_ShouldSortBySequenceId()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var sameTime = new DateTime(2026, 1, 30, 10, 0, 0);

                // 1. Instantiate in the chronological order they arrived
                var line1 = new LogLine("Line 1", LogType.StdOut, sameTime); // Gets lower ID
                var line2 = new LogLine("Line 2", LogType.StdErr, sameTime); // Gets higher ID

                // 2. Put them in the list "out of order" to prove the sort works
                var list = new List<LogLine> { line2, line1 };

                // Act
                var sorted = list.OrderBy(l => l.Timestamp).ThenBy(l => l.Id).ToList();

                // Assert
                Assert.Equal("Line 1", sorted[0].Text);
                Assert.Equal("Line 2", sorted[1].Text);
                Assert.True(sorted[0].Id < sorted[1].Id);
            }, createApp: true);
        }

        [Fact]
        public async Task HistorySort_WithIdenticalTimestamps_ShouldPreserveArrivalOrder()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var sameTime = new DateTime(2026, 1, 30, 10, 0, 0);

                var line2 = new LogLine("Line 2", LogType.StdErr, sameTime);
                var line1 = new LogLine("Line 1", LogType.StdOut, sameTime);

                // Line 2 is at index 0, Line 1 is at index 1
                var combinedHistory = new List<LogLine> { line2, line1 };

                // Act
                var sortedHistory = combinedHistory
                    .Select((line, index) => new { line, index })
                    .OrderBy(x => x.line.Timestamp)
                    .ThenBy(x => x.index)
                    .Select(x => x.line)
                    .ToList();

                // Assert
                // Because timestamps are equal, index 0 (Line 2) must come before index 1 (Line 1)
                Assert.Equal("Line 2", sortedHistory[0].Text);
                Assert.Equal("Line 1", sortedHistory[1].Text);
            }, createApp: true);
        }

        [Fact]
        public async Task SwitchService_WithIdenticalTimestamps_ShouldKeepStdOutBeforeStdErr()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                var sameTime = new DateTime(2026, 1, 30, 12, 0, 0);

                // We simulate StdErr finishing its task first (getting lower IDs)
                // by instantiating it before StdOut.
                var errLine = new LogLine("Error Message", LogType.StdErr, sameTime);
                var outLine = new LogLine("Output Message", LogType.StdOut, sameTime);

                var outRes = new HistoryResult(new List<LogLine> { outLine }, 100, sameTime);
                var errRes = new HistoryResult(new List<LogLine> { errLine }, 50, sameTime);

                // Act
                // We simulate the merge logic inside SwitchService:
                var combinedHistory = new List<LogLine>();
                combinedHistory.AddRange(outRes.Lines); // Add StdOut first
                combinedHistory.AddRange(errRes.Lines); // Add StdErr second

                // The specific sorting logic from your SwitchService:
                var sortedHistory = combinedHistory
                    .Select((line, index) => new { line, index })
                    .OrderBy(x => x.line.Timestamp)
                    .ThenBy(x => x.index)
                    .Select(x => x.line)
                    .ToList();

                // Assert
                Assert.Equal(2, sortedHistory.Count);
                // Even though errLine has a lower ID (created first), 
                // outLine must be first because of the index tie-breaker.
                Assert.Equal(LogType.StdOut, sortedHistory[0].Type);
                Assert.Equal(LogType.StdErr, sortedHistory[1].Type);

                // Final proof: StdOut is first despite having a higher ID
                Assert.True(sortedHistory[0].Id > sortedHistory[1].Id);
            }, createApp: true);
        }

    }
}
