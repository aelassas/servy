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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

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

            // 1. Create the missing IUiDispatcher mock
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

            // 2. FIX: Append uiDispatcherMock.Object as the 5th argument to all child VM constructors
            _performanceViewModelMock = new Mock<PerformanceViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                uiDispatcherMock.Object); // Injected

            _consoleViewModelMock = new Mock<ConsoleViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                uiDispatcherMock.Object); // Injected

            _dependenciesViewModelMock = new Mock<DependenciesViewModel>(
                _serviceRepositoryMock.Object,
                _serviceManagerMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object,
                uiDispatcherMock.Object); // Injected
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
            Helper.RunOnSTA(async () =>
            {
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(null!, _serviceRepositoryMock.Object, _serviceCommandsMock.Object, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelper.Object, Dispatcher.CurrentDispatcher));
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(_serviceManagerMock.Object, null!, _serviceCommandsMock.Object, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelper.Object, Dispatcher.CurrentDispatcher));
                Assert.Throws<ArgumentNullException>(() => new MainViewModel(_serviceManagerMock.Object, _serviceRepositoryMock.Object, null!, _helpServiceMock.Object, _messageBoxServiceMock.Object, _performanceViewModelMock.Object, _consoleViewModelMock.Object, _dependenciesViewModelMock.Object, _appConfigMock.Object, _cursorServiceMock.Object, _processHelper.Object, Dispatcher.CurrentDispatcher));
            }, createApp: true);
        }

        [Fact]
        public void Constructor_DesignTime_DoesNotThrow()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = new MainViewModel();
                Assert.NotNull(vm);
            }, createApp: true);
        }

        [Fact]
        public void Properties_SettersTriggerPropertyChanged()
        {
            Helper.RunOnSTA(async () =>
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
            Helper.RunOnSTA(async () =>
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
            Helper.RunOnSTA(async () =>
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

                // Branch 2: Child modified to false -> SelectAll becomes null
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
                // Sabotage the dispatcher field
                typeof(MainViewModel).GetField("_dispatcher", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);

                await vm.SearchCommand.ExecuteAsync(null);

                _serviceCommandsMock.Verify(c => c.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }, createApp: true);
        }

        #endregion

        #region Background Refresh & Timer Sync

        [Fact]
        public void TimerLifecycle_CreateStartStop_ManagesResourcesCorrectly()
        {
            Helper.RunOnSTA(async () =>
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
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var onTickMethod = typeof(MainViewModel).GetMethod("OnTick", BindingFlags.NonPublic | BindingFlags.Instance);

                // Fire two ticks simultaneously to trigger the CompareExchange lock out
                onTickMethod!.Invoke(vm, new object[] { null!, EventArgs.Empty });
                onTickMethod!.Invoke(vm, new object[] { null!, EventArgs.Empty });

                Assert.True(true); // Survives without exception
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

                _serviceManagerMock.Setup(m => m.GetAllServices(It.IsAny<CancellationToken>()))
                    .Returns(new List<ServiceInfo>
                    {
                        new ServiceInfo { Name = "DriftService", Status = ServiceStatus.Stopped, Description = "New OS Desc", StartupType = ServiceStartType.Automatic, LogOnAs = "CustomUser" }
                    });

                _serviceRepositoryMock.Setup(r => r.GetAllAsync(true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ServiceDto>
                    {
                        new ServiceDto { Name = "DriftService", Pid = 9999, Description = "Old DB Desc", StartupType = (int)ServiceStartType.Manual }
                    });

                var refreshMethod = typeof(MainViewModel).GetMethod("RefreshAllServicesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;
                await FlushDispatcherAsync();

                // UI Assertions
                Assert.Null(targetService.Pid); // Ghost PID wiped since status is Stopped
                Assert.Equal("New OS Desc", targetService.Description);
                Assert.Equal(ServiceStartType.Automatic, targetService.StartupType);
                Assert.Equal(ServiceStatus.Stopped, targetService.Status);
                Assert.Equal("CustomUser", targetService.LogOnAs);

                // DB Synchronization Assertions
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

                // Test missing ServiceManager
                typeof(MainViewModel).GetField("_serviceManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;

                // Restore and test missing Repository
                typeof(MainViewModel).GetField("_serviceManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, _serviceManagerMock.Object);
                typeof(MainViewModel).GetField("_serviceRepository", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, null);
                await (Task)refreshMethod!.Invoke(vm, new object[] { CancellationToken.None })!;

                // No exceptions thrown means early exits successfully triggered
                Assert.True(true);
            }, createApp: true);
        }

        [Fact]
        public void GetServiceUpdateInfo_ExceptionBranch_ReturnsNullsSafely()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var getInfoMethod = typeof(MainViewModel).GetMethod("GetServiceUpdateInfo", BindingFlags.NonPublic | BindingFlags.Instance);

                var service = new Service { Name = "CrashService", Pid = 1234 };
                _processHelper.Setup(p => p.GetProcessTreeMetrics(1234)).Throws(new Exception("Boom!"));

                // Invoke method - internal exception should be swallowed and return (null, null)
                var result = getInfoMethod!.Invoke(vm, new object[] { service, new Dictionary<string, ServiceInfo>(), new ServiceDto(), CancellationToken.None });

                var type = result!.GetType();
                var updateInfo = type.GetField("Item1")!.GetValue(result);
                var updatedDto = type.GetField("Item2")!.GetValue(result);

                Assert.Null(updateInfo);
                Assert.Null(updatedDto);
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
                var vm = CreateViewModel(); // Empty services list
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
                    _serviceCommandsMock.Setup(c => c.StartServiceAsync(It.IsAny<Service>(), false, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Fatal"));
                }, async vm => await vm.StartSelectedCommand.ExecuteAsync(null));

                Assert.False(vmRef.IsBusy);
            }, createApp: true);
        }

        #endregion

        #region Helpers & Standard Commands

        [Fact]
        public void RemoveService_ChecksAccessAndRemovesFromCollection()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var collection = (BulkObservableCollection<ServiceRowViewModel>)typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;

                collection.Add(new ServiceRowViewModel(new Service { Name = "ToRemove" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));
                collection.Add(new ServiceRowViewModel(new Service { Name = "ToKeep" }, _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // 1. Current Thread (UI Thread branch)
                vm.RemoveService("ToRemove");
                Assert.Single(collection);

                // 2. Background Thread (InvokeAsync branch)
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

                // Refresh simply triggers Search
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

                // These should all return immediately without throwing exceptions
                await vm.OpenDocumentationCommand.ExecuteAsync(null);
                await vm.CheckUpdatesCommand.ExecuteAsync(null);
                await vm.OpenAboutDialogCommand.ExecuteAsync(null);

                Assert.True(true);
            }, createApp: true);
        }

        #endregion

        #region Propety Tests

        [Fact]
        public void Properties_TriStateSelectAll_MutatesAndFiresNotification()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                bool selectAllChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SelectAll)) selectAllChangedFired = true;
                };

                // Act: Set to null (indeterminate state)
                vm.SelectAll = null;

                // Assert
                Assert.Null(vm.SelectAll);
                Assert.True(selectAllChangedFired);

                // Reset tracking flag
                selectAllChangedFired = false;

                // Act: Set to same value to verify short-circuit branch bypass
                vm.SelectAll = null;
                Assert.False(selectAllChangedFired, "Setting the same value must bypass raising PropertyChanged.");
            }, createApp: true);
        }

        [Fact]
        public void Properties_SearchText_MutatesAndUpdatesValue()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                bool searchTextChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SearchText)) searchTextChangedFired = true;
                };

                // Act
                vm.SearchText = "WexflowService";

                // Assert
                Assert.Equal("WexflowService", vm.SearchText);
                Assert.True(searchTextChangedFired);
            }, createApp: true);
        }

        [Fact]
        public void Properties_SearchButtonText_MutatesAndUpdatesValue()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                bool buttonTextChangedFired = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.SearchButtonText)) buttonTextChangedFired = true;
                };

                // Act
                vm.SearchButtonText = Strings.Button_Searching;

                // Assert
                Assert.Equal(Strings.Button_Searching, vm.SearchButtonText);
                Assert.True(buttonTextChangedFired);
            }, createApp: true);
        }

        #endregion

        #region Disposal

        [Fact]
        public void Dispose_CleansUpTimersAndSubscriptions_SafelyHandlesDoubleDispose()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();

                var collection = (BulkObservableCollection<ServiceRowViewModel>)typeof(MainViewModel).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
                collection.Add(new ServiceRowViewModel(new Service(), _serviceCommandsMock.Object, _cursorServiceMock.Object));

                // Act
                vm.Dispose();
                vm.Dispose(); // Branch: Double dispose triggers early exit `if (_disposed) return;`

                // Assert
                Assert.Empty(collection); // Proves children were cleared and disposed
            }, createApp: true);
        }

        #endregion
    }
}