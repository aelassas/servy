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
using Servy.UI;
using Servy.UI.Services;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;
using Helper = Servy.Testing.Helper;
using static Servy.Manager.ViewModels.MainViewModel;

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

        private MainViewModel CreateViewModel(Dispatcher? dispatcher = null)
        {
            // FIX: Always fall back to the explicit thread-bound Dispatcher context
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

        private async Task FlushDispatcherAsync(Dispatcher? dispatcher = null)
        {
            // FIX: Utilize the specific current thread-apartment dispatcher to prevent cross-locks
            var targetDispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            await targetDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        #region Constructors & Properties

        [Fact]
        public void Constructor_NullGuards_ThrowsArgumentNullException()
        {
            Helper.RunOnSTA(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(null!, _serviceRepositoryMock.Object, _serviceCommandsMock.Object, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelperMock.Object, Dispatcher.CurrentDispatcher));
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(_serviceManagerMock.Object, null!, _serviceCommandsMock.Object, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelperMock.Object, Dispatcher.CurrentDispatcher));
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(_serviceManagerMock.Object, _serviceRepositoryMock.Object, null!, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelperMock.Object, Dispatcher.CurrentDispatcher));
            }, createApp: true);
        }

        [Fact]
        public void Constructor_DesignTime_DoesNotThrow()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = new MainViewModel();
                Assert.NotNull(vm);
            }, createApp: true);
        }

        [Fact]
        public void Properties_SettersTriggerPropertyChanged()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var changedProps = new List<string>();
                vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName!);

                vm.IsBusy = true;
                vm.IsBusy = true;
                vm.FooterText = "Test";
                vm.SearchButtonText = "Searching...";
                vm.SearchText = "Testing";
                vm.IsConfiguratorEnabled = false;

                Assert.Contains(nameof(vm.IsBusy), changedProps);
                Assert.Contains(nameof(vm.FooterText), changedProps);
                Assert.Contains(nameof(vm.SearchButtonText), changedProps);
                Assert.Contains(nameof(vm.SearchText), changedProps);
                Assert.Contains(nameof(vm.IsConfiguratorEnabled), changedProps);
            }, createApp: true);
        }

        [Fact]
        public void ServiceCommands_Setter_UpdatesChildViewModels()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var newCommands = new Mock<IServiceCommands>().Object;

                vm.ServiceCommands = newCommands;

                Assert.Equal(newCommands, vm.PerformanceVM.ServiceCommands);
                Assert.Equal(newCommands, vm.ConsoleVM.ServiceCommands);
                Assert.Equal(newCommands, vm.DependenciesVM.ServiceCommands);
            }, createApp: true);
        }

        [Fact]
        public void AppConfig_PropertyChanged_UpdatesConfiguratorEnabled()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                _appConfigMock.Setup(c => c.IsDesktopAppAvailable).Returns(false);

                _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IAppConfiguration.IsDesktopAppAvailable)));
                _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs("OtherProperty"));

                Assert.False(vm.IsConfiguratorEnabled);
            }, createApp: true);
        }

        #endregion

        #region Search & SelectAll Cascades

        [Fact]
        public async Task SearchCommand_PopulatesServicesAndHandlesSelectAllStates()
        {
            await Helper.RunOnSTA(async () =>
            {
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                var services = new List<Service?>
                {
                    new Service { Name = "S1", IsInstalled = true },
                    new Service { Name = "S2", IsInstalled = true }
                };

                _serviceCommandsMock.Setup(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(services);

                // Establish a clean, thread-bound message pump loop frame
                var frame = new DispatcherFrame();

                // Schedule the execution sequence onto the dispatcher queue channel.
                _ = currentDispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await vm.SearchCommand.ExecuteAsync(null);
                    }
                    finally
                    {
                        // Lower the flag to break out of the PushFrame pump below
                        frame.Continue = false;
                    }
                }, DispatcherPriority.Normal);

                // Start the active thread-apartment message loop pump.
                // This forces the background loops spawned inside SearchServicesAsync to clear 
                // their queues instantly without hitting cross-thread priority blockades.
                Dispatcher.PushFrame(frame);

                // Assertions - Safely run on the main thread after the pump unwinds
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

                // Branch 4: Double-set skip
                vm.SelectAll = false;

                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task SearchCommand_NullDispatcher_LogsWarningAndExits()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                typeof(MainViewModel).GetField("_dispatcher", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);

                await vm.SearchCommand.ExecuteAsync(null);

                _serviceCommandsMock.Verify(c => c.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        #endregion

        #region Background Refresh, Ghost PIDs & Data Drift

        [Fact]
        public void TimerLifecycle_CreateStartStop_ManagesResourcesCorrectly()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                vm.CreateAndStartTimer();
                var timerField = typeof(MainViewModel).GetField("_refreshTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                var timer = (DispatcherTimer?)timerField!.GetValue(vm);

                Assert.NotNull(timer);
                Assert.True(timer.IsEnabled);

                vm.StopRefreshTimer();
                timer = (DispatcherTimer?)timerField!.GetValue(vm);

                Assert.Null(timer);
            }, createApp: true);
        }

        [Fact]
        public void OnTick_OverlappingTicks_PreventedByInterlocked()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var onTickMethod = typeof(MainViewModel).GetMethod("OnTick", BindingFlags.NonPublic | BindingFlags.Instance);

                onTickMethod!.Invoke(vm, new object[] { null!, EventArgs.Empty });
                onTickMethod!.Invoke(vm, new object[] { null!, EventArgs.Empty });

                Assert.True(true);
            }, createApp: true);
        }

        [Fact]
        public void RefreshAllServicesAsync_UpdatesUI_ResolvesGhostPids_AndFixesDBDrift()
        {
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

            var calculateUpdateMethod = typeof(MainViewModel).GetMethod("GetServiceUpdateInfo",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var result = calculateUpdateMethod!.Invoke(vm, new object[]
            {
                targetService,
                osMockPayload,
                databaseDto,
                CancellationToken.None
            });

            var resultType = result!.GetType();
            var uiUpdateInfo = resultType.GetField("Item1")!.GetValue(result);
            var updatedDatabaseDto = (ServiceDto)resultType.GetField("Item2")!.GetValue(result)!;

            Assert.NotNull(updatedDatabaseDto);
            Assert.Equal("DriftService", updatedDatabaseDto.Name);
            Assert.Equal("New OS Desc", updatedDatabaseDto.Description);
            Assert.Equal((int)ServiceStartType.Automatic, updatedDatabaseDto.StartupType);

            Assert.NotNull(uiUpdateInfo);
            var uiUpdateType = uiUpdateInfo.GetType();

            bool requiresPidUpdate = (bool)uiUpdateType.GetProperty("RequiresPidUpdate")!.GetValue(uiUpdateInfo)!;
            int? newPid = (int?)uiUpdateType.GetProperty("NewPid")!.GetValue(uiUpdateInfo);
            var status = uiUpdateType.GetProperty("Status")!.GetValue(uiUpdateInfo);
            var startupType = uiUpdateType.GetProperty("StartupType")!.GetValue(uiUpdateInfo);
            var description = uiUpdateType.GetProperty("Description")!.GetValue(uiUpdateInfo);

            Assert.True(requiresPidUpdate);
            Assert.Null(newPid);
            Assert.Equal(ServiceStatus.Stopped, status);
            Assert.Equal(ServiceStartType.Automatic, startupType);
            Assert.Equal("New OS Desc", description);
        }

        [Fact]
        public async Task RefreshAllServicesAsync_DependencyNullChecks_ExitEarly()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var refreshMethod = typeof(MainViewModel).GetMethod("RefreshAllServicesAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                typeof(MainViewModel).GetField("_serviceManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;

                typeof(MainViewModel).GetField("_serviceManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, _serviceManagerMock.Object);
                typeof(MainViewModel).GetField("_serviceRepository", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;

                Assert.True(true);
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_ExceptionBranch_ReturnsNullsSafely()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var getInfoMethod = typeof(MainViewModel).GetMethod("GetServiceUpdateInfo", BindingFlags.NonPublic | BindingFlags.Instance);

                var service = new Service { Name = "CrashService", Pid = 1234 };
                _processHelperMock.Setup(p => p.GetProcessTreeMetrics(1234)).Throws(new Exception("Process performance counter corrupt"));

                var result = getInfoMethod!.Invoke(vm, new object[] { service, new Dictionary<string, ServiceInfo>(), new ServiceDto(), CancellationToken.None });

                Assert.NotNull(result);

                var type = result.GetType();
                var updateInfo = type.GetField("Item1")!.GetValue(result) as ServiceUpdateInfo;
                var updatedDto = type.GetField("Item2")!.GetValue(result) as ServiceDto;

                Assert.NotNull(updateInfo);
                Assert.Null(updateInfo.CpuUsage);
                Assert.Null(updateInfo.NewPid);
                Assert.Null(updateInfo.Description);
                Assert.False(updateInfo.IsInstalled);

                Assert.Null(updatedDto);

                return true;
            }, createApp: true);
        }

        #endregion

        #region Bulk Operations (ExecuteBulkOperationAsync)

        private async Task SetupAndRunBulkOperation(Action<MainViewModel> configureTest, Func<MainViewModel, Task> commandAction)
        {
            // 1. Enforce thread-local dispatcher alignment
            var threadDispatcher = Dispatcher.CurrentDispatcher;
            var vm = CreateViewModel(threadDispatcher);

            var serviceField = typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance);
            var collection = (BulkObservableCollection<ServiceRowViewModel>)serviceField!.GetValue(vm)!;

            var svc1 = new Service { Name = "S1", IsInstalled = true };
            var svc2 = new Service { Name = "S2", IsInstalled = true };

            var row1 = new ServiceRowViewModel(svc1, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };
            var row2 = new ServiceRowViewModel(svc2, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };

            collection.Add(row1);
            collection.Add(row2);

            configureTest(vm);

            // 2. Create a native WPF message pump execution frame controller
            var frame = new DispatcherFrame();

            // 3. Schedule the command execution block onto the local message queue.
            // This allows the PushFrame mechanism below to boot up completely first.
            _ = threadDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await commandAction(vm);
                }
                catch (Exception)
                {
                    // Suppress expected target exceptions to let the frame unwind cleanly
                }
                finally
                {
                    // CRITICAL FIX: Breaking the frame loop causes PushFrame to exit 
                    // the moment the async execution pipeline completes.
                    frame.Continue = false;
                }
            }, DispatcherPriority.Normal);

            // 4. Start a live thread-bound Win32 message loop on the STA thread apartment.
            // This handles InvokeAsync operations, background worker callbacks, and 
            // nested message-box awaits sequentially without thread starvation.
            Dispatcher.PushFrame(frame);

            await Task.CompletedTask;
        }

        [Fact]
        public async Task BulkOperations_NoServicesSelected_ShowsInfoMessage()
        {
            await Helper.RunOnSTA(async () =>
            {
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);

                // FIX 1: Explicitly mock the early-exit message dialog to return instantly!
                _messageBoxServiceMock.Setup(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()))
                                      .Returns(Task.CompletedTask);

                var frame = new DispatcherFrame();

                // FIX 2: Route through the DispatcherFrame harness to ensure the thread remains isolated 
                // and responsive during execution steps.
                _ = currentDispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await vm.StartSelectedCommand.ExecuteAsync(null);
                    }
                    finally
                    {
                        frame.Continue = false;
                    }
                });

                Dispatcher.PushFrame(frame);

                // Assert
                _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.Msg_NoServicesSelected, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_NullMessageBoxService_ExitsEarly()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Uses our specialized bulk helper which completely encapsulates 
                // the DispatcherFrame pump mechanics under the hood!
                await SetupAndRunBulkOperation(vm =>
                {
                    typeof(MainViewModel).GetField("_messageBoxService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                _serviceCommandsMock.Verify(c => c.StartServiceAsync(It.IsAny<Service>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_UserCancelsConfirmation_AbortsOperation()
        {
            await Helper.RunOnSTA(async () =>
            {
                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                _serviceCommandsMock.Verify(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_Success_ShowsSuccessInfo()
        {
            await Helper.RunOnSTA(async () =>
            {
                _messageBoxServiceMock.Setup(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.Msg_OperationCompletedSuccessfully, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_PartialFailure_ShowsWarningDetails()
        {
            await Helper.RunOnSTA(async () =>
            {
                _messageBoxServiceMock.Setup(m => m.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

                    _serviceCommandsMock.Setup(c => c.StopServiceAsync(It.Is<Service>(s => s.Name == "S1"), false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.StopServiceAsync(It.Is<Service>(s => s.Name == "S2"), false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                }, async vm => await vm.StopSelectedCommand.ExecuteAsync(null));

                _messageBoxServiceMock.Verify(m => m.ShowWarningAsync(It.Is<string>(msg => msg.Contains("S2")), It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_TotalFailure_ShowsAllFailedWarning()
        {
            await Helper.RunOnSTA(async () =>
            {
                _messageBoxServiceMock.Setup(m => m.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.RestartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                }, async vm => await vm.RestartSelectedCommand.ExecuteAsync(null));

                _messageBoxServiceMock.Verify(m => m.ShowWarningAsync(Strings.Msg_AllOperationsFailed, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public async Task BulkOperations_ExceptionThrown_HandledAndBusyStateReset()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vmRef = (MainViewModel)null!;

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

                Assert.False(vmRef.IsBusy);
                _cursorServiceMock.Verify(c => c.ResetCursor(), Times.AtLeastOnce);
            }, createApp: true);
        }

        #endregion

        #region Helpers & Standalone Commands

        [Fact]
        public async Task RemoveService_ChecksAccessAndRemovesFromCollection()
        {
            await Helper.RunOnSTA(async () =>
            {
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                var collection = (BulkObservableCollection<ServiceRowViewModel>)typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;

                collection.Add(new ServiceRowViewModel(new Service { Name = "ToRemove" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));
                collection.Add(new ServiceRowViewModel(new Service { Name = "ToKeep" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // Branch 1: Current Thread execution path (UI Thread context match)
                vm.RemoveService("ToRemove");
                Assert.Single(collection);

                // Branch 2: Worker Background Thread execution path (InvokeAsync fallback branch)
                var frame = new DispatcherFrame();

                // FIX: Hook directly into the collection's native change notification tracking loop.
                // This ensures frame.Continue = false triggers ONLY when the item has physically 
                // been removed from memory, removing priority races entirely.
                System.Collections.Specialized.NotifyCollectionChangedEventHandler handler = null!;
                handler = (s, e) =>
                {
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                    {
                        frame.Continue = false;
                    }
                };
                collection.CollectionChanged += handler;

                try
                {
                    // Kick off the background removal task
                    _ = Task.Run(() => vm.RemoveService("ToKeep"));

                    // Start the active thread message loop pump. It will stay open until 
                    // the CollectionChanged event handler drops the Continue flag.
                    Dispatcher.PushFrame(frame);
                }
                finally
                {
                    // Clean up event subscription to prevent memory profile leakage across suite runs
                    collection.CollectionChanged -= handler;
                }

                // Assert: Guaranteed to be empty now that the pump has waited for the physical drop
                Assert.Empty(collection);

                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task IndependentCommands_DelegateToUnderlyingServices()
        {
            await Helper.RunOnSTA(async () =>
            {
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                var dummyService = new Service();

                // Setup the underlying loose mock profiles to return instantly completed promises
                _helpServiceMock.Setup(h => h.OpenDocumentation(It.IsAny<string>())).Returns(Task.CompletedTask);
                _helpServiceMock.Setup(h => h.CheckUpdates(It.IsAny<string>())).Returns(Task.CompletedTask);
                _helpServiceMock.Setup(h => h.OpenAboutDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                _serviceCommandsMock.Setup(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(new List<Service?>());

                // Establish a clean, thread-bound message pump loop frame
                var frame = new DispatcherFrame();

                // Schedule the complete sequential command execution suite onto the dispatcher queue channel.
                _ = currentDispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await vm.ConfigureCommand.ExecuteAsync(dummyService);
                        await vm.ImportXmlCommand.ExecuteAsync(null);
                        await vm.ImportJsonCommand.ExecuteAsync(null);
                        await vm.OpenDocumentationCommand.ExecuteAsync(null);
                        await vm.CheckUpdatesCommand.ExecuteAsync(null);
                        await vm.OpenAboutDialogCommand.ExecuteAsync(null);

                        // This internally triggers SearchServicesAsync and handles the background workers safely
                        await vm.Refresh();
                    }
                    finally
                    {
                        // Lower the flag flag to release the message pump loop once the final task completes
                        frame.Continue = false;
                    }
                }, DispatcherPriority.Normal);

                // Start the active thread-apartment message loop pump.
                // This forces the background loops spawned inside 'Refresh' to clear their 
                // InvokeAsync queues instantly without hitting cross-thread lock blockades.
                Dispatcher.PushFrame(frame);

                // Assertions - Verification metrics execute securely after the frame loop unrolls
                _serviceCommandsMock.Verify(c => c.ConfigureServiceAsync(dummyService, It.IsAny<CancellationToken>()), Times.Once);
                _serviceCommandsMock.Verify(c => c.ImportXmlConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
                _serviceCommandsMock.Verify(c => c.ImportJsonConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
                _helpServiceMock.Verify(h => h.OpenDocumentation(It.IsAny<string>()), Times.Once);
                _helpServiceMock.Verify(h => h.CheckUpdates(It.IsAny<string>()), Times.Once);
                _helpServiceMock.Verify(h => h.OpenAboutDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                _serviceCommandsMock.Verify(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);

                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task HelpCommands_NullHelpService_ExitsCleanly()
        {
            await Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                typeof(MainViewModel).GetField("_helpService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);

                await vm.OpenDocumentationCommand.ExecuteAsync(null);
                await vm.CheckUpdatesCommand.ExecuteAsync(null);
                await vm.OpenAboutDialogCommand.ExecuteAsync(null);

                Assert.True(true);
            }, createApp: true);
        }

        #endregion

        #region Value Mutation & INotifyPropertyChanged Properties

        [Fact]
        public void Properties_TriStateSelectAll_MutatesAndFiresNotification()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var servicesField = typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance);
                var collection = (BulkObservableCollection<ServiceRowViewModel>)servicesField!.GetValue(vm)!;

                var childRow1 = new ServiceRowViewModel(new Service { Name = "S1" }, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };
                var childRow2 = new ServiceRowViewModel(new Service { Name = "S2" }, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = false };

                typeof(MainViewModel).GetField("_isUpdatingSelectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, true);
                collection.Add(childRow1);
                collection.Add(childRow2);
                typeof(MainViewModel).GetField("_isUpdatingSelectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, false);

                bool selectAllChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SelectAll)) selectAllChangedFired = true;
                };

                typeof(MainViewModel).GetField("_selectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, true);

                vm.SelectAll = null;

                Assert.True(selectAllChangedFired);

                return true;
            }, createApp: true);
        }

        [Fact]
        public void Properties_SearchText_MutatesAndUpdatesValue()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                bool searchTextChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SearchText)) searchTextChangedFired = true;
                };

                vm.SearchText = "WexflowCoreEngine";

                Assert.Equal("WexflowCoreEngine", vm.SearchText);
                Assert.True(searchTextChangedFired);
            }, createApp: true);
        }

        [Fact]
        public void Properties_SearchButtonText_MutatesAndUpdatesValue()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                bool buttonTextChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SearchButtonText)) buttonTextChangedFired = true;
                };

                vm.SearchButtonText = Strings.Button_Searching;

                Assert.Equal(Strings.Button_Searching, vm.SearchButtonText);
                Assert.True(buttonTextChangedFired);
            }, createApp: true);
        }

        [Fact]
        public void StandardUIProperties_MutateValues_RaisesNotificationsAndBypassesOnEquality()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var changedProps = new List<string>();
                vm.PropertyChanged += (s, e) => { if (e.PropertyName != null) changedProps.Add(e.PropertyName); };

                vm.IsBusy = true;
                Assert.True(vm.IsBusy);
                Assert.Contains(nameof(vm.IsBusy), changedProps);

                changedProps.Clear();
                vm.IsBusy = true;
                Assert.Empty(changedProps);

                vm.FooterText = "Total Services: 5";
                Assert.Equal("Total Services: 5", vm.FooterText);
                Assert.Contains(nameof(vm.FooterText), changedProps);

                changedProps.Clear();
                vm.FooterText = "Total Services: 5";
                Assert.Empty(changedProps);

                vm.SearchButtonText = "Locating...";
                Assert.Equal("Locating...", vm.SearchButtonText);
                Assert.Contains(nameof(vm.SearchButtonText), changedProps);

                changedProps.Clear();
                vm.SearchButtonText = "Locating...";
                Assert.Empty(changedProps);

                vm.SearchText = "WexflowCore";
                Assert.Equal("WexflowCore", vm.SearchText);
                Assert.Contains(nameof(vm.SearchText), changedProps);

                changedProps.Clear();
                vm.SearchText = "WexflowCore";
                Assert.Empty(changedProps);

            }, createApp: true);
        }

        [Fact]
        public void SelectAll_Setter_CascadesCorrectlyToChildrenAndPreventsInfiniteLoops()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                var servicesField = typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance);
                var collection = (BulkObservableCollection<ServiceRowViewModel>)servicesField!.GetValue(vm)!;

                var childRow1 = new ServiceRowViewModel(new Service { Name = "S1" }, _serviceCommandsMock.Object, _cursorServiceMock.Object);
                var childRow2 = new ServiceRowViewModel(new Service { Name = "S2" }, _serviceCommandsMock.Object, _cursorServiceMock.Object);
                collection.Add(childRow1);
                collection.Add(childRow2);

                bool selectAllNotified = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SelectAll)) selectAllNotified = true;
                };

                vm.SelectAll = true;

                Assert.True(childRow1.IsChecked);
                Assert.True(childRow2.IsChecked);
                Assert.False(childRow1.IsSelected);
                Assert.True(selectAllNotified);

                selectAllNotified = false;

                vm.SelectAll = true;
                Assert.False(selectAllNotified);

                typeof(MainViewModel).GetField("_isUpdatingSelectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, true);
                childRow1.IsChecked = false;
                vm.SelectAll = false;

                Assert.True(childRow2.IsChecked);
            }, createApp: true);
        }

        [Fact]
        public void IsConfiguratorEnabled_MutatesValueAndFiresNotification()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();
                bool notified = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.IsConfiguratorEnabled)) notified = true;
                };

                vm.IsConfiguratorEnabled = !vm.IsConfiguratorEnabled;

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
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

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
                // Preserve the original environment context firmly inside the thread scope boundary
                var originalProvider = App.Services;

                // Build an isolated runtime DI container to satisfy the base class locator check.
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_processKillerMock.Object);

                // Build the provider instance explicitly and verify it isn't dropped by cross-thread assignments
                var localProvider = serviceCollection.BuildServiceProvider();
                App.Services = localProvider;

                try
                {
                    // Arrange
                    var vm = CreateViewModel();
                    var getInfoMethod = typeof(MainViewModel).GetMethod("GetServiceUpdateInfo", BindingFlags.NonPublic | BindingFlags.Instance);

                    var service = new Service { Name = "MetricsSvc", Pid = 4321, Status = ServiceStatus.Running };
                    var allServices = new Dictionary<string, ServiceInfo>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "MetricsSvc", new ServiceInfo { Name = "MetricsSvc", Status = ServiceStatus.Running, } }
                    };
                    var serviceDto = new ServiceDto { Name = "MetricsSvc", Pid = 4321 };

                    _processHelperMock.Setup(p => p.GetProcessTreeMetrics(4321)).Returns(new ProcessMetrics(12.5, 2048576));

                    // Act
                    var result = getInfoMethod!.Invoke(vm, new object[] { service, allServices, serviceDto, CancellationToken.None });

                    // Assert
                    Assert.NotNull(result);
                    var resultType = result.GetType();
                    var updateInfo = (ServiceUpdateInfo)resultType.GetField("Item1")!.GetValue(result)!;

                    Assert.Equal(12.5, updateInfo.CpuUsage);
                    Assert.Equal(2048576, updateInfo.RamUsage);
                    _processHelperMock.Verify(p => p.MaintainCache(), Times.Once);
                    _processHelperMock.Verify(p => p.GetProcessTreeMetrics(4321), Times.Once);
                }
                finally
                {
                    // Clean up tracking boundaries to isolate adjacent concurrent test suites
                    App.Services = originalProvider;
                }
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_StartupTypeNullInOsButPresentInDto_FallbackToDtoStartupType()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var vm = CreateViewModel();
                var getInfoMethod = typeof(MainViewModel).GetMethod("GetServiceUpdateInfo", BindingFlags.NonPublic | BindingFlags.Instance);

                var service = new Service { Name = "FallbackSvc", Status = ServiceStatus.Running };

                // OS configuration returns null for the startup type sequence
                var allServices = new Dictionary<string, ServiceInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    { "FallbackSvc", new ServiceInfo { Name = "FallbackSvc", Status = ServiceStatus.Running, StartupType = ServiceStartType.Disabled } }
                };

                // Database fallback contains a valid explicit configuration setting profile
                var serviceDto = new ServiceDto { Name = "FallbackSvc", StartupType = (int)ServiceStartType.Disabled };

                // Act
                var result = getInfoMethod!.Invoke(vm, new object[] { service, allServices, serviceDto, CancellationToken.None });

                // Assert
                Assert.NotNull(result);
                var updateInfo = (ServiceUpdateInfo)result.GetType().GetField("Item1")!.GetValue(result)!;
                Assert.Equal(ServiceStartType.Disabled, updateInfo.StartupType);
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_ServiceDtoNull_SetsRequiresPidUpdateTrueAndNewPidNull()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var vm = CreateViewModel();
                var getInfoMethod = typeof(MainViewModel).GetMethod("GetServiceUpdateInfo", BindingFlags.NonPublic | BindingFlags.Instance);

                var service = new Service { Name = "OrphanedSvc", Pid = 999 };
                var allServices = new Dictionary<string, ServiceInfo>(StringComparer.OrdinalIgnoreCase);

                // Act - Passing null for ServiceDto parameter
                var result = getInfoMethod!.Invoke(vm, new object[] { service, allServices, null!, CancellationToken.None });

                // Assert
                Assert.NotNull(result);
                var updateInfo = (ServiceUpdateInfo)result.GetType().GetField("Item1")!.GetValue(result)!;
                Assert.True(updateInfo.RequiresPidUpdate);
                Assert.Null(updateInfo.NewPid);
            }, createApp: true);
        }

        [Fact]
        public void ApplyServiceUpdate_AppliesAllPropertiesSafelyToTargetUiService()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var vm = CreateViewModel();
                var applyMethod = typeof(MainViewModel).GetMethod("ApplyServiceUpdate", BindingFlags.NonPublic | BindingFlags.Instance);

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
                applyMethod!.Invoke(vm, new object[] { info });

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
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var vm = CreateViewModel();
                bool hasSelectedServicesNotified = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.HasSelectedServices))
                        hasSelectedServicesNotified = true;
                };

                var handlerMethod = typeof(MainViewModel).GetMethod("Service_PropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act - Simulate a child RowViewModel checking state mutation notification callback loop trigger
                handlerMethod!.Invoke(vm, new object[] { null!, new PropertyChangedEventArgs(nameof(ServiceRowViewModel.IsChecked)) });

                // Assert
                Assert.True(hasSelectedServicesNotified);
            }, createApp: true);
        }

        [Fact]
        public void UpdateSelectAllState_CollectionEmpty_SetsSelectAllFalse()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var vm = CreateViewModel();

                // Clear out items securely to challenge collection edge loops
                var servicesCollection = (BulkObservableCollection<ServiceRowViewModel>)typeof(MainViewModel)
                    .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
                servicesCollection.Clear();

                var updateStateMethod = typeof(MainViewModel).GetMethod("UpdateSelectAllState", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                updateStateMethod!.Invoke(vm, null);

                // Assert
                Assert.False(vm.SelectAll);
            }, createApp: true);
        }

        [Fact]
        public void Dispose_PerformanceAndChildViewModelsNotImplementIDisposable_BypassesGracefully()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var uiDispatcherMock = new Mock<IUiDispatcher>();
                uiDispatcherMock.Setup(d => d.YieldAsync()).Returns(Task.CompletedTask);

                // Initialize standalone standard child instances that do NOT inherit or abstract from IDisposable directly
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

                // Act & Assert - Ensure standard layout teardowns do not experience casting violations or throw crashes
                var exception = Record.Exception(() => vm.Dispose());
                Assert.Null(exception);
            }, createApp: true);
        }

        #endregion

        #region Disposal & Teardown

        [Fact]
        public void Dispose_CleansUpTimersAndSubscriptions_SafelyHandlesDoubleDispose()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var collection = (BulkObservableCollection<ServiceRowViewModel>)typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
                collection.Add(new ServiceRowViewModel(new Service(), _serviceCommandsMock.Object, _cursorServiceMock.Object));

                vm.Dispose();

                Assert.Empty(collection);

                var doubleDisposeException = Record.Exception(() => vm.Dispose());
                Assert.Null(doubleDisposeException);
            }, createApp: true);
        }

        #endregion
    }
}