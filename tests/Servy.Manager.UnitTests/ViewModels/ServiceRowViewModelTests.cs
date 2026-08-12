using Moq;
using Servy.Core.Enums;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Testing;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
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
        public void CanExecuteServiceCommand_ShouldReturnFalse_WhenInternalServiceNameIsEmpty()
        {
            // Arrange
            var vm = new ServiceRowViewModel(
                new Service { Name = "" },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );

            // Act
            var result = (bool)TestReflection.InvokeNonPublic(vm, "CanExecuteServiceCommand", new object[] { null });

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task StartCommand_ShouldCallStartServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel("RowSvc");
            var parameterService = new Service { Name = "ParamSvc" };

            _serviceCommandsMock.Setup(s => s.StartServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
                .Setup(s => s.StopServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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

            _serviceCommandsMock.Setup(s => s.RestartServiceAsync(It.Is<Service>(srv => ReferenceEquals(srv, vm.Service)), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
                Pid = 1234,
                IsPidEnabled = true,
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

        [Fact]
        public void Service_PropertyChanged_NullOrEmptyName_ReturnsEarlyWithoutPropertyOrCommandEvaluation()
        {
            // Arrange
            var service = new Service { Name = "RowSvc" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            bool localPropertyNotificationFired = false;
            vm.PropertyChanged += (s, e) => localPropertyNotificationFired = true;

            // Act & Assert Branch 1: Null PropertyChangedEventArgs argument context
            TestReflection.InvokeNonPublic(vm, "Service_PropertyChanged", service, null);
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

            // Act
            Func<Task> faultyAction = () => _serviceCommandsMock.Object.ConfigureServiceAsync(service, CancellationToken.None);

            var taskResult = (Task)TestReflection.InvokeNonPublic(vm, "ExecuteSafeAsync", faultyAction);
            await taskResult;

            // Assert
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
            service.Status = ServiceStatus.Stopped;
            Assert.NotEqual(ServiceStatus.Running, service.Status);   // guards the arrange itself

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
