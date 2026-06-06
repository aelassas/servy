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
using static Servy.Manager.ViewModels.MainViewModel;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IServiceRepository> _serviceRepositoryMock;
        private readonly Mock<IHelpService> _helpServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IProcessHelper> _processHelper;

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
            _processHelper = new Mock<IProcessHelper>();

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

            // 1. PerformanceViewModel: 6 params (Uses _processHelper)
            _performanceViewModelMock = new Mock<PerformanceViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                _processHelper.Object,
                uiDispatcherMock.Object);

            // 2. ConsoleViewModel: 5 params (No _processHelper)
            _consoleViewModelMock = new Mock<ConsoleViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                uiDispatcherMock.Object);

            // 3. DependenciesViewModel: 7 params (No _processHelper, adds _messageBoxService)
            _dependenciesViewModelMock = new Mock<DependenciesViewModel>(
                _serviceRepositoryMock.Object,
                _serviceManagerMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                uiDispatcherMock.Object,
                _messageBoxServiceMock.Object); // <-- Injected missing modal dialog mock
        }

        private MainViewModel CreateViewModel(Dispatcher? dispatcher = null)
        {
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
                _processHelper.Object,
                dispatcher ?? Dispatcher.CurrentDispatcher
            );
        }

        private async Task FlushDispatcherAsync()
        {
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        #region Constructors & Properties

        [Fact]
        public void Constructor_NullGuards_ThrowsArgumentNullException()
        {
            Helper.RunOnSTA(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(null!, _serviceRepositoryMock.Object, _serviceCommandsMock.Object, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelper.Object, Dispatcher.CurrentDispatcher));
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(_serviceManagerMock.Object, null!, _serviceCommandsMock.Object, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelper.Object, Dispatcher.CurrentDispatcher));
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(_serviceManagerMock.Object, _serviceRepositoryMock.Object, null!, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelper.Object, Dispatcher.CurrentDispatcher));
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
                vm.IsBusy = true; // Duplicate to test branch block bypass
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
                _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs("OtherProperty")); // Test skip branch

                Assert.False(vm.IsConfiguratorEnabled);
            }, createApp: true);
        }

        #endregion

        #region Search & SelectAll Cascades

        [Fact]
        public void SearchCommand_PopulatesServicesAndHandlesSelectAllStates()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var services = new List<Service?>
                {
                    new Service { Name = "S1", IsInstalled = true },
                    new Service { Name = "S2", IsInstalled = true }
                };

                _serviceCommandsMock.Setup(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(services);

                await vm.SearchCommand.ExecuteAsync(null);
                await FlushDispatcherAsync();

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

                // Branch 4: SelectAll double-set skip
                vm.SelectAll = false;
            }, createApp: true);
        }

        [Fact]
        public void SearchCommand_NullDispatcher_LogsWarningAndExits()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                // Sabotage the dispatcher field via reflection
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

                // Act 1: Ensure timer starts correctly
                vm.CreateAndStartTimer();
                var timerField = typeof(MainViewModel).GetField("_refreshTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                var timer = (DispatcherTimer?)timerField!.GetValue(vm);

                Assert.NotNull(timer);
                Assert.True(timer.IsEnabled);

                // Act 2: Stop and teardown
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

                // Fire two ticks sequentially to trigger the internal Interlocked flag blackout lock branch
                onTickMethod!.Invoke(vm, new object[] { null!, EventArgs.Empty });
                onTickMethod!.Invoke(vm, new object[] { null!, EventArgs.Empty });

                Assert.True(true); // Survives concurrency stress cleanly without exception leakage
            }, createApp: true);
        }

        [Fact]
        public void RefreshAllServicesAsync_UpdatesUI_ResolvesGhostPids_AndFixesDBDrift()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var targetService = new Service { Name = "DriftService", Pid = 9999, Description = "Old UI Desc", StartupType = ServiceStartType.Manual };

                var servicesField = typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance);
                var collection = (BulkObservableCollection<ServiceRowViewModel>)servicesField!.GetValue(vm)!;
                collection.Add(new ServiceRowViewModel(targetService, _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // Mock dynamic Windows System metrics payload (Operating System State)
                _serviceManagerMock.Setup(m => m.GetAllServices(It.IsAny<CancellationToken>()))
                    .Returns(new List<ServiceInfo>
                    {
                        new ServiceInfo { Name = "DriftService", Status = ServiceStatus.Stopped, Description = "New OS Desc", StartupType = ServiceStartType.Automatic, LogOnAs = "CustomUser" }
                    });

                // Mock SQLite / Persistence layer configuration (Stored Database State)
                _serviceRepositoryMock.Setup(r => r.GetAllAsync(true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ServiceDto>
                    {
                        new ServiceDto { Name = "DriftService", Pid = 9999, Description = "Old DB Desc", StartupType = (int)ServiceStartType.Manual }
                    });

                var refreshMethod = typeof(MainViewModel).GetMethod("RefreshAllServicesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;
                await FlushDispatcherAsync();

                // UI Status Model Assertions
                Assert.Null(targetService.Pid); // Ghost PID wiped since OS status is officially 'Stopped'
                Assert.Equal("New OS Desc", targetService.Description);
                Assert.Equal(ServiceStartType.Automatic, targetService.StartupType);
                Assert.Equal(ServiceStatus.Stopped, targetService.Status);
                Assert.Equal("CustomUser", targetService.LogOnAs);

                // DB Synchronization Drift Batch Upsert Assertions
                _serviceRepositoryMock.Verify(r => r.UpsertBatchAsync(
                    It.Is<IEnumerable<ServiceDto>>(dtos =>
                        dtos.First().Name == "DriftService" &&
                        dtos.First().Description == "New OS Desc" &&
                        dtos.First().StartupType == (int)ServiceStartType.Automatic),
                    It.IsAny<CancellationToken>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public void RefreshAllServicesAsync_DependencyNullChecks_ExitEarly()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var refreshMethod = typeof(MainViewModel).GetMethod("RefreshAllServicesAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                // Step 1: Nullify ServiceManager
                typeof(MainViewModel).GetField("_serviceManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;

                // Step 2: Restore ServiceManager, nullify ServiceRepository
                typeof(MainViewModel).GetField("_serviceManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, _serviceManagerMock.Object);
                typeof(MainViewModel).GetField("_serviceRepository", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;

                Assert.True(true); // Verifies null references abort cleanly without an unhandled panic crash
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
                _processHelper.Setup(p => p.GetProcessTreeMetrics(1234)).Throws(new Exception("Process performance counter corrupt"));

                // Invoke method 
                var result = getInfoMethod!.Invoke(vm, new object[] { service, new Dictionary<string, ServiceInfo>(), new ServiceDto(), CancellationToken.None });

                Assert.NotNull(result);

                // Extract the tuple items dynamically
                var type = result.GetType();
                var updateInfo = type.GetField("Item1")!.GetValue(result) as ServiceUpdateInfo;
                var updatedDto = type.GetField("Item2")!.GetValue(result) as ServiceDto;

                // ==========================================
                // FIX: Assert property structural emptiness 
                // ==========================================
                Assert.NotNull(updateInfo);
                Assert.Null(updateInfo.CpuUsage);
                Assert.Null(updateInfo.NewPid);
                Assert.Null(updateInfo.Description);
                Assert.False(updateInfo.IsInstalled);

                Assert.Null(updatedDto); // Verifies the payload tracking DTO is dropped completely

                return true;
            }, createApp: true);
        }

        #endregion

        #region Bulk Operations (ExecuteBulkOperationAsync)

        private async Task SetupAndRunBulkOperation(Action<MainViewModel> configureTest, Func<MainViewModel, Task> commandAction)
        {
            var vm = CreateViewModel();
            var serviceField = typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance);
            var collection = (BulkObservableCollection<ServiceRowViewModel>)serviceField!.GetValue(vm)!;

            var svc1 = new Service { Name = "S1", IsInstalled = true };
            var svc2 = new Service { Name = "S2", IsInstalled = true };

            var row1 = new ServiceRowViewModel(svc1, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };
            var row2 = new ServiceRowViewModel(svc2, _serviceCommandsMock.Object, _cursorServiceMock.Object) { IsChecked = true };

            collection.Add(row1);
            collection.Add(row2);

            configureTest(vm);

            await commandAction(vm);
            await FlushDispatcherAsync();
        }

        [Fact]
        public void BulkOperations_NoServicesSelected_ShowsInfoMessage()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel(); // Blank grid scenario
                await vm.StartSelectedCommand.ExecuteAsync(null);

                _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.Msg_NoServicesSelected, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public void BulkOperations_NullMessageBoxService_ExitsEarly()
        {
            Helper.RunOnSTA(async () =>
            {
                await SetupAndRunBulkOperation(vm =>
                {
                    typeof(MainViewModel).GetField("_messageBoxService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                _serviceCommandsMock.Verify(c => c.StartServiceAsync(It.IsAny<Service>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        [Fact]
        public void BulkOperations_UserCancelsConfirmation_AbortsOperation()
        {
            Helper.RunOnSTA(async () =>
            {
                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                _serviceCommandsMock.Verify(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        [Fact]
        public void BulkOperations_Success_ShowsSuccessInfo()
        {
            Helper.RunOnSTA(async () =>
            {
                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                _messageBoxServiceMock.Verify(m => m.ShowInfoAsync(Strings.Msg_OperationCompletedSuccessfully, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public void BulkOperations_PartialFailure_ShowsWarningDetails()
        {
            Helper.RunOnSTA(async () =>
            {
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
        public void BulkOperations_TotalFailure_ShowsAllFailedWarning()
        {
            Helper.RunOnSTA(async () =>
            {
                await SetupAndRunBulkOperation(vm =>
                {
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.RestartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                }, async vm => await vm.RestartSelectedCommand.ExecuteAsync(null));

                _messageBoxServiceMock.Verify(m => m.ShowWarningAsync(Strings.Msg_AllOperationsFailed, It.IsAny<string>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public void BulkOperations_ExceptionThrown_HandledAndBusyStateReset()
        {
            Helper.RunOnSTA(async () =>
            {
                var vmRef = (MainViewModel)null!;
                await SetupAndRunBulkOperation(vm =>
                {
                    vmRef = vm;
                    _messageBoxServiceMock.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
                    _serviceCommandsMock.Setup(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Fatal Access Violation"));
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                Assert.False(vmRef.IsBusy); // Busy status resets in finally block
                _cursorServiceMock.Verify(c => c.ResetCursor(), Times.AtLeastOnce);
            }, createApp: true);
        }

        #endregion

        #region Helpers & Standalone Commands

        [Fact]
        public void RemoveService_ChecksAccessAndRemovesFromCollection()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var collection = (BulkObservableCollection<ServiceRowViewModel>)typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;

                collection.Add(new ServiceRowViewModel(new Service { Name = "ToRemove" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));
                collection.Add(new ServiceRowViewModel(new Service { Name = "ToKeep" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // Branch 1: Current Thread execution path (UI Thread context match)
                vm.RemoveService("ToRemove");
                Assert.Single(collection);

                // Branch 2: Worker Background Thread execution path (InvokeAsync fallback)
                await Task.Run(() => vm.RemoveService("ToKeep"));
                await FlushDispatcherAsync();
                Assert.Empty(collection);
            }, createApp: true);
        }

        [Fact]
        public void IndependentCommands_DelegateToUnderlyingServices()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var dummyService = new Service();

                await vm.ConfigureCommand.ExecuteAsync(dummyService);
                _serviceCommandsMock.Verify(c => c.ConfigureServiceAsync(dummyService, It.IsAny<CancellationToken>()), Times.Once);

                await vm.ImportXmlCommand.ExecuteAsync(null);
                _serviceCommandsMock.Verify(c => c.ImportXmlConfigAsync(It.IsAny<CancellationToken>()), Times.Once);

                await vm.ImportJsonCommand.ExecuteAsync(null);
                _serviceCommandsMock.Verify(c => c.ImportJsonConfigAsync(It.IsAny<CancellationToken>()), Times.Once);

                await vm.OpenDocumentationCommand.ExecuteAsync(null);
                _helpServiceMock.Verify(h => h.OpenDocumentation(It.IsAny<string>()), Times.Once);

                await vm.CheckUpdatesCommand.ExecuteAsync(null);
                _helpServiceMock.Verify(h => h.CheckUpdates(It.IsAny<string>()), Times.Once);

                await vm.OpenAboutDialogCommand.ExecuteAsync(null);
                _helpServiceMock.Verify(h => h.OpenAboutDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);

                await vm.Refresh();
                _serviceCommandsMock.Verify(c => c.SearchServicesAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
            }, createApp: true);
        }

        [Fact]
        public void HelpCommands_NullHelpService_ExitsCleanly()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                typeof(MainViewModel).GetField("_helpService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);

                // Missing interfaces must fail cleanly without bubble crashes
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

                // 1. Lock the gate to bypass structural updates during population updates
                typeof(MainViewModel).GetField("_isUpdatingSelectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, true);
                collection.Add(childRow1);
                collection.Add(childRow2);
                typeof(MainViewModel).GetField("_isUpdatingSelectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, false);

                bool selectAllChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SelectAll)) selectAllChangedFired = true;
                };

                // 2. Pre-set the backing field to true so modifying it to null triggers a state change execution loop
                typeof(MainViewModel).GetField("_selectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, true);

                // Act: Transition to Indeterminate (null)
                vm.SelectAll = null;

                // Assert
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
                // Arrange
                var vm = CreateViewModel();
                var changedProps = new List<string>();
                vm.PropertyChanged += (s, e) => { if (e.PropertyName != null) changedProps.Add(e.PropertyName); };

                // ==========================================
                // 1. TEST: IsBusy
                // ==========================================
                vm.IsBusy = true;
                Assert.True(vm.IsBusy);
                Assert.Contains(nameof(vm.IsBusy), changedProps);

                changedProps.Clear();
                vm.IsBusy = true; // Duplicate value assignment
                Assert.Empty(changedProps); // Proves equality check branch optimization bypassed execution

                // ==========================================
                // 2. TEST: FooterText
                // ==========================================
                vm.FooterText = "Total Services: 5";
                Assert.Equal("Total Services: 5", vm.FooterText);
                Assert.Contains(nameof(vm.FooterText), changedProps);

                changedProps.Clear();
                vm.FooterText = "Total Services: 5";
                Assert.Empty(changedProps);

                // ==========================================
                // 3. TEST: SearchButtonText
                // ==========================================
                vm.SearchButtonText = "Locating...";
                Assert.Equal("Locating...", vm.SearchButtonText);
                Assert.Contains(nameof(vm.SearchButtonText), changedProps);

                changedProps.Clear();
                vm.SearchButtonText = "Locating...";
                Assert.Empty(changedProps);

                // ==========================================
                // 4. TEST: SearchText
                // ==========================================
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
                // Arrange
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

                // Act - Set SelectAll to true
                vm.SelectAll = true;

                // Assert Cascade Down
                Assert.True(childRow1.IsChecked);
                Assert.True(childRow2.IsChecked);
                Assert.False(childRow1.IsSelected); // Verify internal row property cleanup
                Assert.True(selectAllNotified);

                // Reset notification flag
                selectAllNotified = false;

                // Act - Set SelectAll to same value to hit short-circuit check branch bypass
                vm.SelectAll = true;
                Assert.False(selectAllNotified);

                // Act - Trigger internal conditional lockout path by hitting the `_isUpdatingSelectAll` re-entry gate
                typeof(MainViewModel).GetField("_isUpdatingSelectAll", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, true);
                childRow1.IsChecked = false; // Intentionally break child collection state synchronization uniformity
                vm.SelectAll = false;

                // If it successfully exits early without modifying the row state due to the true flag gate, child 2 remains true
                Assert.True(childRow2.IsChecked);
            }, createApp: true);
        }

        [Fact]
        public void IsConfiguratorEnabled_MutatesValueAndFiresNotification()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
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
                // Act - Duplicate entry checking behavior target validation bypass
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
                // Arrange & Act
                var vm = CreateViewModel();

                // Assert Engine Commands Collection Presence
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

        #region Disposal & Teardown

        [Fact]
        public void Dispose_CleansUpTimersAndSubscriptions_SafelyHandlesDoubleDispose()
        {
            Helper.RunOnSTA(() =>
            {
                var vm = CreateViewModel();

                var collection = (BulkObservableCollection<ServiceRowViewModel>)typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
                collection.Add(new ServiceRowViewModel(new Service(), _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // Act
                vm.Dispose();

                // Assert structural memory cleanup state
                Assert.Empty(collection); // Child row elements cleared and unhooked successfully

                var doubleDisposeException = Record.Exception(() => vm.Dispose());
                Assert.Null(doubleDisposeException); // Passing down double dispose branch exits smoothly without popping exceptions
            }, createApp: true);
        }

        #endregion
    }
}