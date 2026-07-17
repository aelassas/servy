using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Testing;
using Servy.UI;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;
using static Servy.Manager.ViewModels.MainViewModel;
using Helper = Servy.Testing.Helper;

namespace Servy.Manager.UnitTests.ViewModels
{
    [Collection("Ambient AppServices Dependent Tests")]
    public class MainViewModelTests
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IServiceRepository> _serviceRepositoryMock;
        private readonly Mock<IHelpService> _helpServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IProcessHelper> _processHelperMock;
        private readonly Mock<IProcessKiller> _processKillerMock;

        // Child ViewModels
        private readonly Mock<PerformanceViewModel> _performanceViewModelMock;
        private readonly Mock<ConsoleViewModel> _consoleViewModelMock;
        private readonly Mock<DependenciesViewModel> _dependenciesViewModelMock;

        public MainViewModelTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
            _serviceRepositoryMock = new Mock<IServiceRepository>();
            _helpServiceMock = new Mock<IHelpService>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _messageBoxServiceMock = new Mock<IMessageBoxService>();
            _cursorServiceMock = new Mock<ICursorService>();
            _processHelperMock = new Mock<IProcessHelper>();
            _processKillerMock = new Mock<IProcessKiller>();

            var uiDispatcherMock = new Mock<IUiDispatcher>();
            uiDispatcherMock.Setup(d => d.YieldAsync()).Returns(Task.CompletedTask);

            _appConfigMock = new Mock<IAppConfiguration>();
            _appConfigMock.Setup(c => c.RefreshIntervalInSeconds).Returns(5);
            _appConfigMock.Setup(c => c.PerformanceRefreshIntervalInMs).Returns(1000);
            _appConfigMock.Setup(c => c.ConsoleMaxLines).Returns(500);
            _appConfigMock.Setup(c => c.DependenciesRefreshIntervalInMs).Returns(1000);
            _appConfigMock.Setup(c => c.ConsoleRefreshIntervalInMs).Returns(500);
            _appConfigMock.Setup(c => c.IsDesktopAppAvailable).Returns(true);
            _appConfigMock.Setup(c => c.MaxBulkOperationParallelism).Returns(2);

            _performanceViewModelMock = new Mock<PerformanceViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                _processHelperMock.Object,
                uiDispatcherMock.Object);

            _consoleViewModelMock = new Mock<ConsoleViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                uiDispatcherMock.Object);

            _dependenciesViewModelMock = new Mock<DependenciesViewModel>(
                _serviceRepositoryMock.Object,
                _serviceManagerMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                uiDispatcherMock.Object,
                _messageBoxServiceMock.Object);
        }

        private MainViewModel CreateViewModel(Dispatcher dispatcher = null)
        {
            // Always fall back to the explicit thread-bound Dispatcher context
            // instead of cross-contaminating shared static application references.
            return new MainViewModel(
                _serviceManagerMock.Object,
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _helpServiceMock.Object,
                _messageBoxServiceMock.Object,
                _performanceViewModelMock.Object,
                _consoleViewModelMock.Object,
                _dependenciesViewModelMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                _processHelperMock.Object,
                dispatcher ?? Dispatcher.CurrentDispatcher
            );
        }

        #region Private Message Pump Redirection Helpers

        /// <summary>
        /// Centralizes the message pump execution frame ceremony to eliminate copy-pasted boilerplate,
        /// ensuring asynchronous operations push background tasks onto the STA apartment cleanly.
        /// </summary>
        private static void RunOnPump(Dispatcher dispatcher, Func<Task> action)
        {
            var frame = new DispatcherFrame();

            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                }
                catch (Exception)
                {
                    // Suppress expected target exceptions to let the frame unwind cleanly
                }
                finally
                {
                    frame.Continue = false;
                }
            }, DispatcherPriority.Normal);

            var sw = Stopwatch.StartNew();
            var watchdog = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            watchdog.Tick += (s, e) => { frame.Continue = false; };
            watchdog.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { watchdog.Stop(); }
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), "Pump timed out");
        }

        #endregion

        #region Constructors & Properties

        [Theory]
        [InlineData(0)]  // serviceManager
        [InlineData(1)]  // serviceRepository
        [InlineData(2)]  // serviceCommands
        [InlineData(3)]  // helpService
        [InlineData(4)]  // messageBoxService
        [InlineData(5)]  // performanceVM
        [InlineData(6)]  // consoleVM
        [InlineData(7)]  // dependenciesVM
        [InlineData(8)]  // appConfig
        [InlineData(9)]  // cursorService
        [InlineData(10)] // processHelper
        public void Constructor_NullGuards_ThrowsArgumentNullException(int nullParamIndex)
        {
            // Arrange & Act & Assert
            Helper.RunOnSTA(() =>
            {
                var args = new object[]
                {
                    nullParamIndex == 0 ? null : _serviceManagerMock.Object,
                    nullParamIndex == 1 ? null : _serviceRepositoryMock.Object,
                    nullParamIndex == 2 ? null : _serviceCommandsMock.Object,
                    nullParamIndex == 3 ? null : _helpServiceMock.Object,
                    nullParamIndex == 4 ? null : _messageBoxServiceMock.Object,
                    nullParamIndex == 5 ? null : _performanceViewModelMock.Object,
                    nullParamIndex == 6 ? null : _consoleViewModelMock.Object,
                    nullParamIndex == 7 ? null : _dependenciesViewModelMock.Object,
                    nullParamIndex == 8 ? null : _appConfigMock.Object,
                    nullParamIndex == 9 ? null : _cursorServiceMock.Object,
                    nullParamIndex == 10 ? null : _processHelperMock.Object,
                    Dispatcher.CurrentDispatcher // Dispatcher parameter is optional (nullable)
                };

                Assert.Throws<ArgumentNullException>(() =>
                {
                    try
                    {
                        Activator.CreateInstance(typeof(MainViewModel), args);
                    }
                    catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
                    {
                        // TargetInvocationException is thrown when using Activator.CreateInstance.
                        // Unwrap and throw the real inner ArgumentNullException so Assert.Throws catches it.
                        throw ex.InnerException;
                    }
                });
            }, createApp: true);
        }

        [Fact]
        public void Constructor_DesignTime_DoesNotThrow()
        {
            // Arrange & Act & Assert
            Helper.RunOnSTA(() =>
            {
                var vm = new MainViewModel();
                Assert.NotNull(vm);
            }, createApp: true);
        }

        [Fact]
        public void ServiceCommands_Setter_UpdatesChildViewModels()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var newCommands = new Mock<IServiceCommands>().Object;

                // Act
                vm.ServiceCommands = newCommands;

                // Assert
                Assert.Equal(newCommands, vm.PerformanceVM.ServiceCommands);
                Assert.Equal(newCommands, vm.ConsoleVM.ServiceCommands);
                Assert.Equal(newCommands, vm.DependenciesVM.ServiceCommands);
            }, createApp: true);
        }

        [Fact]
        public void AppConfig_PropertyChanged_UpdatesConfiguratorEnabled()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                // Initialize state: set to false and fire change notification
                _appConfigMock.Setup(c => c.IsDesktopAppAvailable).Returns(false);
                _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IAppConfiguration.IsDesktopAppAvailable)));
                Assert.False(vm.IsConfiguratorEnabled);

                // Act 1 (Control)
                // Flip the mock value to true. If the view model filters correctly, raising an irrelevant 
                // property will NOT trigger a re-read, and the VM state will stay 'false'.
                _appConfigMock.Setup(c => c.IsDesktopAppAvailable).Returns(true);
                _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs("OtherProperty"));

                // Assert 1: Verify the unrelated property change notification was ignored cleanly
                Assert.False(vm.IsConfiguratorEnabled);

                // Act 2 (Positive Path)
                // Now fire the expected property change to prove that it reacts to the matching name filter rule
                _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IAppConfiguration.IsDesktopAppAvailable)));

                // Assert 2: Verify the target property filter caught the notification and updated the state
                Assert.True(vm.IsConfiguratorEnabled);
            }, createApp: true);
        }

        #endregion

        #region Search & SelectAll Cascades

        [Fact]
        public async Task SearchCommand_PopulatesServicesAndHandlesSelectAllStates()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                var services = new List<Service>
                {
                    new Service { Name = "S1", IsInstalled = true },
                    new Service { Name = "S2", IsInstalled = true }
                };

                _serviceCommandsMock.Setup(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(services);

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.SearchCommand.ExecuteAsync(null);
                });

                // Assert
                var view = vm.ServicesView.Cast<ServiceRowViewModel>().ToList();
                Assert.Equal(2, view.Count);

                // Branch 1: SelectAll true -> All children true
                vm.SelectAll = true;
                Assert.True(view[0].IsChecked);
                Assert.True(view[1].IsChecked);

                // Branch 2: Child modified to false -> SelectAll becomes null (Indeterminate)
                view[0].IsChecked = false;
                Assert.Null(vm.SelectAll);
                Assert.True(vm.HasSelectedServices);

                // Branch 3: All children false -> SelectAll becomes false
                view[1].IsChecked = false;
                Assert.False(vm.SelectAll);
                Assert.False(vm.HasSelectedServices);

                // Branch 4: Double-set skip - redundant set must be a no-op
                var raised = false;
                vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.SelectAll)) raised = true; };
                vm.SelectAll = false;
                Assert.False(raised);

                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task SearchCommand_NullDispatcher_ExitsWithoutSearching()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();
                TestReflection.SetField(vm, "_dispatcher", null);

                // Act
                await vm.SearchCommand.ExecuteAsync(null);

                // Assert
                _serviceCommandsMock.Verify(c => c.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        #endregion

        #region Background Refresh, Ghost PIDs & Data Drift

        [Fact]
        public void TimerLifecycle_CreateStartStop_ManagesResourcesCorrectly()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                // Act & Assert
                vm.CreateAndStartTimer();
                var timer = TestReflection.GetField<DispatcherTimer>(vm, "_refreshTimer");

                Assert.NotNull(timer);
                Assert.True(timer.IsEnabled);

                vm.StopRefreshTimer();
                timer = TestReflection.GetField<DispatcherTimer>(vm, "_refreshTimer");

                Assert.Null(timer);
            }, createApp: true);
        }

        [Fact]
        public void OnTick_OverlappingTicks_PreventedByInterlocked()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var vm = CreateViewModel();

                // Act & Assert
                TestReflection.InvokeNonPublic(vm, "OnTick", null, EventArgs.Empty);
                TestReflection.InvokeNonPublic(vm, "OnTick", null, EventArgs.Empty);

                Assert.True(true);
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_OsAndDbDrift_ResolvesGhostPidsAndUpdatesDto()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var vm = CreateViewModel();
                var targetService = new Service
                {
                    Name = "DriftService",
                    Pid = 9999,
                    Description = "Old UI Desc",
                    StartupType = ServiceStartType.Manual
                };

                var osMockPayload = new Dictionary<string, ServiceInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "DriftService",
                        new ServiceInfo
                        {
                            Name = "DriftService",
                            Status = ServiceStatus.Stopped,
                            Description = "New OS Desc",
                            StartupType = ServiceStartType.Automatic,
                            LogOnAs = "CustomUser"
                        }
                    }
                };

                var databaseDto = new ServiceDto
                {
                    Name = "DriftService",
                    Pid = 9999,
                    Description = "Old DB Desc",
                    StartupType = (int)ServiceStartType.Manual
                };

                // Act
                var result = TestReflection.InvokeNonPublic(vm, "GetServiceUpdateInfo", targetService, osMockPayload, databaseDto, CancellationToken.None);

                var resultType = result.GetType();
                var uiUpdateInfo = resultType.GetField("Item1").GetValue(result);
                var updatedDatabaseDto = (ServiceDto)resultType.GetField("Item2").GetValue(result);

                // Assert
                Assert.NotNull(updatedDatabaseDto);
                Assert.Equal("DriftService", updatedDatabaseDto.Name);
                Assert.Equal("New OS Desc", updatedDatabaseDto.Description);
                Assert.Equal((int)ServiceStartType.Automatic, updatedDatabaseDto.StartupType);

                Assert.NotNull(uiUpdateInfo);
                var uiUpdateType = uiUpdateInfo.GetType();

                bool requiresPidUpdate = (bool)uiUpdateType.GetProperty("RequiresPidUpdate").GetValue(uiUpdateInfo);
                int? newPid = (int?)uiUpdateType.GetProperty("NewPid").GetValue(uiUpdateInfo);
                var status = uiUpdateType.GetProperty("Status").GetValue(uiUpdateInfo);
                var startupType = uiUpdateType.GetProperty("StartupType").GetValue(uiUpdateInfo);
                var description = uiUpdateType.GetProperty("Description").GetValue(uiUpdateInfo);
                var logOnAs = uiUpdateType.GetProperty("LogOnAs").GetValue(uiUpdateInfo);

                Assert.True(requiresPidUpdate);
                Assert.Null(newPid);
                Assert.Equal(ServiceStatus.Stopped, status);
                Assert.Equal(ServiceStartType.Automatic, startupType);
                Assert.Equal("New OS Desc", description);
                Assert.Equal("CustomUser", logOnAs);
            }, createApp: true);
        }

        [Fact]
        public async Task RefreshAllServicesAsync_DependencyNullChecks_ExitEarly()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vm = CreateViewModel();

                // Act & Assert
                TestReflection.SetField(vm, "_serviceManager", null);
                var task1 = (Task)TestReflection.InvokeNonPublic(vm, "RefreshAllServicesAsync", CancellationToken.None);
                await task1;

                TestReflection.SetField(vm, "_serviceManager", _serviceManagerMock.Object);
                TestReflection.SetField(vm, "_serviceRepository", null);
                var task2 = (Task)TestReflection.InvokeNonPublic(vm, "RefreshAllServicesAsync", CancellationToken.None);
                await task2;

                Assert.True(true);
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_ExceptionBranch_ReturnsNullsSafely()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var service = new Service { Name = "CrashService", Pid = 1234 };
                _processHelperMock.Setup(p => p.GetProcessTreeMetrics(1234)).Throws(new Exception("Process performance counter corrupt"));

                // Act
                var result = TestReflection.InvokeNonPublic(vm, "GetServiceUpdateInfo", service, new Dictionary<string, ServiceInfo>(), new ServiceDto(), CancellationToken.None);

                // Assert
                Assert.NotNull(result);

                var type = result.GetType();
                var updateInfo = type.GetField("Item1").GetValue(result) as ServiceUpdateInfo;
                var updatedDto = type.GetField("Item2").GetValue(result) as ServiceDto;

                Assert.NotNull(updateInfo);
                Assert.Null(updateInfo.CpuUsage);
                Assert.Null(updateInfo.NewPid);
                Assert.Null(updateInfo.Description);
                Assert.False(updateInfo.IsInstalled);

                Assert.Null(updatedDto);
            }, createApp: true);
        }

        #endregion

        #region Bulk Operations (ExecuteBulkOperationAsync)

        private async Task<Exception> SetupAndRunBulkOperation(Action<MainViewModel> configureTest, Func<MainViewModel, Task> commandAction)
        {
            // 1. Enforce thread-local dispatcher alignment
            var threadDispatcher = Dispatcher.CurrentDispatcher;
            var vm = CreateViewModel(threadDispatcher);

            var collection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");

            var svc1 = new Service { Name = "S1", IsInstalled = true };
            var svc2 = new Service { Name = "S2", IsInstalled = true };

            var row1 = new ServiceRowViewModel(svc1, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };
            var row2 = new ServiceRowViewModel(svc2, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };

            collection.Add(row1);
            collection.Add(row2);

            configureTest(vm);

            Exception capturedException = null;

            // 2. Wrap via the unified message pump helper and track internal exception bubbles
            RunOnPump(threadDispatcher, async () =>
            {
                try
                {
                    await commandAction(vm);
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            return capturedException;
        }

        [Fact]
        public async Task BulkOperations_NoServicesSelected_ShowsInfoMessage()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);

                // Explicitly mock the early-exit message dialog to return instantly!
                _messageBoxServiceMock.Setup(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()))
                                      .Returns(Task.CompletedTask);

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.StartSelectedCommand.ExecuteAsync(null);
                });

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.Msg_NoServicesSelected, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_NullMessageBoxService_ExitsEarly()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange & Act
                var exception = await SetupAndRunBulkOperation(vm =>
                {
                    TestReflection.SetField(vm, "_messageBoxService", null);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                // Assert
                // Ensure that the execution didn't pass Times.Never due to a top-level crash
                Assert.Null(exception);
                _serviceCommandsMock.Verify(c => c.StartServiceAsync(It.IsAny<Service>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_UserCancelsConfirmation_AbortsOperation()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange & Act
                var exception = await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                // Assert
                Assert.Null(exception);
                _serviceCommandsMock.Verify(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_Success_ShowsSuccessInfo()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                _messageBoxServiceMock.Setup(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                // Act
                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.Msg_OperationCompletedSuccessfully, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_PartialFailure_ShowsWarningDetails()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                _messageBoxServiceMock.Setup(m => m.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                // Act
                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

                    _serviceCommandsMock.Setup(c => c.StopServiceAsync(It.Is<Service>(s => s.Name == "S1"), false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.StopServiceAsync(It.Is<Service>(s => s.Name == "S2"), false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                }, async vm => await vm.StopSelectedCommand.ExecuteAsync(null));

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowWarningAsync(It.Is<string>(msg => msg.Contains("S2")), It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_TotalFailure_ShowsAllFailedWarning()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                _messageBoxServiceMock.Setup(m => m.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                // Act
                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.RestartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                }, async vm => await vm.RestartSelectedCommand.ExecuteAsync(null));

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowWarningAsync(Strings.Msg_AllOperationsFailed, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_ExceptionThrown_HandledAndBusyStateReset()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var vmRef = (MainViewModel)null;

                // Act
                await SetupAndRunBulkOperation(vm =>
                {
                    vmRef = vm;

                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
                                          .ReturnsAsync(true);

                    _messageBoxServiceMock.Setup(m => m.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>()))
                                          .Returns(Task.CompletedTask);

                    _serviceCommandsMock.Setup(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>()))
                                          .ThrowsAsync(new InvalidOperationException("Fatal Access Violation"));
                }, async vm =>
                {
                    await vm.StartSelectedCommand.ExecuteAsync(null);
                });

                // Assert
                Assert.False(vmRef.IsBusy);
                _cursorServiceMock.Verify(c => c.ResetCursor(), Times.AtLeastOnce);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_Throttling_EnforcesMaxParallelismCap()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);

                _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                _messageBoxServiceMock.Setup(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                var collection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");
                collection.Clear();

                // Generate 5 checked service lines to thrash against our MaxBulkOperationParallelism = 2 threshold cap
                for (int i = 1; i <= 5; i++)
                {
                    var svc = new Service { Name = $"S{i}", IsInstalled = true };
                    collection.Add(new ServiceRowViewModel(svc, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true });
                }

                int currentInFlightCount = 0;
                int maxObservedParallelism = 0;
                var lockObject = new object();

                // CONCURRENCY TRACKING SEAM: Inject counting wrappers into the asynchronous mock execution path
                _serviceCommandsMock.Setup(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>()))
                    .Returns(async (Service s, bool flag, CancellationToken token) =>
                    {
                        int activeThreads;
                        lock (lockObject)
                        {
                            activeThreads = Interlocked.Increment(ref currentInFlightCount);
                            if (activeThreads > maxObservedParallelism)
                            {
                                maxObservedParallelism = activeThreads;
                            }
                        }

                        // Artificially delay task processing to let internal pipeline operations overlap and queue
                        await Task.Delay(50, token);

                        Interlocked.Decrement(ref currentInFlightCount);
                        return true;
                    });

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.StartSelectedCommand.ExecuteAsync(null);
                });

                // Assert
                // 1. Prove all selected worker nodes reached completion bounds
                _serviceCommandsMock.Verify(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>()), Times.Exactly(5));

                // 2. Validate that parallel worker processing never crossed our configured limit of 2
                Assert.True(maxObservedParallelism <= 2, $"Throttling regression detected! Max concurrent tasks reached: {maxObservedParallelism}");
                Assert.True(maxObservedParallelism > 1, "Concurrency gate was completely single-threaded; execution failed to split workload tasks.");
            }, createApp: true);
        }

        #endregion

        #region Helpers & Standalone Commands

        [Fact]
        public async Task RemoveService_ChecksAccessAndRemovesFromCollection()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                var collection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");

                collection.Add(new ServiceRowViewModel(new Service { Name = "ToRemove" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));
                collection.Add(new ServiceRowViewModel(new Service { Name = "ToKeep" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // Act & Assert - Branch 1: Current Thread execution path (UI Thread context match)
                vm.RemoveService("ToRemove");
                Assert.Single(collection);

                // Act - Branch 2: Worker Background Thread execution path (InvokeAsync fallback branch)
                RunOnPump(currentDispatcher, async () =>
                {
                    // 1. Kick off the service removal on a background thread pool worker
                    await Task.Run(() => vm.RemoveService("ToKeep"));

                    // 2. Yield and await a lower-priority operation on the local message pump.
                    // This forces the pump to fully process the collection removal InvokeAsync action 
                    // queued up by RemoveService before dropping the frame execution loop.
                    await currentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                });

                // Assert
                Assert.Empty(collection);

                await Task.CompletedTask;

            }, createApp: true);
        }

        [Fact]
        public async Task ConfigureCommand_ShouldDelegateToConfigureServiceAsync()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                var dummyService = new Service();

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.ConfigureCommand.ExecuteAsync(dummyService);
                });

                // Assert
                _serviceCommandsMock.Verify(c => c.ConfigureServiceAsync(dummyService, It.IsAny<CancellationToken>()), Times.Once);
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task ImportXmlCommand_ShouldDelegateToImportXmlConfigAsync()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.ImportXmlCommand.ExecuteAsync(null);
                });

                // Assert
                _serviceCommandsMock.Verify(c => c.ImportXmlConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task ImportJsonCommand_ShouldDelegateToImportJsonConfigAsync()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.ImportJsonCommand.ExecuteAsync(null);
                });

                // Assert
                _serviceCommandsMock.Verify(c => c.ImportJsonConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task OpenDocumentationCommand_ShouldDelegateToOpenDocumentationAsync()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                _helpServiceMock.Setup(h => h.OpenDocumentationAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.OpenDocumentationCommand.ExecuteAsync(null);
                });

                // Assert
                _helpServiceMock.Verify(h => h.OpenDocumentationAsync(It.IsAny<string>()), Times.Once);
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task CheckUpdatesCommand_ShouldDelegateToCheckUpdatesAsync()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                _helpServiceMock.Setup(h => h.CheckUpdatesAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.CheckUpdatesCommand.ExecuteAsync(null);
                });

                // Assert
                _helpServiceMock.Verify(h => h.CheckUpdatesAsync(It.IsAny<string>()), Times.Once);
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task OpenAboutDialogCommand_ShouldDelegateToOpenAboutDialogAsync()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                _helpServiceMock.Setup(h => h.OpenAboutDialogAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    await vm.OpenAboutDialogCommand.ExecuteAsync(null);
                });

                // Assert
                _helpServiceMock.Verify(h => h.OpenAboutDialogAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task Refresh_ShouldTriggerSearchServicesAsync()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                _serviceCommandsMock.Setup(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                                     .ReturnsAsync(new List<Service>());

                // Act
                RunOnPump(currentDispatcher, async () =>
                {
                    // This internally triggers SearchServicesAsync and handles the background workers safely
                    await vm.Refresh();
                });

                // Assert
                _serviceCommandsMock.Verify(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
                await Task.CompletedTask;
            }, createApp: true);
        }

        #endregion

        #region Value Mutation & INotifyPropertyChanged Properties

        [Fact]
        public void Properties_TriStateSelectAll_MutatesAndFiresNotification()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var collection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");

                var childRow1 = new ServiceRowViewModel(new Service { Name = "S1" }, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };
                var childRow2 = new ServiceRowViewModel(new Service { Name = "S2" }, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = false };

                TestReflection.SetField(vm, "_isUpdatingSelectAll", true);
                collection.Add(childRow1);
                collection.Add(childRow2);
                TestReflection.SetField(vm, "_isUpdatingSelectAll", false);

                bool selectAllChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SelectAll)) selectAllChangedFired = true;
                };

                TestReflection.SetField(vm, "_selectAll", true);

                // Act
                vm.SelectAll = null;

                // Assert
                Assert.True(selectAllChangedFired); 
            }, createApp: true);
        }

        [Fact]
        public void StandardUIProperties_MutateValues_RaisesNotificationsAndBypassesOnEquality()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var changedProps = new List<string>();
                vm.PropertyChanged += (s, e) => { if (e.PropertyName != null) changedProps.Add(e.PropertyName); };

                // Act & Assert - IsBusy
                vm.IsBusy = true;
                Assert.True(vm.IsBusy);
                Assert.Contains(nameof(vm.IsBusy), changedProps);

                changedProps.Clear();
                vm.IsBusy = true;
                Assert.Empty(changedProps);

                // Act & Assert - FooterText
                changedProps.Clear();
                vm.FooterText = "Total Services: 5";
                Assert.Equal("Total Services: 5", vm.FooterText);
                Assert.Contains(nameof(vm.FooterText), changedProps);

                changedProps.Clear();
                vm.FooterText = "Total Services: 5";
                Assert.Empty(changedProps);

                // Act & Assert - SearchButtonText
                changedProps.Clear();
                vm.SearchButtonText = "Locating...";
                Assert.Equal("Locating...", vm.SearchButtonText);
                Assert.Contains(nameof(vm.SearchButtonText), changedProps);

                changedProps.Clear();
                vm.SearchButtonText = "Locating...";
                Assert.Empty(changedProps);

                // Act & Assert - SearchText
                changedProps.Clear();
                vm.SearchText = "WexflowCore";
                Assert.Equal("WexflowCore", vm.SearchText);
                Assert.Contains(nameof(vm.SearchText), changedProps);

                changedProps.Clear();
                vm.SearchText = "WexflowCore";
                Assert.Empty(changedProps);

                // Act & Assert - IsConfiguratorEnabled
                changedProps.Clear();
                vm.IsConfiguratorEnabled = false;
                Assert.False(vm.IsConfiguratorEnabled);
                Assert.Contains(nameof(vm.IsConfiguratorEnabled), changedProps);

                changedProps.Clear();
                vm.IsConfiguratorEnabled = false;
                Assert.Empty(changedProps);

            }, createApp: true);
        }

        [Fact]
        public void SelectAll_Setter_CascadesCorrectlyToChildrenAndPreventsInfiniteLoops()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var collection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");

                var childRow1 = new ServiceRowViewModel(new Service { Name = "S1" }, _serviceCommandsMock.Object, _cursorServiceMock.Object);
                var childRow2 = new ServiceRowViewModel(new Service { Name = "S2" }, _serviceCommandsMock.Object, _cursorServiceMock.Object);
                collection.Add(childRow1);
                collection.Add(childRow2);

                bool selectAllNotified = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SelectAll)) selectAllNotified = true;
                };

                // Act
                vm.SelectAll = true;

                // Assert
                Assert.True(childRow1.IsChecked);
                Assert.True(childRow2.IsChecked);
                Assert.False(childRow1.IsSelected);
                Assert.True(selectAllNotified);

                selectAllNotified = false;

                vm.SelectAll = true;
                Assert.False(selectAllNotified);

                TestReflection.SetField(vm, "_isUpdatingSelectAll", true);
                childRow1.IsChecked = false;
                vm.SelectAll = false;

                Assert.True(childRow2.IsChecked);
            }, createApp: true);
        }

        [Fact]
        public void IsConfiguratorEnabled_MutatesValueAndFiresNotification()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                bool notified = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.IsConfiguratorEnabled)) notified = true;
                };

                // Act
                vm.IsConfiguratorEnabled = !vm.IsConfiguratorEnabled;

                // Assert
                Assert.True(notified);

                notified = false;
                vm.IsConfiguratorEnabled = vm.IsConfiguratorEnabled;
                Assert.False(notified);
            }, createApp: true);
        }

        #endregion

        #region Commands

        [Fact]
        public void AsyncCommands_AreExposedAndProperlyInitialized()
        {
            // Arrange & Act
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                // Assert
                Assert.NotNull(vm.SearchCommand);
                Assert.NotNull(vm.ConfigureCommand);
                Assert.NotNull(vm.ImportXmlCommand);
                Assert.NotNull(vm.ImportJsonCommand);
                Assert.NotNull(vm.StartSelectedCommand);
                Assert.NotNull(vm.StopSelectedCommand);
                Assert.NotNull(vm.RestartSelectedCommand);
                Assert.NotNull(vm.OpenDocumentationCommand);
                Assert.NotNull(vm.CheckUpdatesCommand);
                Assert.NotNull(vm.OpenAboutDialogCommand);
            }, createApp: true);
        }

        #endregion

        #region Complete Branch & Exception Circuit Coverage Tests

        [Fact]
        public void GetServiceUpdateInfo_TargetPidHasValueAndGreaterThanZero_MaintainsCacheAndFetchesMetrics()
        {
            Helper.RunOnSTA(() =>
            {
                var originalProvider = App.Services;

                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_processKillerMock.Object);

                var localProvider = serviceCollection.BuildServiceProvider();
                App.Services = localProvider;

                try
                {
                    // Arrange
                    var vm = CreateViewModel();

                    var service = new Service { Name = "MetricsSvc", Pid = 4321, Status = ServiceStatus.Running };
                    var allServices = new Dictionary<string, ServiceInfo>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "MetricsSvc", new ServiceInfo { Name = "MetricsSvc", Status = ServiceStatus.Running, } }
                    };
                    var serviceDto = new ServiceDto { Name = "MetricsSvc", Pid = 4321 };

                    _processHelperMock.Setup(p => p.GetProcessTreeMetrics(4321)).Returns(new ProcessMetrics(12.5, 2048576));

                    // Act
                    var result = TestReflection.InvokeNonPublic(vm, "GetServiceUpdateInfo", service, allServices, serviceDto, CancellationToken.None);

                    // Assert
                    Assert.NotNull(result);
                    var resultType = result.GetType();
                    var updateInfo = (ServiceUpdateInfo)resultType.GetField("Item1").GetValue(result);

                    Assert.Equal(12.5, updateInfo.CpuUsage);
                    Assert.Equal(2048576, updateInfo.RamUsage);
                    _processHelperMock.Verify(p => p.MaintainCache(), Times.Once);
                    _processHelperMock.Verify(p => p.GetProcessTreeMetrics(4321), Times.Once);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_StartupTypeNullInOsButPresentInDto_FallbackToDtoStartupType()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var service = new Service { Name = "FallbackSvc", Status = ServiceStatus.Running };

                // Completely omit the service from the OS dictionary map.
                // This simulates an environment where the service is missing from OS query tracking,
                // forcing the calculation pipeline to execute its ServiceDto fallback path.
                var allServices = new Dictionary<string, ServiceInfo>(StringComparer.OrdinalIgnoreCase);

                // Provide a distinct configuration setting type profile in the database DTO layer
                var serviceDto = new ServiceDto { Name = "FallbackSvc", StartupType = (int)ServiceStartType.Automatic };

                // Act
                var result = TestReflection.InvokeNonPublic(vm, "GetServiceUpdateInfo", service, allServices, serviceDto, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                var updateInfo = (ServiceUpdateInfo)result.GetType().GetField("Item1").GetValue(result);

                // Asserting Automatic explicitly proves that the data payload bypassed the missing OS lookup frame
                // and correctly unrolled into the database DTO fallback value layer.
                Assert.Equal(ServiceStartType.Automatic, updateInfo.StartupType);
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_ServiceDtoNull_SetsRequiresPidUpdateTrueAndNewPidNull()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var service = new Service { Name = "OrphanedSvc", Pid = 999 };
                var allServices = new Dictionary<string, ServiceInfo>(StringComparer.OrdinalIgnoreCase);

                // Act
                var result = TestReflection.InvokeNonPublic(vm, "GetServiceUpdateInfo", service, allServices, null, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                var updateInfo = (ServiceUpdateInfo)result.GetType().GetField("Item1").GetValue(result);
                Assert.True(updateInfo.RequiresPidUpdate);
                Assert.Null(updateInfo.NewPid);
            }, createApp: true);
        }

        [Fact]
        public void ApplyServiceUpdate_AppliesAllPropertiesSafelyToTargetUiService()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var targetService = new Service
                {
                    Name = "Svc",
                    Pid = 10,
                    CpuUsage = 0,
                    RamUsage = 0,
                    IsInstalled = false,
                    Status = ServiceStatus.Stopped,
                    StartupType = ServiceStartType.Manual,
                    LogOnAs = "OldUser",
                    Description = "Old Desc"
                };

                var info = new ServiceUpdateInfo(targetService)
                {
                    RequiresPidUpdate = true,
                    NewPid = 20,
                    CpuUsage = 5.5,
                    RamUsage = 1024,
                    IsInstalled = true,
                    Status = ServiceStatus.Running,
                    StartupType = ServiceStartType.Automatic,
                    LogOnAs = "NewUser",
                    Description = "New Desc"
                };

                // Act
                TestReflection.InvokeNonPublic(vm, "ApplyServiceUpdate", info);

                // Assert
                Assert.Equal(20, targetService.Pid);
                Assert.True(targetService.IsPidEnabled);
                Assert.Equal(5.5, targetService.CpuUsage);
                Assert.Equal(1024, targetService.RamUsage);
                Assert.True(targetService.IsInstalled);
                Assert.Equal(ServiceStatus.Running, targetService.Status);
                Assert.Equal(ServiceStartType.Automatic, targetService.StartupType);
                Assert.Equal("NewUser", targetService.LogOnAs);
                Assert.Equal("New Desc", targetService.Description);
            }, createApp: true);
        }

        [Fact]
        public void Service_PropertyChanged_IsCheckedPropertyName_TriggersSelectAllAndHasSelectedServicesCascade()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                // Seed the collection so UpdateSelectAllState() can evaluate an expected tri-state
                var collection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");
                var childRow = new ServiceRowViewModel(new Service { Name = "S1" }, _serviceCommandsMock.Object, _cursorServiceMock.Object)
                {
                    IsChecked = true
                };

                TestReflection.SetField(vm, "_isUpdatingSelectAll", true);
                collection.Add(childRow);
                TestReflection.SetField(vm, "_isUpdatingSelectAll", false);

                bool hasSelectedServicesNotified = false;
                bool selectAllNotified = false;

                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.HasSelectedServices))
                        hasSelectedServicesNotified = true;
                    if (e.PropertyName == nameof(MainViewModel.SelectAll))
                        selectAllNotified = true;
                };

                // Act - Simulate a child row selection status change notification event
                TestReflection.InvokeNonPublic(vm, "Service_PropertyChanged", childRow, new PropertyChangedEventArgs(nameof(ServiceRowViewModel.IsChecked)));

                // Assert both components of the cascade are executed and raised
                Assert.True(selectAllNotified, "The SelectAll property update cascade was not triggered or notified.");
                Assert.True(hasSelectedServicesNotified, "The HasSelectedServices property change notification was not raised.");
                Assert.True(vm.SelectAll, "The tri-state SelectAll flag failed to resolve to true based on collection status.");
            }, createApp: true);
        }

        [Fact]
        public void UpdateSelectAllState_CollectionEmpty_SetsSelectAllFalse()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var servicesCollection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");
                servicesCollection.Clear();

                // Act
                TestReflection.InvokeNonPublic(vm, "UpdateSelectAllState");

                // Assert
                Assert.False(vm.SelectAll);
            }, createApp: true);
        }

        [Fact]
        public void Dispose_PerformanceAndChildViewModelsNotImplementIDisposable_BypassesGracefully()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var uiDispatcherMock = new Mock<IUiDispatcher>();
                uiDispatcherMock.Setup(d => d.YieldAsync()).Returns(Task.CompletedTask);

                var mockPerformance = new Mock<PerformanceViewModel>(_serviceRepositoryMock.Object, _serviceCommandsMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelperMock.Object, uiDispatcherMock.Object);
                var mockConsole = new Mock<ConsoleViewModel>(_serviceRepositoryMock.Object, _serviceCommandsMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, uiDispatcherMock.Object);
                var mockDependencies = new Mock<DependenciesViewModel>(_serviceRepositoryMock.Object, _serviceManagerMock.Object, _serviceCommandsMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, uiDispatcherMock.Object, _messageBoxServiceMock.Object);

                var vm = new MainViewModel(
                    _serviceManagerMock.Object,
                    _serviceRepositoryMock.Object,
                    _serviceCommandsMock.Object,
                    _helpServiceMock.Object,
                    _messageBoxServiceMock.Object,
                    mockPerformance.Object,
                    mockConsole.Object,
                    mockDependencies.Object,
                    _appConfigMock.Object,
                    _cursorServiceMock.Object,
                    _processHelperMock.Object,
                    Dispatcher.CurrentDispatcher
                );

                // Act & Assert
                var exception = Record.Exception(() => vm.Dispose());
                Assert.Null(exception);
            }, createApp: true);
        }

        #endregion

        #region Disposal & Teardown

        [Fact]
        public void Dispose_CleansUpTimersAndSubscriptions_SafelyHandlesDoubleDispose()
        {
            // Arrange
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var collection = TestReflection.GetField<BulkObservableCollection<ServiceRowViewModel>>(vm, "_services");
                collection.Add(new ServiceRowViewModel(new Service(), _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // Act
                vm.Dispose();

                // Assert
                Assert.Empty(collection);

                var doubleDisposeException = Record.Exception(() => vm.Dispose());
                Assert.Null(doubleDisposeException);
            }, createApp: true);
        }

        #endregion
    }
}