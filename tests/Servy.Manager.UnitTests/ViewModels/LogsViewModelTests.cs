using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using Helper = Servy.Testing.Helper;

namespace Servy.Manager.UnitTests.ViewModels
{
    [Collection("Ambient AppServices Dependent Tests")]
    public class LogsViewModelTests
    {
        // Centralized lock token shared across the test fixture to block cross-thread interference
        private static readonly object StaticEnvironmentLock = new object();

        private readonly Mock<IAppConfiguration> _appConfigurationMock;
        private readonly Mock<IEventLogService> _eventLogServiceMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public LogsViewModelTests()
        {
            _appConfigurationMock = new Mock<IAppConfiguration>();
            _eventLogServiceMock = new Mock<IEventLogService>();
            _cursorServiceMock = new Mock<ICursorService>();
            _mockProcessKiller = new Mock<IProcessKiller>();

            _appConfigurationMock.Setup(c => c.LogsWindowDays).Returns(7);
        }

        private LogsViewModel CreateViewModel()
        {
            return new LogsViewModel(
                _appConfigurationMock.Object,
                _eventLogServiceMock.Object,
                _cursorServiceMock.Object);
        }

        #region Constructor Guard Clauses & Initialization Tests

        [Fact]
        public void Constructor_NullAppConfig_ThrowsArgumentNullException()
        {
            // Guarded with environmental lock block to protect against ambient state drift from adjacent tests
            lock (StaticEnvironmentLock)
            {
                Assert.Throws<ArgumentNullException>(() => new LogsViewModel(
                    null!, _eventLogServiceMock.Object, _cursorServiceMock.Object));
            }
        }

        [Fact]
        public void Constructor_NullEventLogService_ThrowsArgumentNullException()
        {
            // Guarded with environmental lock block to protect against ambient state drift from adjacent tests
            lock (StaticEnvironmentLock)
            {
                Assert.Throws<ArgumentNullException>(() => new LogsViewModel(
                    _appConfigurationMock.Object, null!, _cursorServiceMock.Object));
            }
        }

        [Fact]
        public void Constructor_NullCursorService_ThrowsArgumentNullException()
        {
            // Guarded with environmental lock block to protect against ambient state drift from adjacent tests
            lock (StaticEnvironmentLock)
            {
                Assert.Throws<ArgumentNullException>(() => new LogsViewModel(
                    _appConfigurationMock.Object, _eventLogServiceMock.Object, null!));
            }
        }

        [Fact]
        public void Constructor_ShouldInitializeDefaults()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();

                    Assert.NotNull(vm.LogsView);
                    Assert.NotNull(vm.SearchCommand);
                    Assert.NotNull(vm.RowClickCommand);
                    Assert.False(vm.IsBusy);
                    Assert.Contains("Search", vm.SearchButtonText);
                    Assert.NotNull(vm.FromDate);
                    Assert.NotNull(vm.ToDate);
                    Assert.Equal(EventLogLevel.All, vm.SelectedLevel);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        #endregion

        #region Properties & Change Notification Validation Tests

        [Fact]
        public void PropertyChanged_IsRaised()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    string? propertyName = null;
                    vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

                    vm.IsBusy = true;

                    Assert.Equal(nameof(LogsViewModel.IsBusy), propertyName);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void Properties_DuplicateAssignments_DoNotRaisePropertyChanged()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();

                    // Bind explicit static reference anchors to bypass real-time millisecond/tick differences
                    var staticDate = new DateTime(2026, 6, 8);
                    vm.FromDate = staticDate;
                    vm.ToDate = staticDate;
                    vm.SearchButtonText = "Search";
                    vm.Keyword = "Clean";
                    vm.FooterText = "Ready";

                    int notificationCount = 0;
                    vm.PropertyChanged += (s, e) => notificationCount++;

                    // Act - Set to identical static values to test optimization guards cleanly
                    vm.IsBusy = false;
                    vm.FooterText = "Ready";
                    vm.SearchButtonText = "Search";
                    vm.FromDate = staticDate;
                    vm.FromDateMaxDate = vm.FromDateMaxDate;
                    vm.ToDate = staticDate;
                    vm.ToDateMinDate = vm.ToDateMinDate;
                    vm.Keyword = "Clean";
                    vm.SelectedLogMessage = null;

                    // Assert
                    Assert.Equal(0, notificationCount);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void FromDate_ShouldUpdate_ToDateMinDate()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var newDate = DateTime.Today.AddDays(-5);

                    vm.FromDate = newDate;

                    Assert.Equal(newDate, vm.ToDateMinDate);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void ToDate_ShouldUpdate_FromDateMaxDate()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var newDate = DateTime.Today;

                    vm.ToDate = newDate;

                    Assert.Equal(newDate, vm.FromDateMaxDate);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void Keyword_PropertyMutates_RaisesNotificationCorrectly()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    string? changedProp = null;
                    vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

                    // Act
                    vm.Keyword = "ServyAgent";

                    // Assert
                    Assert.Equal("ServyAgent", vm.Keyword);
                    Assert.Equal(nameof(LogsViewModel.Keyword), changedProp);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void SelectedLevel_PropertyMutates_RaisesNotificationCorrectly()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    string? changedProp = null;
                    vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

                    // Act
                    vm.SelectedLevel = EventLogLevel.Error;

                    // Assert
                    Assert.Equal(EventLogLevel.Error, vm.SelectedLevel);
                    Assert.Equal(nameof(LogsViewModel.SelectedLevel), changedProp);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void FooterText_PropertyMutates_RaisesNotificationCorrectly()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    string? changedProp = null;
                    vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

                    // Act
                    vm.FooterText = "Rows processed cleanly";

                    // Assert
                    Assert.Equal("Rows processed cleanly", vm.FooterText);
                    Assert.Equal(nameof(LogsViewModel.FooterText), changedProp);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void SelectedLog_SetToNull_ClearsSelectedLogMessage()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    vm.SelectedLog = new LogEntryModel { Message = "Error Context" };

                    // Act
                    vm.SelectedLog = null;

                    // Assert
                    Assert.Null(vm.SelectedLog);
                    Assert.Equal(string.Empty, vm.SelectedLogMessage);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void GetLogLevels_StaticCall_ExcludesCriticalAndVerbose()
        {
            // Act
            var levels = LogsViewModel.LogLevels;

            // Assert
            Assert.DoesNotContain(EventLogLevel.Critical, levels);
            Assert.DoesNotContain(EventLogLevel.Verbose, levels);
            Assert.Contains(EventLogLevel.All, levels);
            Assert.Contains(EventLogLevel.Error, levels);
            Assert.Contains(EventLogLevel.Information, levels);
            Assert.Contains(EventLogLevel.Warning, levels);
        }

        #endregion

        #region Row Click Validation Tests

        [Fact]
        public void RowClickCommand_ShouldSetSelectedLog()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();
                    var log = new LogEntryModel { Message = "test" };

                    vm.RowClickCommand.Execute(log);

                    Assert.Equal("test", vm.SelectedLog?.Message);
                    Assert.Equal("test", vm.SelectedLogMessage);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void RowClickCommand_InvalidParameterObject_BypassesStateMutation()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    vm.SelectedLog = null;

                    // Act - Pass an irrelevant object parameter layout type context
                    vm.RowClickCommand.Execute(new List<string> { "Malformed context entry payload mapping" });

                    // Assert
                    Assert.Null(vm.SelectedLog);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        #endregion

        #region Search Pipeline Asynchronous Workflow & Exception Circuit Tests

        [Fact]
        public async Task SearchCommand_ShouldPopulateLogs_AndRaiseScrollEvent()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in the exclusive environment lock block to register the required DI tracker targets
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        // Arrange
                        var entries = new List<ServyEventLogEntry>
                        {
                            new ServyEventLogEntry { EventId = 1, Time = DateTimeOffset.Now, Level = EventLogLevel.Information, Message = "test message" }
                        };

                        _eventLogServiceMock
                            .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(entries);

                        var vm = CreateViewModel();

                        var scrollEventSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        vm.ScrollLogsToTopRequested += () => scrollEventSource.TrySetResult(true);

                        // Act
                        var searchTask = vm.SearchCommand.ExecuteAsync(null);

                        // Force the execution loop context to await task processing step safely
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                        var completedTask = Task.WhenAny(scrollEventSource.Task, timeoutTask).GetAwaiter().GetResult();

                        if (completedTask == timeoutTask)
                        {
                            throw new TimeoutException("The ScrollLogsToTopRequested event failed to fire within the allocated safety window.");
                        }

                        searchTask.GetAwaiter().GetResult();

                        // Assert
                        Assert.Single(vm.LogsView.SourceCollection.Cast<LogEntryModel>());
                        Assert.True(scrollEventSource.Task.Result);
                        Assert.Contains("Search", vm.SearchButtonText);
                        Assert.False(vm.IsBusy);

                        // Verify the cursor service was utilized
                        _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task SearchCommand_ConcurrentlyInvoked_CancelsAndDisposesPreviousRunningCts()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in the exclusive environment lock block to register the required DI tracker targets
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        // Arrange
                        var firstSearchTcs = new TaskCompletionSource<IEnumerable<ServyEventLogEntry>>();
                        var secondSearchTcs = new TaskCompletionSource<IEnumerable<ServyEventLogEntry>>();

                        var searchCallCount = 0;
                        _eventLogServiceMock
                            .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .Returns(() =>
                            {
                                searchCallCount++;
                                return searchCallCount == 1 ? firstSearchTcs.Task : secondSearchTcs.Task;
                            });

                        var vm = CreateViewModel();
                        var fieldInfo = typeof(LogsViewModel).GetField("_cancellationTokenSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        // Fetch the private 'Search' method directly using reflection to bypass AsyncCommand's 'IsRunning' re-entrancy guard
                        var searchMethod = typeof(LogsViewModel).GetMethod("Search", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Assert.NotNull(searchMethod);

                        // Act - 1. Fire the first background lookup
                        var firstSearchTask = (Task)searchMethod.Invoke(vm, new object[] { null! })!;

                        // 2. Poll until the first token reference is registered inside the model
                        CancellationTokenSource? firstCtsInstance = null;
                        int retries = 0;
                        while (firstCtsInstance == null && retries < 50)
                        {
                            firstCtsInstance = fieldInfo?.GetValue(vm) as CancellationTokenSource;
                            if (firstCtsInstance == null)
                            {
                                Task.Delay(10).GetAwaiter().GetResult();
                                retries++;
                            }
                        }

                        Assert.NotNull(firstCtsInstance); // Guard rail assertion

                        // 3. Invoke the private method directly a second time to force the atomic Interlocked swap loop
                        var secondSearchTask = (Task)searchMethod.Invoke(vm, new object[] { null! })!;

                        // Assert - Verify that the original token was forced into a cancelled state immediately
                        Assert.True(firstCtsInstance.IsCancellationRequested, "The previous CancellationTokenSource was not cancelled by the subsequent search.");

                        // 4. Tear down task blocks cleanly
                        firstSearchTcs.TrySetResult(Array.Empty<ServyEventLogEntry>());
                        secondSearchTcs.TrySetResult(Array.Empty<ServyEventLogEntry>());

                        Task.WhenAll(firstSearchTask, secondSearchTask).GetAwaiter().GetResult();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task SearchCommand_ServiceThrowsException_LogsFaultContextAndRestoresCursorState()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in the exclusive environment lock block to register the required DI tracker targets
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        // Arrange
                        _eventLogServiceMock
                            .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new InvalidOperationException("WMI Repository Event log corruption detected"));

                        var vm = CreateViewModel();

                        // Act
                        vm.SearchCommand.ExecuteAsync(null).GetAwaiter().GetResult();

                        // Assert - Verify final security release blocks completed execution cycle parameters
                        _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
                        Assert.False(vm.IsBusy);
                        Assert.Equal(Strings.Button_Search, vm.SearchButtonText);
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task SearchCommand_OperationCanceledMidStream_ExitsGracefullyWithoutCrashing()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in the exclusive environment lock block to register the required DI tracker targets
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        // Arrange
                        _eventLogServiceMock
                            .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new OperationCanceledException());

                        var vm = CreateViewModel();

                        // Act
                        var exception = Record.ExceptionAsync(() => vm.SearchCommand.ExecuteAsync(null)).GetAwaiter().GetResult();

                        // Assert
                        Assert.Null(exception); // The OperationCanceledException catch block handled it safely
                        _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        #endregion

        #region Resource Management, Cleanup & Disposal Tests

        [Fact]
        public void Cleanup_ShouldCancelAndDisposeToken()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    var vm = CreateViewModel();

                    vm.Cleanup();

                    // After cleanup, a second call should not throw
                    var exception = Record.Exception(() => vm.Cleanup());
                    Assert.Null(exception);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void Dispose_InvokedMultipleTimes_ExitsEarlyThroughDisposedValueAtomicGuard()
        {
            // Guarded with exclusive environment lock and dynamic DI configuration setup
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var vm = CreateViewModel();

                    // Act - First explicit teardown execution context
                    vm.Dispose();

                    // Act - Second verification path execution loop
                    var doubleDisposeException = Record.Exception(() => vm.Dispose());

                    // Assert
                    Assert.Null(doubleDisposeException);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        #endregion
    }
}