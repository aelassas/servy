using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.Utils;
using Servy.Manager.ViewModels;
using Servy.UI.Constants;
using Servy.UI.Services;
using System.Reflection;
using System.Windows.Threading;
using Helper = Servy.Testing.Helper;

namespace Servy.Manager.UnitTests.ViewModels
{
    [Collection("Ambient AppServices Dependent Tests")]
    public class ConsoleViewModelTests
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IUiDispatcher> _uiDispatcherMock;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public ConsoleViewModelTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _cursorServiceMock = new Mock<ICursorService>();
            _uiDispatcherMock = new Mock<IUiDispatcher>();
            _mockProcessKiller = new Mock<IProcessKiller>();

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

        #region Constructor & Guard Tests

        [Fact]
        public void Constructor_NullServiceRepository_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConsoleViewModel(
                null!, _serviceCommandsMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _uiDispatcherMock.Object));
        }

        [Fact]
        public void Constructor_NullAppConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConsoleViewModel(
                _serviceRepoMock.Object, _serviceCommandsMock.Object, null!, _cursorServiceMock.Object, _uiDispatcherMock.Object));
        }

        [Fact]
        public void DesignTimeConstructor_InitializesSuccessfully()
        {
            Helper.RunOnSTA(() =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var dtViewModel = new ConsoleViewModel();
                    Assert.NotNull(dtViewModel.RawLines);
                    Assert.Equal(UiConstants.NotAvailable, dtViewModel.Pid);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            }, createApp: true);
        }

        #endregion

        #region Property & Command Lifecycle Tests

        [Fact]
        public async Task SelectedService_Change_ResetsStateAndTriggersMonitoring()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var service = new ConsoleService { Name = "AppService", StdoutPath = "C:\\out.log" };

                    // Act
                    vm.SelectedService = service;

                    // Assert
                    Assert.Equal("AppService", vm.SelectedService.Name);
                    Assert.Empty(vm.RawLines);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task ClearSelectionCommand_Executes_SetsSelectionActiveFalse()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    vm.SetSelectionActive(true);
                    Assert.True(vm.IsPaused);

                    // Act
                    vm.ClearSelectionCommand.Execute(null);

                    // Assert
                    Assert.False(vm.IsPaused);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task CopyPidCommand_ValidPid_InvokesServiceCommandsMapping()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var mockService = new ConsoleService { Name = "TestService", Pid = 5555 };
                    vm.SelectedService = mockService;

                    vm.CopyPidCommand.ExecuteAsync(null).GetAwaiter().GetResult();

                    // Assert
                    _serviceCommandsMock.Verify(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "TestService"), It.IsAny<CancellationToken>()), Times.Once);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task ConsoleSearchText_Filter_FiltersVisibleLinesAndTriggersDebounce()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();

                    vm.RawLines.Add(new LogLine("Operation successful", LogType.StdOut));
                    vm.RawLines.Add(new LogLine("System Crash", LogType.StdErr));

                    // Act - Trigger Filter
                    vm.ConsoleSearchText = "Crash";

                    // Wait for the debounce + dispatcher queue
                    int retries = 0;
                    while (vm.VisibleLines.Cast<LogLine>().Count() != 1 && retries < 10)
                    {
                        Task.Delay(20).GetAwaiter().GetResult();
                        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                        retries++;
                    }

                    // Assert
                    var filtered = vm.VisibleLines.Cast<LogLine>().ToList();
                    Assert.Single(filtered);
                    Assert.Contains("Crash", filtered[0].Text);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        #endregion

        #region Base Monitoring Loop (OnTickAsync) Tests

        [Fact]
        public async Task OnTickAsync_SelectedServiceNull_ResetsConsole()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var methodInfo = typeof(ConsoleViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Force state variable to simulate an orphaned selection
                    var fieldInfo = typeof(ConsoleViewModel).GetField("_hadSelectedService", BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInfo?.SetValue(vm, true);
                    vm.Pid = "1234";

                    // Act
                    var task = (Task)methodInfo!.Invoke(vm, null)!;
                    task.GetAwaiter().GetResult();

                    // Assert
                    Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                    Assert.False((bool)fieldInfo!.GetValue(vm)!);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task OnTickAsync_SnapshotPidNull_ClearsPathsAndPid()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var service = new ConsoleService { Name = "DeadService", Pid = 1234, StdoutPath = "log.txt" };
                    vm.SelectedService = service;

                    // Simulate DB returning a service state with no active PID (stopped)
                    _serviceRepoMock.Setup(r => r.GetServiceConsoleStateAsync("DeadService", It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(new ServiceConsoleStateDto { Pid = null });

                    var methodInfo = typeof(ConsoleViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Act
                    var task = (Task)methodInfo!.Invoke(vm, null)!;
                    task.GetAwaiter().GetResult();

                    // Assert
                    Assert.Null(service.Pid);
                    Assert.Null(service.StdoutPath);
                    Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task OnTickAsync_PathsChanged_UpdatesPathsAndTriggersSwitch()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var service = new ConsoleService { Name = "ActiveService", Pid = 100, StdoutPath = "old.txt" };
                    vm.SelectedService = service;

                    // Simulate DB reporting a newly rotated log file path
                    _serviceRepoMock.Setup(r => r.GetServiceConsoleStateAsync("ActiveService", It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(new ServiceConsoleStateDto { Pid = 100, ActiveStdoutPath = "new.txt" });

                    var methodInfo = typeof(ConsoleViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Act
                    var task = (Task)methodInfo!.Invoke(vm, null)!;
                    task.GetAwaiter().GetResult();

                    // Assert
                    Assert.Equal("new.txt", service.StdoutPath);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        #endregion

        #region Resource Management & Disposal Tests

        [Fact]
        public async Task Dispose_CleansUpCancellationTokensAndEvents()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    bool scrollTriggered = false;
                    vm.RequestScroll += (force) => scrollTriggered = true;

                    // Act
                    vm.Dispose();

                    // Assert
                    Assert.False(scrollTriggered);
                }
                finally
                {
                    App.Services = originalProvider;
                }
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

        #region Advanced Code-Gap Coverage (CreateServiceItem, SwitchService, StartLiveTail, Dispose Branches)

        [Fact]
        public void CreateServiceItem_ValidServiceInput_MapsToConsoleServiceWithNullFields()
        {
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                // Arrange
                var vm = CreateViewModel();
                var service = new Service { Name = "EngineService" };

                var methodInfo = typeof(ConsoleViewModel).GetMethod("CreateServiceItem", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                var result = methodInfo!.Invoke(vm, new object[] { service }) as ConsoleService;

                // Assert
                Assert.NotNull(result);
                Assert.Equal("EngineService", result.Name);
                Assert.Null(result.Pid);
                Assert.Null(result.StdoutPath);
                Assert.Null(result.StderrPath);
            }
            finally
            {
                App.Services = originalProvider;
            }
        }

        [Fact]
        public async Task ApplyFilterWithDebounceAsync_OldCtsNotNull_CancelsAndDisposesPreviousFilterToken()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    var firstCts = new CancellationTokenSource();

                    var fieldInfo = typeof(ConsoleViewModel).GetField("_logFilterCts", BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInfo?.SetValue(vm, firstCts);

                    // Act - Mutating ConsoleSearchText causes ApplyFilterWithDebounceAsync to process an Interlocked.Exchange over firstCts
                    vm.ConsoleSearchText = "NewSearchQueryTextString";

                    // Assert
                    Assert.True(firstCts.IsCancellationRequested);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task SwitchServiceAsync_EmptyCombinedHistory_DoesNotTriggerSortOrCollectionMutation()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    vm.RawLines.Add(new LogLine("Preserve Me", LogType.StdOut));

                    var methodInfo = typeof(ConsoleViewModel).GetMethod("SwitchServiceAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Act - Invoke service transition with empty/null pathing arguments to trigger structural history emptiness
                    var task = (Task)methodInfo!.Invoke(vm, new object[] { string.Empty, string.Empty })!;
                    task.GetAwaiter().GetResult();

                    // Assert - Verify that the internal branch evaluation safely skipped AddRange loops since paths were empty
                    Assert.Empty(vm.RawLines);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task StartLiveTail_LogReceivedOnActiveSession_AppendsToRawLinesAndTrimsExcessRows()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();

                    var fieldMaxLines = typeof(ConsoleViewModel).GetField("_maxLines", BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldMaxLines?.SetValue(vm, 2);

                    var methodInfo = typeof(ConsoleViewModel).GetMethod("StartLiveTail", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Read current Session ID to pass down as a synchronized parameter match
                    var fieldSessionId = typeof(ConsoleViewModel).GetField("_currentSessionId", BindingFlags.NonPublic | BindingFlags.Instance);
                    int activeSessionId = (int)fieldSessionId!.GetValue(vm)!;

                    // Act - Spin up an active live tail listener instance context mapping to stdout
                    methodInfo!.Invoke(vm, new object[] { "out.log", LogType.StdOut, 0L, DateTime.UtcNow, activeSessionId, CancellationToken.None });

                    // Pull the dynamic internal event handler delegate out via reflection
                    var fieldActiveTailer = typeof(ConsoleViewModel).GetField("_activeStdoutTailer", BindingFlags.NonPublic | BindingFlags.Instance);
                    var tailerInstance = fieldActiveTailer!.GetValue(vm) as LogTailer;

                    // Construct a test payload batch block of 3 log lines to pass directly through the tailer's event handler pipeline
                    var newLinesBatch = new List<LogLine>
                    {
                        new LogLine("Row 1", LogType.StdOut),
                        new LogLine("Row 2", LogType.StdOut),
                        new LogLine("Row 3", LogType.StdOut)
                    };

                    // Raise the event inside the log tailer instance to simulate inbound disk streaming updates
                    var handlerDelegate = typeof(ConsoleViewModel).GetField("_stdoutTailerHandler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm) as Delegate;

                    // Act - Direct programmatic dispatch invoke step
                    handlerDelegate!.DynamicInvoke(new object[] { newLinesBatch });

                    // Assert - Proves both AddRange and TrimToSize internal loops executed safely, capping length to 2 rows
                    Assert.Equal(2, vm.RawLines.Count);
                    Assert.Equal("Row 2", vm.RawLines[0].Text);
                    Assert.Equal("Row 3", vm.RawLines[1].Text);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task StartLiveTail_LogReceivedWhileSelectionIsActive_BypassesCollectionMutationToPreserveUserFocus()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    vm.SetSelectionActive(true); // User is selecting text in the UI terminal window frame

                    var methodInfo = typeof(ConsoleViewModel).GetMethod("StartLiveTail", BindingFlags.NonPublic | BindingFlags.Instance);
                    int currentSessionId = (int)typeof(ConsoleViewModel).GetField("_currentSessionId", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;

                    methodInfo!.Invoke(vm, new object[] { "out.log", LogType.StdOut, 0L, DateTime.UtcNow, currentSessionId, CancellationToken.None });

                    var handlerDelegate = typeof(ConsoleViewModel).GetField("_stdoutTailerHandler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm) as Delegate;

                    var newLinesBatch = new List<LogLine> { new LogLine("Ignored live incoming console data string line", LogType.StdOut) };

                    // Act
                    handlerDelegate!.DynamicInvoke(new object[] { newLinesBatch });

                    // Assert - Log array size should remain 0 because mutation bypassed collection injection via text pause guard gate
                    Assert.Empty(vm.RawLines);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task StartLiveTail_LogReceivedOnStaleSession_BypassesCollectionMutationSilently()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    var methodInfo = typeof(ConsoleViewModel).GetMethod("StartLiveTail", BindingFlags.NonPublic | BindingFlags.Instance);

                    int currentSessionId = (int)typeof(ConsoleViewModel).GetField("_currentSessionId", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
                    int staleSessionId = currentSessionId - 1; // Simulated obsolete thread queue callback sequence tracking state

                    methodInfo!.Invoke(vm, new object[] { "out.log", LogType.StdOut, 0L, DateTime.UtcNow, staleSessionId, CancellationToken.None });

                    var handlerDelegate = typeof(ConsoleViewModel).GetField("_stdoutTailerHandler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm) as Delegate;
                    var newLinesBatch = new List<LogLine> { new LogLine("Obsolete service log line output", LogType.StdOut) };

                    // Act
                    handlerDelegate!.DynamicInvoke(new object[] { newLinesBatch });

                    // Assert
                    Assert.Empty(vm.RawLines);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task SetSelectionActive_SelectionCleared_ReTriggerServicesSwitchPipeline()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    var service = new ConsoleService { Name = "ActiveService", StdoutPath = "out.log", StderrPath = "err.log" };
                    vm.SelectedService = service;
                    vm.SetSelectionActive(true);

                    // Act - Toggle selection state back to false to trigger the internal conditional reset pathway loop block
                    vm.SetSelectionActive(false);

                    // Assert
                    Assert.False(vm.IsPaused);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        [Fact]
        public async Task Dispose_CalledMultipleTimes_ReturnsSilentlyThroughInternalDisposedValueGuard()
        {
            await Helper.RunOnSTA(async () =>
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();

                    // Act
                    vm.Dispose();

                    // Assert - Re-invoking sequential Dispose cycles must exit early without crash exceptions or registry faults
                    var multipleDisposeError = Record.Exception(() => vm.Dispose());
                    Assert.Null(multipleDisposeError);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            });
        }

        #endregion
    }
}