using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Testing;
using Servy.UI.Constants;
using Servy.UI.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Helper = Servy.Testing.Helper;

namespace Servy.Manager.UnitTests.ViewModels
{
    [Collection(AmbientTestCollection.Name)]
    public class PerformanceViewModelTests : IDisposable
    {
        private readonly Mock<IServiceRepository> _mockServiceRepository;
        private readonly Mock<IServiceCommands> _mockServiceCommands;
        private readonly Mock<IAppConfiguration> _mockAppConfig;
        private readonly Mock<ICursorService> _mockCursorService;
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly Mock<IUiDispatcher> _mockUiDispatcher;

        // Track generated SUT view model instances to enforce complete memory containment cleanup
        private readonly ConcurrentBag<PerformanceViewModel> _allocatedViewModels = new ConcurrentBag<PerformanceViewModel>();

        public PerformanceViewModelTests()
        {
            _mockServiceRepository = new Mock<IServiceRepository>();
            _mockServiceCommands = new Mock<IServiceCommands>();
            _mockAppConfig = new Mock<IAppConfiguration>();
            _mockCursorService = new Mock<ICursorService>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockUiDispatcher = new Mock<IUiDispatcher>();
            _mockProcessKiller = new Mock<IProcessKiller>();

            // InitTimer() reads PerformanceRefreshIntervalInMs; give it a sane interval
            _mockAppConfig.Setup(c => c.PerformanceRefreshIntervalInMs).Returns(1000);

            // Stub out formatting helpers to return predictable metric text
            _mockProcessHelper.Setup(p => p.FormatCpuUsage(It.IsAny<double>())).Returns("15%");
            _mockProcessHelper.Setup(p => p.FormatRamUsage(It.IsAny<long>())).Returns("120 MB");
        }

        private PerformanceViewModel CreateViewModel()
        {
            var vm = new PerformanceViewModel(
                _mockServiceRepository.Object,
                _mockServiceCommands.Object,
                _mockAppConfig.Object,
                _mockCursorService.Object,
                _mockProcessHelper.Object,
                _mockUiDispatcher.Object);

            _allocatedViewModels.Add(vm);
            return vm;
        }

        #region Initialization & Constructor Verification

        [Theory]
        [InlineData(0, "serviceRepository")]
        [InlineData(1, "serviceCommands")]
        [InlineData(2, "appConfig")]
        [InlineData(3, "cursorService")]
        [InlineData(4, "processHelper")]
        [InlineData(5, "uiDispatcher")]
        public void Constructor_NullGuards_ThrowsArgumentNullException(int nullIndex, string expectedParamName)
        {
            // Arrange & Act & Assert
            Helper.RunOnSTA(() =>
            {
                var ex = Assert.Throws<ArgumentNullException>(() => new PerformanceViewModel(
                    nullIndex == 0 ? null! : _mockServiceRepository.Object,
                    nullIndex == 1 ? null! : _mockServiceCommands.Object,
                    nullIndex == 2 ? null! : _mockAppConfig.Object,
                    nullIndex == 3 ? null! : _mockCursorService.Object,
                    nullIndex == 4 ? null! : _mockProcessHelper.Object,
                    nullIndex == 5 ? null! : _mockUiDispatcher.Object));

                Assert.Equal(expectedParamName, ex.ParamName);
            }, createApp: true);
        }

        [Fact]
        public void DesignTimeConstructor_InitializesEmptyGraphCollections()
        {
            // Arrange & Act
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var dtViewModel = new PerformanceViewModel())
                {
                    _allocatedViewModels.Add(dtViewModel);

                    // Assert
                    // Verify basic structural state and clean empty graph collection initialization
                    Assert.Equal(UiConstants.NotAvailable, dtViewModel.Pid);
                    Assert.NotNull(dtViewModel.CpuPointCollection);
                    Assert.NotNull(dtViewModel.RamPointCollection);
                    Assert.Empty(dtViewModel.CpuPointCollection);
                    Assert.Empty(dtViewModel.RamPointCollection);
                }
            }, createApp: true);
        }

        #endregion

        #region Mutation & Graph Reset Behavior

        [Fact]
        public void SelectedService_ChangesSelection_ClearsBuffersAndRestartsMonitoring()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    var mockService = new PerformanceService { Name = "WexflowEngine", Pid = 4321 };
                    bool propChangedFired = false;

                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(vm.SelectedService)) propChangedFired = true;
                    };

                    // Seed points into collections to ensure buffer clearing is genuinely verified during service transitions
                    vm.CpuPointCollection.Add(new Point(1, 1));
                    vm.RamPointCollection.Add(new Point(2, 2));

                    // Act
                    vm.SelectedService = mockService;

                    // Assert - Verification Part 1: State Changes & Buffers Cleared
                    Assert.True(propChangedFired);
                    Assert.Same(mockService, vm.SelectedService);

                    // Graph buffers should be completely reset to empty points during service transitions
                    Assert.Empty(vm.CpuPointCollection);
                    Assert.Empty(vm.RamPointCollection);

                    // Assert - Verification Part 2: RestartsMonitoring
                    // Verify that the underlying monitoring lifecycle is active and tracking tokens are initialized
                    int isMonitoringFlag = TestReflection.GetField<int>(vm, "_isMonitoringFlag");
                    var timer = TestReflection.GetField<DispatcherTimer>(vm, "_timer");
                    var cancellationTokenSource = TestReflection.GetField<CancellationTokenSource>(vm, "_monitoringCts");

                    Assert.Equal(1, isMonitoringFlag); // 1 flags that base class monitoring is active
                    Assert.NotNull(timer);
                    Assert.True(timer.IsEnabled); // The background polling loop timer is active
                    Assert.NotNull(cancellationTokenSource);
                    Assert.False(cancellationTokenSource.IsCancellationRequested); // Fresh, un-cancelled token source is active
                }
            }, createApp: true);
        }

        [Fact]
        public void SelectedService_SetSameReference_ShortCircuitsBranch()
        {
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    var mockService = new PerformanceService { Name = "SameService" };
                    vm.SelectedService = mockService;

                    bool propertyChangedRaised = false;
                    vm.PropertyChanged += (s, e) => propertyChangedRaised = true;

                    // Act
                    vm.SelectedService = mockService;

                    // Assert
                    Assert.False(propertyChangedRaised);
                }
            }, createApp: true);
        }

        #endregion

        #region Performance Data Polling Loop (OnTickAsync) Tests

        [Fact]
        public void OnTickAsync_SelectedServiceNull_ResetsGraphLabels()
        {
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    // Arrange
                    vm.Pid = "999";
                    vm.CpuUsage = "50%";
                    vm.RamUsage = "300 MB";
                    vm.CpuPointCollection.Add(new Point(1, 1));
                    vm.RamPointCollection.Add(new Point(2, 2));

                    // Force internal state flag via reflection helper to simulate a transition away from an active tracking state
                    TestReflection.SetField(vm, "_hadSelectedService", true);

                    // Act
                    var task = (Task)TestReflection.InvokeNonPublic(vm, "OnTickAsync")!;
                    task.GetAwaiter().GetResult();

                    // Assert
                    Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                    Assert.Equal(UiConstants.NotAvailable, vm.CpuUsage);
                    Assert.Equal(UiConstants.NotAvailable, vm.RamUsage);
                    Assert.Empty(vm.CpuPointCollection);
                    Assert.Empty(vm.RamPointCollection);
                    Assert.False(TestReflection.GetField<bool>(vm, "_hadSelectedService"));
                }
            }, createApp: true);
        }

        [Fact]
        public void OnTickAsync_ValidService_CollectsMetricsAndHydratesPointCollections()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    // 1. Establish the Synchronization Context for this STA Thread execution boundary.
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                    var mockService = new PerformanceService { Name = "ServyDaemon", Pid = 2050 };
                    vm.SelectedService = mockService;

                    // Stop the background DispatcherTimer to prevent concurrent automatic ticks during manual dispatcher pumping
                    TestReflection.GetField<DispatcherTimer>(vm, "_timer")?.Stop();

                    _mockServiceRepository.Setup(r => r.GetServicePidAsync("ServyDaemon", It.IsAny<CancellationToken>()))
                                           .ReturnsAsync(2050);

                    var fakeMetrics = new ProcessMetrics(45.5, 50 * 1024 * 1024);
                    _mockProcessHelper.Setup(p => p.GetProcessTreeMetrics(2050)).Returns(fakeMetrics);

                    // Pin formatting inputs precisely with distinguishable return strings to verify that collected metrics reach formatters rather than generic defaults
                    _mockProcessHelper.Setup(p => p.FormatCpuUsage(It.Is<double>(d => Math.Abs(d - 45.5) < 0.001))).Returns("45.5%");
                    _mockProcessHelper.Setup(p => p.FormatRamUsage(It.Is<long>(b => b == 50L * 1024 * 1024))).Returns("50 MB");

                    // Mock IUiDispatcher to invoke actions immediately on this thread
                    _mockUiDispatcher.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                                     .Callback<Action>(action => action())
                                     .Returns(Task.CompletedTask);

                    // Act
                    // Use TestReflection to invoke OnTickAsync, which now supports inheritance hierarchy traversal
                    var task = (Task)TestReflection.InvokeNonPublic(vm, "OnTickAsync")!;

                    // 2. Keep the message pump processing while waiting for Task.Run to finish
                    var pumpTimeout = TimeSpan.FromSeconds(10);
                    var sw = Stopwatch.StartNew();
                    while (!task.IsCompleted)
                    {
                        if (sw.Elapsed > pumpTimeout)
                            throw new TimeoutException($"OnTickAsync did not complete within {pumpTimeout.TotalSeconds:0}s.");
                        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                        Thread.Sleep(1);
                    }

                    task.GetAwaiter().GetResult();

                    // Assert
                    Assert.Equal("2050", vm.Pid);
                    Assert.Equal("45.5%", vm.CpuUsage);
                    Assert.Equal("50 MB", vm.RamUsage);

                    // Explicitly verify that exact metrics passed through the mock formatters
                    _mockProcessHelper.Verify(p => p.FormatCpuUsage(45.5), Times.Once);
                    _mockProcessHelper.Verify(p => p.FormatRamUsage(50L * 1024 * 1024), Times.Once);

                    Assert.NotEmpty(vm.CpuPointCollection);
                    Assert.NotEmpty(vm.CpuFillPoints);
                    Assert.NotEmpty(vm.RamPointCollection);
                    Assert.NotEmpty(vm.RamFillPoints);
                }
            }, createApp: true);
        }

        [Fact]
        public void OnTickAsync_ServiceStopped_ClearsPidAndDisablesCopyPidCommand()
        {
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    // Arrange
                    var mockService = new PerformanceService { Name = "ServyDaemon", Pid = 2050 };
                    vm.SelectedService = mockService;

                    // Stop the background DispatcherTimer to prevent concurrent automatic ticks
                    TestReflection.GetField<DispatcherTimer>(vm, "_timer")?.Stop();

                    // The monitored service stopped, so the repository no longer reports a PID
                    _mockServiceRepository.Setup(r => r.GetServicePidAsync("ServyDaemon", It.IsAny<CancellationToken>()))
                                           .ReturnsAsync((int?)null);

                    // Act
                    var task = (Task)TestReflection.InvokeNonPublic(vm, "OnTickAsync")!;
                    task.GetAwaiter().GetResult();

                    // Assert - the running -> stopped transition clears the PID and greys out the Copy PID button
                    Assert.Null(mockService.Pid);
                    Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                    Assert.False(vm.CopyPidCommand.CanExecute(null));

                    // The early return is what makes the branch falsifiable: no metrics are collected for a stopped service
                    _mockProcessHelper.Verify(p => p.GetProcessTreeMetrics(It.IsAny<int>()), Times.Never);
                }
            }, createApp: true);
        }

        [Fact]
        public void OnTickAsync_PidChanged_ResetsGraphsAndCollectsForTheNewPid()
        {
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    // Arrange
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                    var mockService = new PerformanceService { Name = "ServyDaemon", Pid = 2050 };
                    vm.SelectedService = mockService;

                    TestReflection.GetField<DispatcherTimer>(vm, "_timer")?.Stop();

                    // Seed a point that only a graph reset can remove, so the reset is verified and not assumed
                    var stalePoint = new Point(-1, -1);
                    vm.CpuPointCollection.Add(stalePoint);
                    vm.RamPointCollection.Add(stalePoint);

                    // The service restarted, so it comes back on a different PID than the seeded one
                    _mockServiceRepository.Setup(r => r.GetServicePidAsync("ServyDaemon", It.IsAny<CancellationToken>()))
                                           .ReturnsAsync(9999);

                    var fakeMetrics = new ProcessMetrics(45.5, 50 * 1024 * 1024);
                    _mockProcessHelper.Setup(p => p.GetProcessTreeMetrics(9999)).Returns(fakeMetrics);

                    _mockUiDispatcher.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                                     .Callback<Action>(action => action())
                                     .Returns(Task.CompletedTask);

                    // Act
                    PumpUntilCompleted((Task)TestReflection.InvokeNonPublic(vm, "OnTickAsync")!);

                    // Assert - the new PID is adopted and the previous process's history is not charted as the new one's
                    Assert.Equal(9999, mockService.Pid);
                    Assert.Equal("9999", vm.Pid);
                    Assert.DoesNotContain(stalePoint, vm.CpuPointCollection);
                    Assert.DoesNotContain(stalePoint, vm.RamPointCollection);

                    _mockProcessHelper.Verify(p => p.GetProcessTreeMetrics(9999), Times.Once);
                    _mockProcessHelper.Verify(p => p.GetProcessTreeMetrics(2050), Times.Never);
                }
            }, createApp: true);
        }

        [Fact]
        public void OnTickAsync_SelectionChangesDuringPidLookup_DropsTheTick()
        {
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    // Arrange
                    var mockService = new PerformanceService { Name = "ServyDaemon", Pid = 2050 };
                    vm.SelectedService = mockService;

                    TestReflection.GetField<DispatcherTimer>(vm, "_timer")?.Stop();

                    // The user switches service while the repository call is in flight. The backing field is
                    // swapped directly so the switch does not restart the monitoring timer mid-test.
                    _mockServiceRepository.Setup(r => r.GetServicePidAsync("ServyDaemon", It.IsAny<CancellationToken>()))
                                           .Callback(() => TestReflection.SetField(vm, "_selectedService",
                                               new PerformanceService { Name = "OtherService", Pid = 1 }))
                                           .ReturnsAsync(2050);

                    // Act
                    var task = (Task)TestReflection.InvokeNonPublic(vm, "OnTickAsync")!;
                    task.GetAwaiter().GetResult();

                    // Assert - the superseded selection's metrics never reach the new selection's graph
                    _mockProcessHelper.Verify(p => p.GetProcessTreeMetrics(It.IsAny<int>()), Times.Never);
                    Assert.Equal(UiConstants.NotAvailable, vm.CpuUsage);
                    Assert.Empty(vm.CpuPointCollection);
                }
            }, createApp: true);
        }

        [Fact]
        public void OnTickAsync_MonitoringCancelledDuringMetricCollection_DropsTheTick()
        {
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    // Arrange
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                    var mockService = new PerformanceService { Name = "ServyDaemon", Pid = 2050 };
                    vm.SelectedService = mockService;

                    TestReflection.GetField<DispatcherTimer>(vm, "_timer")?.Stop();

                    _mockServiceRepository.Setup(r => r.GetServicePidAsync("ServyDaemon", It.IsAny<CancellationToken>()))
                                           .ReturnsAsync(2050);

                    // Monitoring is cancelled while the metrics are being collected off the UI thread
                    _mockProcessHelper.Setup(p => p.GetProcessTreeMetrics(2050))
                                      .Callback(() => TestReflection.GetField<CancellationTokenSource>(vm, "_monitoringCts").Cancel())
                                      .Returns(new ProcessMetrics(45.5, 50 * 1024 * 1024));

                    _mockUiDispatcher.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                                     .Callback<Action>(action => action())
                                     .Returns(Task.CompletedTask);

                    // Act
                    PumpUntilCompleted((Task)TestReflection.InvokeNonPublic(vm, "OnTickAsync")!);

                    // Assert - metrics collected for a cancelled monitoring session are discarded
                    _mockProcessHelper.Verify(p => p.FormatCpuUsage(It.IsAny<double>()), Times.Never);
                    Assert.Equal(UiConstants.NotAvailable, vm.CpuUsage);
                    Assert.Equal(UiConstants.NotAvailable, vm.RamUsage);
                    Assert.Empty(vm.CpuPointCollection);
                }
            }, createApp: true);
        }

        [Fact]
        public void OnTickAsync_SelectionChangesDuringMetricCollection_DropsTheTick()
        {
            Helper.RunOnSTA(() =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    // Arrange
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                    var mockService = new PerformanceService { Name = "ServyDaemon", Pid = 2050 };
                    vm.SelectedService = mockService;

                    TestReflection.GetField<DispatcherTimer>(vm, "_timer")?.Stop();

                    _mockServiceRepository.Setup(r => r.GetServicePidAsync("ServyDaemon", It.IsAny<CancellationToken>()))
                                           .ReturnsAsync(2050);

                    // The user switches service while the metrics are being collected off the UI thread
                    _mockProcessHelper.Setup(p => p.GetProcessTreeMetrics(2050))
                                      .Callback(() => TestReflection.SetField(vm, "_selectedService",
                                          new PerformanceService { Name = "OtherService", Pid = 1 }))
                                      .Returns(new ProcessMetrics(45.5, 50 * 1024 * 1024));

                    _mockUiDispatcher.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                                     .Callback<Action>(action => action())
                                     .Returns(Task.CompletedTask);

                    // Act
                    PumpUntilCompleted((Task)TestReflection.InvokeNonPublic(vm, "OnTickAsync")!);

                    // Assert - the stale service's metrics are not written into the newly selected service's graph
                    _mockProcessHelper.Verify(p => p.FormatCpuUsage(It.IsAny<double>()), Times.Never);
                    Assert.Equal(UiConstants.NotAvailable, vm.CpuUsage);
                    Assert.Equal(UiConstants.NotAvailable, vm.RamUsage);
                    Assert.Empty(vm.CpuPointCollection);
                }
            }, createApp: true);
        }

        /// <summary>
        /// Keeps the current dispatcher pumping until <paramref name="task"/> completes, so the
        /// <see cref="Task.Run(Action)"/> awaited inside ApplyTickAsync can finish, then observes its result.
        /// </summary>
        /// <param name="task">The tick task to await.</param>
        private static void PumpUntilCompleted(Task task)
        {
            var sw = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (sw.Elapsed > TestTimeouts.CiGenerous)
                    throw new TimeoutException($"OnTickAsync did not complete within {TestTimeouts.CiGenerous.TotalSeconds:0}s.");
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(1);
            }

            task.GetAwaiter().GetResult();
        }

        #endregion

        #region Command Processing & Clear Framework Flags

        [Fact]
        public async Task CopyPidCommand_ValidSelection_InvokesDownstreamCommands()
        {
            await Helper.RunOnSTA(async () =>
            {
                using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessKiller.Object)))
                using (var vm = CreateViewModel())
                {
                    var mockService = new PerformanceService { Name = "ActiveService", Pid = 8888 };
                    vm.SelectedService = mockService;

                    // Act
                    await vm.CopyPidCommand.ExecuteAsync(null);

                    // Assert
                    _mockServiceCommands.Verify(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "ActiveService" && s.Pid == 8888), It.IsAny<CancellationToken>()), Times.Once);
                }
            }, createApp: true);
        }

        #endregion

        #region Disposal & Teardown

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
