using Moq;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Constants;
using Servy.UI.Services;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class PerformanceViewModelTests
    {
        private readonly Mock<IServiceRepository> _mockServiceRepository;
        private readonly Mock<IServiceCommands> _mockServiceCommands;
        private readonly Mock<IAppConfiguration> _mockAppConfig;
        private readonly Mock<ICursorService> _mockCursorService;
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly Mock<IUiDispatcher> _mockUiDispatcher;

        public PerformanceViewModelTests()
        {
            _mockServiceRepository = new Mock<IServiceRepository>();
            _mockServiceCommands = new Mock<IServiceCommands>();
            _mockAppConfig = new Mock<IAppConfiguration>();
            _mockCursorService = new Mock<ICursorService>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockUiDispatcher = new Mock<IUiDispatcher>();

            // Setup configuration defaults to prevent timer initialization drops
            _mockAppConfig.Setup(c => c.PerformanceRefreshIntervalInMs).Returns(1000);

            // Stub out formatting helpers to return predictable metric text
            _mockProcessHelper.Setup(p => p.FormatCpuUsage(It.IsAny<double>())).Returns("15%");
            _mockProcessHelper.Setup(p => p.FormatRamUsage(It.IsAny<long>())).Returns("120 MB");
        }

        private PerformanceViewModel CreateViewModel()
        {
            return new PerformanceViewModel(
                _mockServiceRepository.Object,
                _mockServiceCommands.Object,
                _appConfigMockOrTarget,
                _mockCursorService.Object,
                _mockProcessHelper.Object,
                _mockUiDispatcher.Object);
        }

        private IAppConfiguration _appConfigMockOrTarget => _mockAppConfig.Object;

        #region Initialization & Constructor Verification

        [Fact]
        public void Constructor_NullGuards_ThrowsArgumentNullException()
        {
            Helper.RunOnSTA(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new PerformanceViewModel(null!, _mockServiceCommands.Object, _mockAppConfig.Object, _mockCursorService.Object, _mockProcessHelper.Object, _mockUiDispatcher.Object));
                Assert.Throws<ArgumentNullException>(() => new PerformanceViewModel(_mockServiceRepository.Object, _mockServiceCommands.Object, null!, _mockCursorService.Object, _mockProcessHelper.Object, _mockUiDispatcher.Object));
                Assert.Throws<ArgumentNullException>(() => new PerformanceViewModel(_mockServiceRepository.Object, _mockServiceCommands.Object, _mockAppConfig.Object, _mockCursorService.Object, null!, _mockUiDispatcher.Object));
            }, createApp: true);
        }

        [Fact]
        public void DesignTimeConstructor_InitializesAndSeedsGraphsSuccessfully()
        {
            Helper.RunOnSTA(() =>
            {
                var dtViewModel = new PerformanceViewModel();

                // Verify collections were seeded with placeholder zeros for immediate layout coverage
                Assert.Equal(UiConstants.NotAvailable, dtViewModel.Pid);
                Assert.NotNull(dtViewModel.CpuPointCollection);
                Assert.NotNull(dtViewModel.RamPointCollection);
            }, createApp: true);
        }

        #endregion

        #region Mutation & Graph Reset Behavior

        [Fact]
        public void SelectedService_ChangesSelection_ClearsBuffersAndRestartsMonitoring()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var mockService = new PerformanceService { Name = "WexflowEngine", Pid = 4321 };
                bool propChangedFired = false;

                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SelectedService)) propChangedFired = true;
                };

                // Act
                vm.SelectedService = mockService;

                // Assert
                Assert.True(propChangedFired);
                Assert.Same(mockService, vm.SelectedService);

                // Graph buffers should be completely reset to empty points during service transitions
                Assert.Empty(vm.CpuPointCollection);
                Assert.Empty(vm.RamPointCollection);
            }, createApp: true);
        }

        [Fact]
        public void SelectedService_SetSameReference_ShortCircuitsBranch()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var mockService = new PerformanceService { Name = "SameService" };
                vm.SelectedService = mockService;

                bool propertyChangedRaised = false;
                vm.PropertyChanged += (s, e) => propertyChangedRaised = true;

                // Act
                vm.SelectedService = mockService;

                // Assert
                Assert.False(propertyChangedRaised);
            }, createApp: true);
        }

        #endregion

        #region Performance Data Polling Loop (OnTickAsync) Tests

        [Fact]
        public async Task OnTickAsync_SelectedServiceNull_ResetsGraphLabels()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                vm.Pid = "999";
                vm.CpuUsage = "50%";

                // Force internal state flag via reflection to simulate a transition away from an active tracking state
                var fieldInfo = typeof(PerformanceViewModel).GetField("_hadSelectedService", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfo?.SetValue(vm, true);

                var methodInfo = typeof(PerformanceViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                var task = (Task)methodInfo!.Invoke(vm, null)!;
                task.GetAwaiter().GetResult();

                // Assert
                Assert.Equal(UiConstants.NotAvailable, vm.Pid);
                Assert.Equal(UiConstants.NotAvailable, vm.CpuUsage);
                Assert.False((bool)fieldInfo!.GetValue(vm)!);
            }, createApp: true);
        }

        [Fact]
        public async Task OnTickAsync_ValidService_CollectsMetricsAndHydratesPointCollections()
        {
            Helper.RunOnSTA(() =>
            {
                // 1. Establish the Synchronization Context for this STA Thread execution boundary.
                // This guarantees that 'await' continuations inside the view model snap back
                // to this thread instead of scattering onto the background pool thread.
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                var vm = CreateViewModel();
                var mockService = new PerformanceService { Name = "ServyDaemon", Pid = 2050 };
                vm.SelectedService = mockService;

                _mockServiceRepository.Setup(r => r.GetServicePidAsync("ServyDaemon", It.IsAny<CancellationToken>()))
                                      .ReturnsAsync(2050);

                var fakeMetrics = new ProcessMetrics(45.5, 50 * 1024 * 1024);
                _mockProcessHelper.Setup(p => p.GetProcessTreeMetrics(2050)).Returns(fakeMetrics);

                // Mock IUiDispatcher to invoke actions immediately on this thread
                _mockUiDispatcher.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                                 .Callback<Action>(action => action())
                                 .Returns(Task.CompletedTask);

                var methodInfo = typeof(PerformanceViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                var task = (Task)methodInfo!.Invoke(vm, null)!;

                // 2. Keep the message pump processing while waiting for Task.Run to finish
                // This simulates a true WPF environment, letting the continuation pass safely.
                while (!task.IsCompleted)
                {
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    Thread.Sleep(1);
                }

                task.GetAwaiter().GetResult();

                // Assert
                Assert.Equal("2050", vm.Pid);
                Assert.Equal("15%", vm.CpuUsage);
                Assert.Equal("120 MB", vm.RamUsage);

                Assert.NotEmpty(vm.CpuPointCollection);
                Assert.NotEmpty(vm.CpuFillPoints);
                Assert.NotEmpty(vm.RamPointCollection);
                Assert.NotEmpty(vm.RamFillPoints);
            }, createApp: true);
        }

        #endregion

        #region Command Processing & Clear Framework Flags

        [Fact]
        public async Task CopyPidCommand_ValidSelection_InvokesDownstreamCommands()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var mockService = new PerformanceService { Name = "ActiveService", Pid = 8888 };
                vm.SelectedService = mockService;

                // Act
                vm.CopyPidCommand.ExecuteAsync(null).GetAwaiter().GetResult();

                // Assert
                _mockServiceCommands.Verify(c => c.CopyPid(It.Is<Service>(s => s.Name == "ActiveService")), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public void OnMonitoringStopped_ClearViewTrue_FlushesPointCollections()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                vm.CpuPointCollection.Add(new Point(10, 10));

                var methodInfo = typeof(PerformanceViewModel).GetMethod("OnMonitoringStopped", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                methodInfo!.Invoke(vm, new object[] { true });

                // Assert
                Assert.Empty(vm.CpuPointCollection);
                Assert.Empty(vm.CpuFillPoints);
                Assert.Empty(vm.RamPointCollection);
                Assert.Empty(vm.RamFillPoints);
            }, createApp: true);
        }

        #endregion
    }
}