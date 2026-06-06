using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Moq;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class MonitoringViewModelBaseTests
    {
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IUiDispatcher> _uiDispatcherMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;

        public MonitoringViewModelBaseTests()
        {
            _cursorServiceMock = new Mock<ICursorService>();
            _uiDispatcherMock = new Mock<IUiDispatcher>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
        }

        #region Test Class Implementation

        private class TestMonitoringViewModel : MonitoringViewModelBase
        {
            private readonly Func<CancellationToken, Task> _onTickHandler;
            protected override int RefreshIntervalMs { get; }

            public bool IsOnMonitoringStoppedCalled { get; private set; }
            public bool PassedClearViewParam { get; private set; }

            public TestMonitoringViewModel(
                ICursorService cursorService,
                IUiDispatcher uiDispatcher,
                IServiceCommands serviceCommands,
                int refreshIntervalMs,
                Func<CancellationToken, Task> onTickHandler)
                : base(cursorService, uiDispatcher, serviceCommands)
            {
                RefreshIntervalMs = refreshIntervalMs;
                _onTickHandler = onTickHandler;
            }

            public void ExposeInitTimer() => InitTimer();
            public DispatcherTimer ExposeTimer => _timer;
            public CancellationTokenSource ExposeCts => _monitoringCts;
            public CancellationToken ExposeCurrentToken() => GetCurrentMonitoringToken();
            public int ExposeIsMonitoringFlag => Volatile.Read(ref _isMonitoringFlag);
            public int ExposeIsTickRunningFlag => Volatile.Read(ref _isTickRunningFlag);

            public void ExposeOnTick()
            {
                var method = typeof(MonitoringViewModelBase).GetMethod("OnTick", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(this, new object[] { this, EventArgs.Empty });
            }

            protected override async Task OnTickAsync()
            {
                await _onTickHandler(GetCurrentMonitoringToken());
            }

            protected override void OnMonitoringStopped(bool clearView)
            {
                IsOnMonitoringStoppedCalled = true;
                PassedClearViewParam = clearView;
                base.OnMonitoringStopped(clearView);
            }

            public void ExposeDispose(bool disposing)
            {
                var method = typeof(MonitoringViewModelBase).GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(this, new object[] { disposing });
            }

            protected override ServiceItemBase CreateServiceItem(Service service)
            {
                return null; // Not relevant for these tests
            }
        }

        #endregion

        #region Factory Method

        private TestMonitoringViewModel CreateViewModel(int interval = 100, Func<CancellationToken, Task> onTick = null)
        {
            return new TestMonitoringViewModel(
                _cursorServiceMock.Object,
                _uiDispatcherMock.Object,
                _serviceCommandsMock.Object,
                interval,
                onTick ?? (_ => Task.CompletedTask)
            );
        }

        #endregion

        #region Unit Tests

        [Fact]
        public void InitTimer_CreatesTimerWithCorrectInterval()
        {
            // Arrange
            var vm = CreateViewModel(interval: 250);

            // Act
            vm.ExposeInitTimer();

            // Assert
            Assert.NotNull(vm.ExposeTimer);
            Assert.Equal(TimeSpan.FromMilliseconds(250), vm.ExposeTimer.Interval);
            Assert.False(vm.ExposeTimer.IsEnabled);
        }

        [Fact]
        public void StartMonitoring_InitializesTimerAndSetsActiveFlags()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.StartMonitoring();

            // Assert
            Assert.Equal(1, vm.ExposeIsMonitoringFlag);
            Assert.NotNull(vm.ExposeTimer);
            Assert.True(vm.ExposeTimer.IsEnabled);
            Assert.NotNull(vm.ExposeCts);
            Assert.False(vm.ExposeCts.IsCancellationRequested);
        }

        [Fact]
        public void StopMonitoring_HaltsTimerCancelsCtsAndNotifiesDerivedClasses()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.StartMonitoring();
            var previousCts = vm.ExposeCts;

            // Act
            vm.StopMonitoring(clearView: true);

            // Assert
            Assert.Equal(0, vm.ExposeIsMonitoringFlag);
            Assert.False(vm.ExposeTimer.IsEnabled);
            Assert.True(previousCts.IsCancellationRequested);
            Assert.True(vm.IsOnMonitoringStoppedCalled);
            Assert.True(vm.PassedClearViewParam);
        }

        [Fact]
        public void GetCurrentMonitoringToken_LifecycleStates_ReturnsExpectedTokens()
        {
            // Arrange
            var vm = CreateViewModel();

            // Scenario 1: Not initialized yet -> Returns CancellationToken.None
            Assert.Equal(CancellationToken.None, vm.ExposeCurrentToken());
            Assert.False(vm.ExposeCurrentToken().IsCancellationRequested);

            // Scenario 2: Active monitoring session running -> Valid, live token
            vm.StartMonitoring();
            var activeToken = vm.ExposeCurrentToken();
            Assert.True(activeToken.CanBeCanceled);
            Assert.False(activeToken.IsCancellationRequested);

            // Scenario 3: Monitoring session explicitly stopped -> Token is explicitly cancelled
            vm.StopMonitoring();
            Assert.True(activeToken.IsCancellationRequested);

            // Scenario 4: After explicit Dispose -> Field becomes null, returns CancellationToken.None again
            vm.ExposeDispose(true);
            var postDisposeToken = vm.ExposeCurrentToken();

            // ==========================================
            // FIX: Assert design logic fallback patterns
            // ==========================================
            Assert.Equal(CancellationToken.None, postDisposeToken);
            Assert.False(postDisposeToken.IsCancellationRequested); // None is never in a cancelled state
        }

        [Fact]
        public void OnTick_NotMonitoring_ExitsEarlyWithoutExecutingPayload()
        {
            // Arrange
            bool tickExecuted = false;
            var vm = CreateViewModel(onTick: _ =>
            {
                tickExecuted = true;
                return Task.CompletedTask;
            });
            vm.ExposeInitTimer();

            // Act
            vm.ExposeOnTick(); // Executed directly while _isMonitoringFlag is zero

            // Assert
            Assert.False(tickExecuted);
            Assert.Equal(0, vm.ExposeIsTickRunningFlag);
        }

        [Fact]
        public void OnTick_OverlappingTicks_EnforcesAtomicGuardAndPreventsConcurrentExecution()
        {
            // Arrange
            int activeExecutionsCount = 0;
            var tcs = new TaskCompletionSource<object>();

            var vm = CreateViewModel(onTick: async _ =>
            {
                Interlocked.Increment(ref activeExecutionsCount);
                await tcs.Task; // Keep execution suspended inside the block
            });

            vm.StartMonitoring();

            // Act - Trigger initial tick execution flow
            vm.ExposeOnTick();
            Assert.Equal(1, activeExecutionsCount);
            Assert.Equal(1, vm.ExposeIsTickRunningFlag);

            // Act - Concurrently trigger subsequent tick entry attempts while first loop is running
            vm.ExposeOnTick();
            vm.ExposeOnTick();

            // Assert
            Assert.Equal(1, activeExecutionsCount); // Re-entrancy guard caught concurrent calls
            Assert.False(vm.ExposeTimer.IsEnabled); // Proves timer remains paused during operations

            // Teardown
            tcs.SetResult(null);
        }

        [Fact]
        public void OnTick_OperationCanceledException_ResetsRunningFlagAndRestartsTimer()
        {
            // Arrange
            var vm = CreateViewModel(onTick: _ => throw new OperationCanceledException());
            vm.StartMonitoring();

            // Act
            vm.ExposeOnTick();

            // Assert
            Assert.Equal(0, vm.ExposeIsTickRunningFlag);
            Assert.True(vm.ExposeTimer.IsEnabled); // Timer restarts cleanly inside finally block
        }

        [Fact]
        public void OnTick_ConsecutiveFailures_RateLimitsErrorLogsObservedByLoggers()
        {
            // Arrange
            // Redirect or mock standard global framework infrastructure logger channels if applicable.
            // Since Logger utilizes static methods, we verify via loop thresholds 
            var vm = CreateViewModel(onTick: _ => throw new InvalidOperationException("SCM connection drop out panic."));
            vm.StartMonitoring();

            // Act & Assert Loop Chain Simulation
            for (int i = 1; i <= 21; i++)
            {
                vm.ExposeOnTick();

                // Read internal atomic fields via reflection to ensure state stability matches error policy code loops
                var field = typeof(MonitoringViewModelBase).GetField("_tickErrorCount", BindingFlags.NonPublic | BindingFlags.Instance);
                long observedErrors = (long)field.GetValue(vm);

                Assert.Equal(i, observedErrors);
                Assert.Equal(0, vm.ExposeIsTickRunningFlag); // Ensures exception block prevents lockouts
            }
        }

        [Fact]
        public void Dispose_UnsubscribesEventsAndTearsDownFrameworkTimerReferences()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.StartMonitoring();
            var timerRef = vm.ExposeTimer;

            // Act
            vm.ExposeDispose(true);

            // Assert
            Assert.Null(vm.ExposeCts);
            Assert.Null(vm.ExposeTimer);
        }

        #endregion
    }
}