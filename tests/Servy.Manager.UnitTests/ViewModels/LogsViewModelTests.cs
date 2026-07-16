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
using Servy.Testing;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Helper = Servy.Testing.Helper;

namespace Servy.Manager.UnitTests.ViewModels
{
    [Collection("Ambient AppServices Dependent Tests")]
    public class LogsViewModelTests
    {
        private readonly Mock<IAppConfiguration> _appConfigurationMock;
        private readonly Mock<IEventLogService> _eventLogServiceMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly Mock<IMessageBoxService> _mockMessageBoxService;

        public LogsViewModelTests()
        {
            _appConfigurationMock = new Mock<IAppConfiguration>();
            _eventLogServiceMock = new Mock<IEventLogService>();
            _cursorServiceMock = new Mock<ICursorService>();
            _mockProcessKiller = new Mock<IProcessKiller>();
            _mockMessageBoxService = new Mock<IMessageBoxService>();

            _appConfigurationMock.Setup(c => c.LogsWindowDays).Returns(7);
        }

        private LogsViewModel CreateViewModel()
        {
            return new LogsViewModel(
                _appConfigurationMock.Object,
                _eventLogServiceMock.Object,
                _cursorServiceMock.Object,
                _mockMessageBoxService.Object);
        }

        #region Constructor Guard Clauses & Initialization Tests

        [Fact]
        public void Constructor_NullAppConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LogsViewModel(
                null, _eventLogServiceMock.Object, _cursorServiceMock.Object, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullEventLogService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LogsViewModel(
                _appConfigurationMock.Object, null, _cursorServiceMock.Object, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullCursorService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LogsViewModel(
                _appConfigurationMock.Object, _eventLogServiceMock.Object, null, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullMessageBoxService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LogsViewModel(
                _appConfigurationMock.Object, _eventLogServiceMock.Object, _cursorServiceMock.Object, null));
        }

        [Fact]
        public void Constructor_ShouldInitializeDefaults()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Assert
                Assert.NotNull(vm.LogsView);
                Assert.NotNull(vm.SearchCommand);
                Assert.NotNull(vm.RowClickCommand);
                Assert.False(vm.IsBusy);
                Assert.Equal(Strings.Button_Search, vm.SearchButtonText);
                Assert.NotNull(vm.FromDate);
                Assert.NotNull(vm.ToDate);
                Assert.Equal(EventLogLevel.All, vm.SelectedLevel);
            }
        }

        #endregion

        #region Properties & Change Notification Validation Tests

        [Fact]
        public void PropertyChanged_IsRaised()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                string propertyName = null;
                vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

                // Act
                vm.IsBusy = true;

                // Assert
                Assert.Equal(nameof(LogsViewModel.IsBusy), propertyName);
            }
        }

        [Fact]
        public void Properties_DuplicateAssignments_DoNotRaisePropertyChanged()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                var staticDate = new DateTime(2026, 6, 8);
                vm.FromDate = staticDate;
                vm.ToDate = staticDate;
                vm.SearchButtonText = "Search";
                vm.Keyword = "Clean";
                vm.FooterText = "Ready";

                int notificationCount = 0;
                vm.PropertyChanged += (s, e) => notificationCount++;

                // Act
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
        }

        [Fact]
        public void FromDate_ShouldUpdate_ToDateMinDate()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                var newDate = DateTime.Today.AddDays(-5);

                // Act
                vm.FromDate = newDate;

                // Assert
                Assert.Equal(newDate, vm.ToDateMinDate);
            }
        }

        [Fact]
        public void ToDate_ShouldUpdate_FromDateMaxDate()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                var newDate = DateTime.Today;

                // Act
                vm.ToDate = newDate;

                // Assert
                Assert.Equal(newDate, vm.FromDateMaxDate);
            }
        }

        [Fact]
        public void Keyword_PropertyMutates_RaisesNotificationCorrectly()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                string changedProp = null;
                vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

                // Act
                vm.Keyword = "ServyAgent";

                // Assert
                Assert.Equal("ServyAgent", vm.Keyword);
                Assert.Equal(nameof(LogsViewModel.Keyword), changedProp);
            }
        }

        [Fact]
        public void SelectedLevel_PropertyMutates_RaisesNotificationCorrectly()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                string changedProp = null;
                vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

                // Act
                vm.SelectedLevel = EventLogLevel.Error;

                // Assert
                Assert.Equal(EventLogLevel.Error, vm.SelectedLevel);
                Assert.Equal(nameof(LogsViewModel.SelectedLevel), changedProp);
            }
        }

        [Fact]
        public void FooterText_PropertyMutates_RaisesNotificationCorrectly()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                string changedProp = null;
                vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

                // Act
                vm.FooterText = "Rows processed cleanly";

                // Assert
                Assert.Equal("Rows processed cleanly", vm.FooterText);
                Assert.Equal(nameof(LogsViewModel.FooterText), changedProp);
            }
        }

        [Fact]
        public void SelectedLog_SetToNull_ClearsSelectedLogMessage()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                vm.SelectedLog = new LogEntryModel { Message = "Error Context" };

                // Act
                vm.SelectedLog = null;

                // Assert
                Assert.Null(vm.SelectedLog);
                Assert.Equal(string.Empty, vm.SelectedLogMessage);
            }
        }

        [Fact]
        public void LogLevels_Get_ExcludesCriticalAndVerbose()
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
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                var log = new LogEntryModel { Message = "test" };

                // Act
                vm.RowClickCommand.Execute(log);

                // Assert
                Assert.Equal("test", vm.SelectedLog?.Message);
                Assert.Equal("test", vm.SelectedLogMessage);
            }
        }

        [Fact]
        public void RowClickCommand_InvalidParameterObject_BypassesStateMutation()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Arrange
                vm.SelectedLog = null;

                // Act
                vm.RowClickCommand.Execute(new List<string> { "Malformed context entry payload mapping" });

                // Assert
                Assert.Null(vm.SelectedLog);
            }
        }

        #endregion

        #region Search Pipeline Asynchronous Workflow & Exception Circuit Tests

        [Fact]
        public async Task SearchCommand_ShouldPopulateLogs_AndRaiseScrollEvent()
        {
            await Helper.RunOnSTA(async () =>
            {
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    // Arrange
                    var entries = new List<ServyEventLogEntry>
                    {
                        new ServyEventLogEntry { EventId = 1, Time = DateTimeOffset.Now, Level = EventLogLevel.Information, Message = "test message" }
                    };

                    _eventLogServiceMock
                        .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(entries);

                    using (var vm = CreateViewModel())
                    {
                        var scrollEventSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        vm.ScrollLogsToTopRequested += () => scrollEventSource.TrySetResult(true);

                        // Act
                        var searchTask = vm.SearchCommand.ExecuteAsync(null);

                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                        var completedTask = await Task.WhenAny(scrollEventSource.Task, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            throw new TimeoutException("The ScrollLogsToTopRequested event failed to fire within the allocated safety window.");
                        }

                        await searchTask;

                        // Assert
                        using (var enumerator = vm.LogsView.SourceCollection.Cast<LogEntryModel>().GetEnumerator())
                        {
                            Assert.True(enumerator.MoveNext());
                            Assert.False(enumerator.MoveNext());
                            Assert.True(scrollEventSource.Task.Result);
                            Assert.Equal(Strings.Button_Search, vm.SearchButtonText);
                            Assert.False(vm.IsBusy);

                            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task Search_PrivateMethodInvokedTwice_CancelsPreviousCts()
        {
            await Helper.RunOnSTA(async () =>
            {
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
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

                    // Act - 1. Fire the first background lookup
                    var firstSearchTask = (Task)TestReflection.InvokeNonPublic(vm, "Search", new object[] { null });

                    // 2. Poll until the first token reference is registered inside the model
                    await Helper.WaitUntilAsync(
                        () => TestReflection.GetField<CancellationTokenSource>(vm, "_searchCts") != null,
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromMilliseconds(10),
                        CancellationToken.None);

                    CancellationTokenSource firstCtsInstance = TestReflection.GetField<CancellationTokenSource>(vm, "_searchCts");
                    Assert.NotNull(firstCtsInstance); // Guard rail assertion

                    // 3. Invoke the private method directly a second time to force the atomic Interlocked swap loop
                    var secondSearchTask = (Task)TestReflection.InvokeNonPublic(vm, "Search", new object[] { null });

                    // Assert - Verify that the original token was forced into a cancelled state immediately
                    Assert.True(firstCtsInstance.IsCancellationRequested, "The previous CancellationTokenSource was not cancelled by the subsequent search.");
                    Assert.Throws<ObjectDisposedException>(() => _ = firstCtsInstance.Token);

                    // 4. Tear down task blocks cleanly
                    firstSearchTcs.TrySetResult(Array.Empty<ServyEventLogEntry>());
                    secondSearchTcs.TrySetResult(Array.Empty<ServyEventLogEntry>());

                    await Task.WhenAll(firstSearchTask, secondSearchTask);
                }
            }, createApp: true);
        }

        [Fact]
        public async Task SearchCommand_ServiceThrowsException_RestoresCursorState()
        {
            await Helper.RunOnSTA(async () =>
            {
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    // Arrange
                    _eventLogServiceMock
                        .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new InvalidOperationException("WMI Repository Event log corruption detected"));

                    using (var vm = CreateViewModel())
                    {
                        // Act
                        await vm.SearchCommand.ExecuteAsync(null);

                        // Assert
                        _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
                        Assert.False(vm.IsBusy);
                        Assert.Equal(Strings.Button_Search, vm.SearchButtonText);
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task SearchCommand_OperationCanceledMidStream_ExitsGracefullyWithoutCrashing()
        {
            await Helper.RunOnSTA(async () =>
            {
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    // Arrange
                    _eventLogServiceMock
                        .Setup(s => s.SearchAsync(It.IsAny<EventLogLevel?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new OperationCanceledException());

                    using (var vm = CreateViewModel())
                    {
                        // Act
                        var exception = await Record.ExceptionAsync(() => vm.SearchCommand.ExecuteAsync(null));

                        // Assert
                        Assert.Null(exception); // The OperationCanceledException catch block handled it safely
                        Assert.False(vm.IsBusy);
                        Assert.Equal(Strings.Button_Search, vm.SearchButtonText);
                        _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
                    }
                }
            }, createApp: true);
        }

        #endregion

        #region Resource Management, Cleanup & Disposal Tests

        [Fact]
        public void Cleanup_ShouldCancelAndDisposeToken()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            using (var vm = CreateViewModel())
            {
                // Act
                vm.CancelSearch();

                // After cleanup, a second call should not throw
                var exception = Record.Exception(() => vm.CancelSearch());
                Assert.Null(exception);
            }
        }

        [Fact]
        public void Dispose_InvokedMultipleTimes_ExitsEarlyThroughDisposedValueGuard()
        {
            using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
            {
                // Arrange
                var vm = CreateViewModel();

                // Act - Verify initial state before disposal context
                bool isDisposedBefore = TestReflection.GetField<bool>(vm, "_isDisposed");
                Assert.False(isDisposedBefore, "The logs view model wrapper should not initialize in a pre-disposed state.");

                // Act - First explicit teardown execution context
                vm.Dispose();

                // Assert - Guard must be active after primary disposal loop pass
                bool isDisposedAfterFirst = TestReflection.GetField<bool>(vm, "_isDisposed");
                Assert.True(isDisposedAfterFirst, "The internal _isDisposed state guard was not toggled on the primary cleanup path execution.");

                // Act - Manually alter the field value back to false to verify short-circuit branch safety coverage profiles natively
                TestReflection.SetField(vm, "_isDisposed", false);
                var doubleDisposeException = Record.Exception(vm.Dispose);

                // Assert
                Assert.Null(doubleDisposeException);
                Assert.True(TestReflection.GetField<bool>(vm, "_isDisposed"), "The state engine failed to toggle back to an active disposed layout configuration.");
            }
        }

        #endregion
    }
}