using Moq;
using Servy.Core.Enums;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Testing;
using Servy.UI.Services;
using System.ComponentModel;
using System.Windows.Threading;

namespace Servy.Manager.UnitTests.ViewModels
{
    [Collection(AmbientTestCollection.Name)]
    public class ServiceRowViewModelTests
    {
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<ICursorService> _cursorServiceMock;

        public ServiceRowViewModelTests()
        {
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _cursorServiceMock = new Mock<ICursorService>();
        }

        private ServiceRowViewModel CreateViewModel(string serviceName = "RowSvc")
        {
            return new ServiceRowViewModel(
                new Service { Name = serviceName, Pid = 123 },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );
        }

        #region Constructor Guard Clauses Tests

        [Theory]
        [InlineData(0, "service")]
        [InlineData(1, "serviceCommands")]
        [InlineData(2, "cursorService")]
        public void Constructor_NullArguments_ThrowsArgumentNullException(int nullIndex, string expectedParamName)
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ServiceRowViewModel(
                nullIndex == 0 ? null : new Service { Name = "RowSvc" },
                nullIndex == 1 ? null : _serviceCommandsMock.Object,
                nullIndex == 2 ? null : _cursorServiceMock.Object));

            Assert.Equal(expectedParamName, ex.ParamName);
        }

        [Fact]
        public void Constructor_ValidArguments_InitializesAllAsyncCommandsSuccessfully()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm.StartCommand);
            Assert.NotNull(vm.StopCommand);
            Assert.NotNull(vm.RestartCommand);
            Assert.NotNull(vm.ConfigureCommand);
            Assert.NotNull(vm.InstallCommand);
            Assert.NotNull(vm.UninstallCommand);
            Assert.NotNull(vm.RemoveCommand);
            Assert.NotNull(vm.ExportXmlCommand);
            Assert.NotNull(vm.ExportJsonCommand);
            Assert.NotNull(vm.CopyPidCommand);
        }

        #endregion

        #region Command Functional Execution Tests

        [Fact]
        public async Task StartCommand_ShouldCallStartServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.StartServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.Is<bool>(b => b), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service.IsInstalled = true;
            vm.Service.Status = ServiceStatus.Stopped;

            // Act
            await vm.StartCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task StopCommand_ShouldCallStopServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock
                .Setup(s => s.StopServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.Is<bool>(b => b), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service.IsInstalled = true;
            vm.Service.Status = ServiceStatus.Running;

            // Act
            await vm.StopCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task RestartCommand_ShouldCallRestartServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.RestartServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.Is<bool>(b => b), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service.IsInstalled = true;
            vm.Service.Status = ServiceStatus.Running;

            // Act
            await vm.RestartCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task ConfigureCommand_ShouldCallConfigureServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.ConfigureServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask)
              .Verifiable();

            // Act
            await vm.ConfigureCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task InstallCommand_ShouldCallInstallServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.InstallServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            // Act
            await vm.InstallCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task UninstallCommand_ShouldCallUninstallServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.UninstallServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service.IsInstalled = true;

            // Act
            await vm.UninstallCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task RemoveCommand_ShouldCallRemoveServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.RemoveServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            // Act
            await vm.RemoveCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task ExportXmlCommand_ShouldCallExportServiceToXmlAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.ExportServiceToXmlAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            await vm.ExportXmlCommand.ExecuteAsync(parameterService);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task ExportJsonCommand_ShouldCallExportServiceToJsonAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");

            // Verify it binds securely back to the internal SUT Model reference when null is given
            _serviceCommandsMock
                 .Setup(s => s.ExportServiceToJsonAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Verifiable();

            // Act
            await vm.ExportJsonCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public async Task CopyPidCommand_ShouldCallCopyPidAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");

            _serviceCommandsMock
                .Setup(s => s.CopyPidAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            await vm.CopyPidCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "RowSvc" && s.Pid == 123), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Reads one row command's predicate, selected by the command's property name, so the theories
        /// below can vary which command they exercise without reflection.
        /// </summary>
        /// <param name="vm">The row view model whose command predicate is read.</param>
        /// <param name="commandName">
        /// The name of the command property to read, as <c>nameof</c> spells it.
        /// </param>
        /// <returns><c>true</c> when the selected command can execute; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="commandName"/> is not one of the row commands.
        /// </exception>
        private static bool CanExecuteByName(ServiceRowViewModel vm, string commandName)
        {
            if (commandName == nameof(ServiceRowViewModel.StartCommand)) return vm.StartCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.StopCommand)) return vm.StopCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.RestartCommand)) return vm.RestartCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.UninstallCommand)) return vm.UninstallCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.ConfigureCommand)) return vm.ConfigureCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.InstallCommand)) return vm.InstallCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.RemoveCommand)) return vm.RemoveCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.ExportXmlCommand)) return vm.ExportXmlCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.ExportJsonCommand)) return vm.ExportJsonCommand.CanExecute(null);
            if (commandName == nameof(ServiceRowViewModel.CopyPidCommand)) return vm.CopyPidCommand.CanExecute(null);

            throw new ArgumentOutOfRangeException(nameof(commandName), commandName, "Unknown row command.");
        }

        [Theory]
        [InlineData(nameof(ServiceRowViewModel.StartCommand), false, ServiceStatus.Stopped)]
        [InlineData(nameof(ServiceRowViewModel.StopCommand), false, ServiceStatus.Running)]
        [InlineData(nameof(ServiceRowViewModel.RestartCommand), false, ServiceStatus.Running)]
        [InlineData(nameof(ServiceRowViewModel.RestartCommand), true, ServiceStatus.Stopped)]
        [InlineData(nameof(ServiceRowViewModel.UninstallCommand), false, ServiceStatus.Stopped)]
        public void StateGatedCommands_CanExecute_IsFalseWhenTheServiceCannotTakeTheAction(string commandName, bool isInstalled, ServiceStatus status)
        {
            // Arrange - every other term of the predicate holds, so only the varied one can disable it
            var vm = new ServiceRowViewModel(
                new Service { Name = "RowSvc", Pid = 123, IsInstalled = isInstalled, Status = status },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );

            // Act
            var canExecute = CanExecuteByName(vm, commandName);

            // Assert
            Assert.False(canExecute);
        }

        [Theory]
        [InlineData(nameof(ServiceRowViewModel.StartCommand), ServiceStatus.Stopped, "")]
        [InlineData(nameof(ServiceRowViewModel.StopCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.RestartCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.ConfigureCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.InstallCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.UninstallCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.RemoveCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.ExportXmlCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.ExportJsonCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.CopyPidCommand), ServiceStatus.Running, "")]
        [InlineData(nameof(ServiceRowViewModel.ConfigureCommand), ServiceStatus.Running, "   ")]
        public void RowCommands_CanExecute_IsFalseWhenTheServiceHasNoName(string commandName, ServiceStatus status, string name)
        {
            // Arrange - IsInstalled, Status and Pid all hold on both rows, so only the name can disable the command
            var named = new ServiceRowViewModel(
                new Service { Name = "RowSvc", Pid = 123, IsInstalled = true, Status = status },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );
            var unnamed = new ServiceRowViewModel(
                new Service { Name = name, Pid = 123, IsInstalled = true, Status = status },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );

            // Act
            var namedCanExecute = CanExecuteByName(named, commandName);
            var unnamedCanExecute = CanExecuteByName(unnamed, commandName);

            // Assert - the control row proves the arrange enables the command, so the false below comes from the name
            Assert.True(namedCanExecute);
            Assert.False(unnamedCanExecute);
        }

        [Fact]
        public void CopyPidCommand_CanExecute_IsFalseWithoutAPid()
        {
            // Arrange
            var vm = new ServiceRowViewModel(
                new Service { Name = "RowSvc", Pid = null },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );

            // Act
            var canExecute = vm.CopyPidCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        #endregion

        #region Properties & Model Propagation Tests

        [Fact]
        public void Properties_ShouldReflectModelAndNotifyChanges()
        {
            // Arrange
            var service = new Service
            {
                Name = "TestService",
                Description = "Test Description",
                Status = ServiceStatus.Running,
                StartupType = ServiceStartType.Automatic,
                LogOnAs = "LocalSystem",
                IsInstalled = true,
                IsDesktopAppAvailable = true,
                Pid = 1234,   // IsPidEnabled is derived from this
                CpuUsage = 5.5,
                RamUsage = 1024 * 1024 // 1 MB
            };

            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);
            var propertiesChanged = new List<string>();
            vm.PropertyChanged += (s, e) => { if (e.PropertyName != null) propertiesChanged.Add(e.PropertyName); };

            // Assert Initial Passthrough Layout
            Assert.Equal("TestService", vm.Name);
            Assert.Equal("Test Description", vm.Description);
            Assert.Equal(ServiceStatus.Running, vm.Status);
            Assert.Equal(ServiceStartType.Automatic, vm.StartupType);
            Assert.Equal("LocalSystem", vm.LogOnAs);
            Assert.True(vm.IsInstalled);
            Assert.True(vm.IsDesktopAppAvailable);
            Assert.Equal(1234, vm.Pid);
            Assert.True(vm.IsPidEnabled);
            Assert.Equal(5.5, vm.CpuUsage);
            Assert.Equal(1024 * 1024, vm.RamUsage);

            // Act - Change ViewModel properties directly
            vm.IsSelected = true;
            vm.IsSelected = true; // Duplicate pass to ensure optimization coverage
            vm.IsChecked = true;
            vm.IsChecked = true;  // Duplicate pass to ensure optimization coverage

            // Act - Trigger changes through the underlying Model to verify automatic forwarding
            service.Status = ServiceStatus.Stopped;
            service.Pid = 0;

            // Assert Notification Triggers & Optimization Coverage
            Assert.Equal(1, propertiesChanged.Count(p => p == nameof(vm.IsSelected)));
            Assert.Equal(1, propertiesChanged.Count(p => p == nameof(vm.IsChecked)));

            // Core model mutations should still stream through at-least-once via automatic forwarding hooks
            Assert.Contains(nameof(vm.Status), propertiesChanged);
            Assert.Contains(nameof(vm.Pid), propertiesChanged);
        }

        [Theory]
        [InlineData(null, 4321, true)]
        [InlineData(4321, null, false)]
        public void IsPidEnabled_IsDerivedFromPid_AndIsRaisedWhenPidChanges(int? initialPid, int? newPid, bool expected)
        {
            // Arrange
            var service = new Service { Name = "RowSvc", Pid = initialPid };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);
            var propertiesChanged = new List<string>();
            vm.PropertyChanged += (s, e) => { if (e.PropertyName != null) propertiesChanged.Add(e.PropertyName); };

            // Act - only Pid is written; nothing restates the IsPidEnabled rule
            service.Pid = newPid;

            // Assert - the Copy PID menu item binds IsPidEnabled through this passthrough, so the
            // notification matters as much as the value
            Assert.Equal(expected, vm.IsPidEnabled);
            Assert.Contains(nameof(vm.IsPidEnabled), propertiesChanged);
        }

        [Fact]
        public void Service_PropertyChanged_NullOrEmptyName_ReturnsEarlyWithoutPropertyOrCommandEvaluation()
        {
            // Arrange
            var service = new Service { Name = "RowSvc" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            bool localPropertyNotificationFired = false;
            vm.PropertyChanged += (s, e) => localPropertyNotificationFired = true;

            // Act & Assert Branch 1: Null PropertyChangedEventArgs argument context
            TestReflection.InvokeNonPublic(vm, "Service_PropertyChanged", service, null!);
            Assert.False(localPropertyNotificationFired);

            // Act & Assert Branch 2: Empty PropertyName string value
            TestReflection.InvokeNonPublic(vm, "Service_PropertyChanged", service, new PropertyChangedEventArgs(string.Empty));
            Assert.False(localPropertyNotificationFired);
        }

        [Fact]
        public async Task Service_PropertyChanged_RelevantPropertyUpdated_RaisesCanExecuteChangedOnCommands()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var service = new Service { Name = "RowSvc", IsInstalled = true, Status = ServiceStatus.Stopped };
                var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

                var wasRaised = false;
                EventHandler handler = (sender, args) => wasRaised = true;

                vm.StartCommand.CanExecuteChanged += handler;
                vm.StopCommand.CanExecuteChanged += handler;
                vm.RestartCommand.CanExecuteChanged += handler;

                try
                {
                    // Assert state transition before mutation
                    Assert.True(vm.StartCommand.CanExecute(null));
                    Assert.False(vm.StopCommand.CanExecute(null));

                    // Act - Trigger a status update, which should invalidate CommandManager requery
                    service.Status = ServiceStatus.Running;

                    // Pump the STA dispatcher frames so CommandManager.RequerySuggested fires
                    await Dispatcher.Yield(DispatcherPriority.Background);

                    // Assert
                    Assert.True(wasRaised, "CanExecuteChanged was not raised when a relevant property (Status) was updated.");
                    Assert.False(vm.StartCommand.CanExecute(null));
                    Assert.True(vm.StopCommand.CanExecute(null));
                }
                finally
                {
                    vm.StartCommand.CanExecuteChanged -= handler;
                    vm.StopCommand.CanExecuteChanged -= handler;
                    vm.RestartCommand.CanExecuteChanged -= handler;
                }
            }, createApp: true);
        }

        [Fact]
        public async Task Service_PropertyChanged_IsInstalledUpdated_RaisesCanExecuteChangedOnCommands()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var service = new Service { Name = "RowSvc", IsInstalled = false, Status = ServiceStatus.Stopped };
                var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

                var wasRaised = false;
                EventHandler handler = (sender, args) => wasRaised = true;

                vm.StartCommand.CanExecuteChanged += handler;

                try
                {
                    // Assert state transition before mutation
                    Assert.False(vm.StartCommand.CanExecute(null));

                    // Act - an install is the IsInstalled term of the requery trigger
                    service.IsInstalled = true;

                    // Pump the STA dispatcher frames so CommandManager.RequerySuggested fires
                    await Dispatcher.Yield(DispatcherPriority.Background);

                    // Assert
                    Assert.True(wasRaised, "CanExecuteChanged was not raised when a relevant property (IsInstalled) was updated.");
                    Assert.True(vm.StartCommand.CanExecute(null));
                }
                finally
                {
                    vm.StartCommand.CanExecuteChanged -= handler;
                }
            }, createApp: true);
        }

        [Fact]
        public async Task Service_PropertyChanged_PidUpdated_RaisesCanExecuteChangedOnCommands()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var service = new Service { Name = "RowSvc", IsInstalled = true, Status = ServiceStatus.Running, Pid = 123 };
                var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

                var wasRaised = false;
                EventHandler handler = (sender, args) => wasRaised = true;

                vm.CopyPidCommand.CanExecuteChanged += handler;

                try
                {
                    // Assert state transition before mutation
                    Assert.True(vm.CopyPidCommand.CanExecute(null));

                    // Act - losing the PID is the Pid term of the requery trigger
                    service.Pid = null;

                    // Pump the STA dispatcher frames so CommandManager.RequerySuggested fires
                    await Dispatcher.Yield(DispatcherPriority.Background);

                    // Assert
                    Assert.True(wasRaised, "CanExecuteChanged was not raised when a relevant property (Pid) was updated.");
                    Assert.False(vm.CopyPidCommand.CanExecute(null));
                }
                finally
                {
                    vm.CopyPidCommand.CanExecuteChanged -= handler;
                }
            }, createApp: true);
        }

        [Fact]
        public async Task Service_PropertyChanged_IrrelevantPropertyUpdated_DoesNotRaiseCanExecuteChangedOnCommands()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var service = new Service { Name = "RowSvc" };
                var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

                var wasRaised = false;
                EventHandler handler = (sender, args) => wasRaised = true;

                // Subscribe to representative commands that evaluate service state thresholds
                vm.StartCommand.CanExecuteChanged += handler;
                vm.StopCommand.CanExecuteChanged += handler;
                vm.RestartCommand.CanExecuteChanged += handler;

                try
                {
                    // Act
                    // Triggering a non-state tracking field update (like Description) shouldn't invoke structural command refreshes
                    TestReflection.InvokeNonPublic(vm, "Service_PropertyChanged", service, new PropertyChangedEventArgs(nameof(Service.Description)));

                    // Pump the STA dispatcher frames so any scheduled RequerySuggested would execute
                    await Dispatcher.Yield(DispatcherPriority.Background);

                    // Assert
                    // Explicitly prove that no command re-evaluation was triggered by the change event
                    Assert.False(wasRaised, "CanExecuteChanged was erroneously raised on commands for an irrelevant property update.");
                }
                finally
                {
                    // Clean up event handlers to prevent test-runner memory leaks
                    vm.StartCommand.CanExecuteChanged -= handler;
                    vm.StopCommand.CanExecuteChanged -= handler;
                    vm.RestartCommand.CanExecuteChanged -= handler;
                }
            }, createApp: true);
        }

        #endregion

        #region Execution Safety & Disposal Tests

        [Fact]
        public async Task ExecuteSafeAsync_ShouldCatchExceptionsAndResetCursor()
        {
            // Arrange
            var service = new Service { Name = "FaultyService" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            _serviceCommandsMock
                .Setup(s => s.ConfigureServiceAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SCM access denied simulation error."));

            Func<Task> faultyAction = () => _serviceCommandsMock.Object.ConfigureServiceAsync(service, TestContext.Current.CancellationToken);

            // Act
            // The file log is captured because the Logger.Error line is the only thing the general
            // catch (Exception) arm does that the cancellation arm deliberately does not, so it is
            // the only assertion that tells the two arms apart.
            var log = await LogCapture.RunAsync(
                () => (Task)TestReflection.InvokeNonPublic(vm, "ExecuteSafeAsync", nameof(vm.ConfigureCommand), faultyAction)!);

            // Assert
            Assert.Contains("ConfigureCommand failed for FaultyService.", log);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task ExecuteSafeAsync_OperationCanceledException_SwallowsAndResetsCursor()
        {
            // Arrange
            var service = new Service { Name = "CancelledService" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            // A cancelled action must reach the dedicated catch (OperationCanceledException) arm,
            // which swallows it silently instead of logging it like the general catch (Exception) arm.
            Func<Task> cancelledAction = () => Task.FromException(new OperationCanceledException());

            // Act
            var taskResult = Task.CompletedTask;
            var log = await LogCapture.RunAsync(
                () => taskResult = (Task)TestReflection.InvokeNonPublic(vm, "ExecuteSafeAsync", nameof(vm.ConfigureCommand), cancelledAction)!);

            // Assert
            // Awaiting without throwing is the proof the cancellation was absorbed, not rethrown.
            Assert.True(taskResult.IsCompletedSuccessfully, "ExecuteSafeAsync did not absorb the OperationCanceledException.");
            // Deleting the cancellation arm routes this into catch (Exception), which logs this line,
            // and adding a Logger.Error to the cancellation arm logs it from there. Either way the
            // silence this arm exists for is gone, and only this assertion sees it.
            Assert.DoesNotContain("failed for CancelledService", log);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldUnsubscribeFromModelEvents()
        {
            // Arrange
            var service = new Service { Name = "TransientService", Status = ServiceStatus.Stopped };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            var receivedNotifications = 0;
            vm.PropertyChanged += (s, e) => receivedNotifications++;

            // Baseline: the VM forwards model notifications while subscribed.
            service.Status = ServiceStatus.Running;
            Assert.True(receivedNotifications > 0, "The view model never forwarded notifications before disposal.");

            receivedNotifications = 0;

            // Act
            vm.Dispose();

            // A genuinely different value, so the model definitely raises PropertyChanged.
            // Observe the model's own event instead of re-reading the value just written: Status is
            // an equality-guarded setter, so whether it raises depends on the baseline set above,
            // which a comparison against the freshly assigned value cannot see.
            var modelRaised = false;
            PropertyChangedEventHandler probe = (s, e) => modelRaised = true;
            service.PropertyChanged += probe;
            try
            {
                service.Status = ServiceStatus.Stopped;
            }
            finally
            {
                service.PropertyChanged -= probe;
            }

            // The arrange is only valid if the model actually notified.
            Assert.True(modelRaised, "The model did not raise PropertyChanged, so this test cannot prove the view model unsubscribed.");

            // Assert
            Assert.Equal(0, receivedNotifications);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ReturnsEarlySilently()
        {
            // Arrange
            var service = new Service { Name = "TransientService" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            // Act
            vm.Dispose();
            bool isDisposedAfterFirstCall = TestReflection.GetField<bool>(vm, "_disposed");

            // Re-invoke tracking logic to challenge the internal boolean field guard branch
            var sequentialDisposeException = Record.Exception(() => vm.Dispose());

            // Assert
            Assert.True(isDisposedAfterFirstCall, "The underlying tracking field '_disposed' was not set to true during the first execution pass.");
            Assert.Null(sequentialDisposeException);
        }

        #endregion
    }
}
