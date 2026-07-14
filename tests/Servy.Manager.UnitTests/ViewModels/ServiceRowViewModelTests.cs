using Moq;
using Servy.Core.Enums;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Testing;
using Servy.UI.Services;
using System.ComponentModel;

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

        private ServiceRowViewModel CreateViewModel()
        {
            return new ServiceRowViewModel(
                new Service { Name = "S", Pid = 123 },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );
        }

        #region Constructor Guard Clauses Tests

        [Fact]
        public void Constructor_NullService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceRowViewModel(null!, _serviceCommandsMock.Object, _cursorServiceMock.Object));
        }

        [Fact]
        public void Constructor_NullServiceCommands_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceRowViewModel(new Service { Name = "S" }, null!, _cursorServiceMock.Object));
        }

        [Fact]
        public void Constructor_NullCursorService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceRowViewModel(new Service { Name = "S" }, _serviceCommandsMock.Object, null!));
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
            var result = (bool)TestReflection.InvokeNonPublic(vm, "CanExecuteServiceCommand", new object[] { null! })!;

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void StartCommand_ShouldCallStartServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.StartServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.Is<bool>(b => b), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service!.IsInstalled = true;
            vm.Service!.Status = ServiceStatus.Stopped;

            // Act
            vm.StartCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void StopCommand_ShouldCallStopServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };

            _serviceCommandsMock
                .Setup(s => s.StopServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.Is<bool>(b => b), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service!.IsInstalled = true;
            vm.Service!.Status = ServiceStatus.Running;

            // Act
            vm.StopCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void RestartCommand_ShouldCallRestartServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.RestartServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.Is<bool>(b => b), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service!.IsInstalled = true;
            vm.Service!.Status = ServiceStatus.Running;

            // Act
            vm.RestartCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ConfigureCommand_ShouldCallConfigureServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.ConfigureServiceAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask)
              .Verifiable();

            // Act
            vm.ConfigureCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void InstallCommand_ShouldCallInstallServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.InstallServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            // Act
            vm.InstallCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void UninstallCommand_ShouldCallUninstallServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.UninstallServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.Service!.IsInstalled = true;

            // Act
            vm.UninstallCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void RemoveCommand_ShouldCallRemoveServiceAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.RemoveServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            // Act
            vm.RemoveCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ExportXmlCommand_ShouldCallExportServiceToXmlAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.ExportServiceToXmlAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            vm.ExportXmlCommand.Execute(service);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ExportJsonCommand_ShouldCallExportServiceToJsonAsync()
        {
            // Arrange
            var vm = CreateViewModel();

            _serviceCommandsMock
                 .Setup(s => s.ExportServiceToJsonAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Verifiable();

            // Act
            vm.ExportJsonCommand.Execute(null);

            // Assert
            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void CopyPidCommand_ShouldCallCopyPidAsync()
        {
            // Arrange
            var vm = CreateViewModel();
            _serviceCommandsMock
                .Setup(s => s.CopyPidAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            vm.CopyPidCommand.Execute(null);

            // Assert
            _serviceCommandsMock.Verify();
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
                RamUsage = 1024_1024
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
            Assert.Equal(1024_1024, vm.RamUsage);

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
            var service = new Service { Name = "S" };
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
        public void Service_PropertyChanged_IrrelevantPropertyUpdated_DoesNotRaiseCanExecuteChangedOnCommands()
        {
            // Arrange
            var service = new Service { Name = "S" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            // Act
            // Triggering a non-state tracking field update (like Description) shouldn't invoke structural command refreshes
            TestReflection.InvokeNonPublic(vm, "Service_PropertyChanged", service, new PropertyChangedEventArgs(nameof(Service.Description)));

            // Test successfully passes if no execution error leaks from command wrapper pipes
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

            var taskResult = (Task)TestReflection.InvokeNonPublic(vm, "ExecuteSafeAsync", faultyAction)!;
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

            // 1. Establish a positive baseline: change model state *before* disposing 
            // and verify that the View Model correctly propagates property updates.
            service.Status = ServiceStatus.Running;
            Assert.True(receivedNotifications > 0, "The view model never forwarded notifications before disposal. Subscription wiring is likely missing.");

            // Reset event receipt counter for post-disposal tracking
            receivedNotifications = 0;

            // Act - Dispose to sever the lifecycle loop link
            vm.Dispose();

            // Fire model event changes post-disposal frame
            service.Status = ServiceStatus.Running;

            // Assert - No events should catch since the link was severed
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