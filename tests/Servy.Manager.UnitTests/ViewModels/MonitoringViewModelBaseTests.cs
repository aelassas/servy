using Moq;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Testing;
using Servy.UI.Constants;
using Servy.UI.Services;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class MonitoringViewModelBaseTests : IDisposable
    {
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IUiDispatcher> _uiDispatcherMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;

        // Track generated SUT view model instances to enforce complete memory containment cleanup
        private readonly ConcurrentBag<TestMonitoringViewModel> _allocatedViewModels = new ConcurrentBag<TestMonitoringViewModel>();

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

            // Backing property to verify hosted selection variants
            public ServiceItemBase MockedSelectedService { get; set; }

            public bool IsResetMonitoringStateCalled { get; private set; }
            public ServiceItemBase LastAppliedSelection { get; private set; }

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

            // Connect the base abstraction hook to our local test control field
            protected override ServiceItemBase SelectedServiceItem => MockedSelectedService;

            public void ExposeOnTick()
            {
                TestReflection.InvokeNonPublic(this, "OnTick", this, EventArgs.Empty);
            }

            public void ExposeSetPidText(ServiceItemBase service)
            {
                TestReflection.InvokeNonPublic(this, "SetPidText", new object[] { service });
            }

            protected override void OnMonitoringStopped()
            {
                IsOnMonitoringStoppedCalled = true;
                base.OnMonitoringStopped();
            }

            protected override void ResetMonitoringState()
            {
                IsResetMonitoringStateCalled = true;
            }

            protected override async Task ApplyTickAsync(ServiceItemBase selection, CancellationToken token)
            {
                LastAppliedSelection = selection;
                await _onTickHandler(token);
            }

            public void ExposeDispose(bool disposing)
            {
                TestReflection.InvokeNonPublic(this, "Dispose", disposing);
            }

            protected override ServiceItemBase CreateServiceItem(Service service)
            {
                return null; // Not relevant for these tests
            }
        }

        private class ConcreteServiceItem : ServiceItemBase
        {
            // Concrete stub implementation for validation
        }

        #endregion

        #region Factory Method

        private TestMonitoringViewModel CreateViewModel(int interval = 100, Func<CancellationToken, Task> onTick = null)
        {
            var vm = new TestMonitoringViewModel(
                _cursorServiceMock.Object,
                _uiDispatcherMock.Object,
                _serviceCommandsMock.Object,
                interval,
                onTick ?? (_ => Task.CompletedTask)
            );

            _allocatedViewModels.Add(vm);
            return vm;
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
            vm.StopMonitoring();

            // Assert
            Assert.Equal(0, vm.ExposeIsMonitoringFlag);
            Assert.False(vm.ExposeTimer.IsEnabled);
            Assert.True(previousCts.IsCancellationRequested);
            Assert.True(vm.IsOnMonitoringStoppedCalled);
        }

        [Fact]
        public void GetCurrentMonitoringToken_LifecycleStates_ReturnsExpectedTokens()
        {
            // Arrange
            var vm = CreateViewModel();

            // Scenario 1: Not initialized yet -> Returns CancellationToken.None
            // Act & Assert
            Assert.Equal(CancellationToken.None, vm.ExposeCurrentToken());
            Assert.False(vm.ExposeCurrentToken().IsCancellationRequested);

            // Scenario 2: Active monitoring session running -> Valid, live token
            // Act
            vm.StartMonitoring();
            var activeToken = vm.ExposeCurrentToken();

            // Assert
            Assert.True(activeToken.CanBeCanceled);
            Assert.False(activeToken.IsCancellationRequested);

            // Scenario 3: Monitoring session explicitly stopped -> Token is explicitly cancelled
            // Act
            vm.StopMonitoring();

            // Assert
            Assert.True(activeToken.IsCancellationRequested);

            // Scenario 4: After explicit Dispose -> Field becomes null, returns CancellationToken.None again
            // Act
            vm.ExposeDispose(true);
            var postDisposeToken = vm.ExposeCurrentToken();

            // Assert
            Assert.Equal(CancellationToken.None, postDisposeToken);
            Assert.False(postDisposeToken.IsCancellationRequested);
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
            vm.ExposeOnTick();

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
                await tcs.Task;
            });

            // Active a valid service snapshot context to bypass the outer selection guard check inside OnTick logic
            vm.MockedSelectedService = new ConcreteServiceItem { Name = "LiveService", Pid = 9999 };
            vm.StartMonitoring();

            // Act - Trigger initial tick execution flow
            vm.ExposeOnTick();
            Assert.Equal(1, activeExecutionsCount);
            Assert.Equal(1, vm.ExposeIsTickRunningFlag);

            // Act - Concurrently trigger subsequent tick entry attempts while first loop is running
            vm.ExposeOnTick();
            vm.ExposeOnTick();

            // Assert
            Assert.Equal(1, activeExecutionsCount);
            Assert.False(vm.ExposeTimer.IsEnabled);

            // Teardown
            tcs.SetResult(null);
        }

        [Fact]
        public void OnTick_OperationCanceledException_ResetsRunningFlagAndRestartsTimer()
        {
            // Arrange
            var vm = CreateViewModel(onTick: _ => throw new OperationCanceledException());
            vm.MockedSelectedService = new ConcreteServiceItem { Name = "LiveService", Pid = 9999 };
            vm.StartMonitoring();

            // Act
            vm.ExposeOnTick();

            // Assert
            Assert.Equal(0, vm.ExposeIsTickRunningFlag);
            Assert.True(vm.ExposeTimer.IsEnabled);
        }

        [Fact]
        public void OnTick_ConsecutiveFailures_IncrementsErrorCountAndResetsRunningFlag()
        {
            // Arrange
            var vm = CreateViewModel(onTick: _ => throw new InvalidOperationException("SCM connection drop out panic."));
            vm.MockedSelectedService = new ConcreteServiceItem { Name = "LiveService", Pid = 9999 };
            vm.StartMonitoring();

            // Act & Assert Loop Chain Simulation
            for (int i = 1; i <= 21; i++)
            {
                // Act
                vm.ExposeOnTick();

                // Assert
                long observedErrors = TestReflection.GetField<long>(vm, "_tickErrorCount");

                Assert.Equal(i, observedErrors);
                Assert.Equal(0, vm.ExposeIsTickRunningFlag);
            }
        }

        [Fact]
        public void Dispose_UnsubscribesEventsAndTearsDownFrameworkTimerReferences()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.StartMonitoring();

            // Act
            vm.ExposeDispose(true);

            // Assert
            Assert.Null(vm.ExposeCts);
            Assert.Null(vm.ExposeTimer);
        }

        [Fact]
        public void CopyPidCommand_CanExecute_ReflectsSelectedServicePidAvailability()
        {
            // Arrange
            var vm = CreateViewModel();

            // Scenario 1: SelectedServiceItem is completely null
            // Act & Assert
            vm.MockedSelectedService = null;
            Assert.False(vm.CopyPidCommand.CanExecute(null));

            // Scenario 2: Service is set but Pid contains null value context
            // Act & Assert
            vm.MockedSelectedService = new ConcreteServiceItem { Pid = null };
            Assert.False(vm.CopyPidCommand.CanExecute(null));

            // Scenario 3: Service is active and has a valid tracked operating system ID
            // Act & Assert
            vm.MockedSelectedService = new ConcreteServiceItem { Pid = 4012 };
            Assert.True(vm.CopyPidCommand.CanExecute(null));
        }

        [Fact]
        public void SetPidText_VariousStates_CorrectlyUpdatesPropertyValues()
        {
            // Arrange
            var vm = CreateViewModel();

            // Branch 1: Null Service Item provided -> sets string text to N/A fallback string
            // Act
            vm.Pid = "InitialText";
            vm.ExposeSetPidText(null);

            // Assert
            Assert.Equal(UiConstants.NotAvailable, vm.Pid);

            // Branch 2: Valid Service Item but null numerical PID property
            // Act
            var itemWithNullPid = new ConcreteServiceItem { Pid = null };
            vm.Pid = "SomeExistingPidValue";
            vm.ExposeSetPidText(itemWithNullPid);

            // Assert
            Assert.Equal(UiConstants.NotAvailable, vm.Pid);

            // Branch 3: Valid Service Item containing active numeric tracking integer value
            // Act
            var itemWithValidPid = new ConcreteServiceItem { Name = "Spooler", Pid = 1248 };
            vm.Pid = "OldText";
            vm.ExposeSetPidText(itemWithValidPid);

            // Assert
            Assert.Equal("1248", vm.Pid);

            // Branch 4: Optimization match path logic -> Value stays same, skips redundant evaluations
            // Arrange change tracking parameters
            var propertyChangedFired = false;
            PropertyChangedEventHandler handler = (sender, e) =>
            {
                if (e.PropertyName == nameof(vm.Pid))
                {
                    propertyChangedFired = true;
                }
            };
            vm.PropertyChanged += handler;

            try
            {
                // Act
                vm.ExposeSetPidText(itemWithValidPid);

                // Assert
                Assert.Equal("1248", vm.Pid);
                // Symmetrical Verification: Verify that the change-suppression optimization actually prevented event execution loops
                Assert.False(propertyChangedFired, "PropertyChanged was erroneously raised for an optimized redundant value assignment.");
            }
            finally
            {
                // Tear down event handlers to preserve test runner sandbox limits
                vm.PropertyChanged -= handler;
            }
        }

        [Fact]
        public async Task CopyPidAsync_ValidServiceWithPid_DispatchesCommandInfrastructureAndExecutesSuccessfully()
        {
            // Arrange
            var vm = CreateViewModel();
            var serviceItem = new ConcreteServiceItem { Name = "ServyEngine", Pid = 5028 };
            vm.MockedSelectedService = serviceItem;

            _serviceCommandsMock
                .Setup(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "ServyEngine" && s.Pid == 5028), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            await vm.CopyPidCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task CopyPidAsync_NullOrMissingProperties_BypassesExecutionAndReturnsSilently()
        {
            // Arrange
            var vm = CreateViewModel();

            // Scenario 1: SelectedServiceItem is completely null -> bypasses invoke loop sequence
            // Act
            vm.MockedSelectedService = null;
            await vm.CopyPidCommand.ExecuteAsync(null);

            // Scenario 2: Service instance assigned but numerical Pid collection value is missing
            // Act
            vm.MockedSelectedService = new ConcreteServiceItem { Pid = null };
            await vm.CopyPidCommand.ExecuteAsync(null);

            // Assert: Verify that no clipboard copy commands were dispatched to system channels
            _serviceCommandsMock.Verify(c => c.CopyPidAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Teardown Lifecycle Execution

        /// <summary>
        /// Explicit test fixture teardown sequence to purge in-flight background CTS contexts safely.
        /// </summary>
        public void Dispose()
        {
            foreach (var vm in _allocatedViewModels)
            {
                try
                {
                    vm.Dispose();
                }
                catch
                {
                    // Catch-all block to guarantee adjacent cleanup executions complete safely
                }
            }
        }

        #endregion
    }
}