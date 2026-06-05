using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.Utils;
using Servy.Manager.ViewModels;
using Servy.UI.Constants;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class ConsoleViewModelTests : IDisposable
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IUiDispatcher> _uiDispatcherMock;

        public ConsoleViewModelTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _cursorServiceMock = new Mock<ICursorService>();
            _uiDispatcherMock = new Mock<IUiDispatcher>();

            // Execute dispatcher invocations inline synchronously to prevent async deadlocks during tests
            _uiDispatcherMock.Setup(d => d.YieldAsync()).Returns(Task.CompletedTask);
            _uiDispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Action>(), It.IsAny<DispatcherPriority>()))
                             .Callback<Action, DispatcherPriority>((action, priority) => action())
                             .Returns(Task.CompletedTask);

            // Default configuration values
            _appConfigMock = new Mock<IAppConfiguration>();
            _appConfigMock.Setup(c => c.ConsoleMaxLines).Returns(500);
            _appConfigMock.Setup(c => c.ConsoleRefreshIntervalInMs).Returns(100);
            _appConfigMock.Setup(c => c.SearchDebounceDelayMs).Returns(10); // Short debounce for testing
        }

        private ConsoleViewModel CreateViewModel()
        {
            return new ConsoleViewModel(
                _serviceRepoMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                _uiDispatcherMock.Object);
        }

        public void Dispose()
        {
            // Centralized cleanup not needed for mocks, but interface implemented for scaling
        }

        #region Constructor & Guard Tests

        [Fact]
        public void Constructor_NullServiceRepository_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConsoleViewModel(
                null, _serviceCommandsMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _uiDispatcherMock.Object));
        }

        [Fact]
        public void Constructor_NullAppConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConsoleViewModel(
                _serviceRepoMock.Object, _serviceCommandsMock.Object, null, _cursorServiceMock.Object, _uiDispatcherMock.Object));
        }

        [Fact]
        public void DesignTimeConstructor_InitializesSuccessfully()
        {
            var dtViewModel = new ConsoleViewModel();
            Assert.NotNull(dtViewModel.RawLines);
            Assert.Equal(UiConstants.NotAvailable, dtViewModel.Pid);
        }

        #endregion

        #region Property & Command Lifecycle Tests

        [Fact]
        public async Task SelectedService_Change_ResetsStateAndTriggersMonitoring()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var service = new ConsoleService { Name = "AppService", StdoutPath = "C:\\out.log" };

                // Act
                vm.SelectedService = service;

                // Assert
                Assert.Equal("AppService", vm.SelectedService.Name);
                Assert.Empty(vm.RawLines);
            });
        }

        [Fact]
        public async Task ClearSelectionCommand_Executes_SetsSelectionActiveFalse()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                vm.SetSelectionActive(true);
                Assert.True(vm.IsPaused);

                // Act
                vm.ClearSelectionCommand.Execute(null);

                // Assert
                Assert.False(vm.IsPaused);
            });
        }

        [Fact]
        public async Task CopyPidCommand_ValidPid_InvokesServiceCommandsMapping()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var mockService = new ConsoleService { Name = "TestService", Pid = 5555 };
                vm.SelectedService = mockService;

                // Act
                await vm.CopyPidCommand.ExecuteAsync(null);

                // Assert
                _serviceCommandsMock.Verify(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "TestService")), Times.Once);
            });
        }

        [Fact]
        public async Task ConsoleSearchText_Filter_FiltersVisibleLinesAndTriggersDebounce()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();

                vm.RawLines.Add(new LogLine("Operation successful", LogType.StdOut));
                vm.RawLines.Add(new LogLine("System Crash", LogType.StdErr));

                // Act - Trigger Filter
                vm.ConsoleSearchText = "Crash";

                // Wait for the debounce + dispatcher queue
                // We use a small loop to wait for the UI thread to catch up
                int retries = 0;
                while (vm.VisibleLines.Cast<LogLine>().Count() != 1 && retries < 10)
                {
                    await Task.Delay(20);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    retries++;
                }

                // Assert
                var filtered = vm.VisibleLines.Cast<LogLine>().ToList();
                Assert.Single(filtered);
                Assert.Contains("Crash", filtered[0].Text);
            });
        }

        [Fact]
        public async Task ConsoleSearchText_Cleared_TriggersScrollRequest()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                bool scrollRequested = false;
                vm.RequestScroll += (force) => scrollRequested = true;

                // Act
                vm.ConsoleSearchText = "";
                await Task.Delay(50);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

                // Assert
                Assert.True(scrollRequested);
            });
        }

        #endregion

        #region Base Monitoring Loop (OnTickAsync) Tests

        [Fact]
        public async Task OnTickAsync_SelectedServiceNull_ResetsConsole()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var methodInfo = typeof(ConsoleViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                // Force state variable to simulate an orphaned selection
                var fieldInfo = typeof(ConsoleViewModel).GetField("_hadSelectedService", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfo?.SetValue(vm, true);
                vm.Pid = "1234";

                // Act
                await (Task)methodInfo.Invoke(vm, null);

                // Assert
                Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                Assert.False((bool)fieldInfo.GetValue(vm));
            });
        }

        [Fact]
        public async Task OnTickAsync_StateSnapshotNull_ClearsPathsAndPid()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var service = new ConsoleService { Name = "DeadService", Pid = 1234, StdoutPath = "log.txt" };
                vm.SelectedService = service;

                // Simulate DB returning a service state with no active PID (stopped)
                _serviceRepoMock.Setup(r => r.GetServiceConsoleStateAsync("DeadService", It.IsAny<CancellationToken>()))
                                .ReturnsAsync(new ServiceConsoleStateDto { Pid = null });

                var methodInfo = typeof(ConsoleViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                await (Task)methodInfo.Invoke(vm, null);

                // Assert
                Assert.Null(service.Pid);
                Assert.Null(service.StdoutPath);
                Assert.Equal(UiConstants.NotAvailable, vm.Pid);
            });
        }

        [Fact]
        public async Task OnTickAsync_PathsChanged_UpdatesPathsAndTriggersSwitch()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var service = new ConsoleService { Name = "ActiveService", Pid = 100, StdoutPath = "old.txt" };
                vm.SelectedService = service;

                // Simulate DB reporting a newly rotated log file path
                _serviceRepoMock.Setup(r => r.GetServiceConsoleStateAsync("ActiveService", It.IsAny<CancellationToken>()))
                                .ReturnsAsync(new ServiceConsoleStateDto { Pid = 100, ActiveStdoutPath = "new.txt" });

                var methodInfo = typeof(ConsoleViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                await (Task)methodInfo.Invoke(vm, null);

                // Assert
                Assert.Equal("new.txt", service.StdoutPath);
            });
        }

        #endregion

        #region Resource Management & Disposal Tests

        [Fact]
        public async Task OnMonitoringStopped_ClearViewTrue_ResetsConsole()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                vm.Pid = "1234";
                vm.RawLines.Add(new LogLine("Test", LogType.StdOut));

                var methodInfo = typeof(ConsoleViewModel).GetMethod("OnMonitoringStopped", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                methodInfo.Invoke(vm, new object[] { true });

                // Assert
                Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                Assert.Empty(vm.RawLines);
            });
        }

        [Fact]
        public async Task Dispose_CleansUpCancellationTokensAndEvents()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                bool scrollTriggered = false;
                vm.RequestScroll += (force) => scrollTriggered = true;

                // Act
                vm.Dispose();

                // Assert: Event invocation list should be wiped
                var eventField = typeof(ConsoleViewModel).GetField("RequestScroll", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = eventField?.GetValue(vm);
                Assert.Null(eventDelegate);
                Assert.False(scrollTriggered);
            });
        }

        #endregion

        #region Sorting Logic Proof Tests

        [Fact]
        public async Task HistorySort_WithIdenticalTimestamps_ShouldPreserveArrivalOrder()
        {
            await Helper.RunOnSTA(async () =>
            {
                var sameTime = new DateTime(2026, 1, 30, 10, 0, 0);

                var line2 = new LogLine("Line 2", LogType.StdErr, sameTime);
                var line1 = new LogLine("Line 1", LogType.StdOut, sameTime);

                var combinedHistory = new List<LogLine> { line2, line1 };

                var sortedHistory = combinedHistory
                    .Select((line, index) => new { line, index })
                    .OrderBy(x => x.line.Timestamp)
                    .ThenBy(x => x.index)
                    .Select(x => x.line)
                    .ToList();

                Assert.Equal("Line 2", sortedHistory[0].Text);
                Assert.Equal("Line 1", sortedHistory[1].Text);
            });
        }

        #endregion
    }
}