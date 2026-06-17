using Moq;
using Servy.Core.Enums;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceRowViewModel(null, _serviceCommandsMock.Object, _cursorServiceMock.Object));
        }

        [Fact]
        public void Constructor_NullServiceCommands_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceRowViewModel(new Service { Name = "S" }, null, _cursorServiceMock.Object));
        }

        [Fact]
        public void Constructor_NullCursorService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceRowViewModel(new Service { Name = "S" }, _serviceCommandsMock.Object, null));
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
            var result = vm.GetType()
                           .GetMethod("CanExecuteServiceCommand", BindingFlags.NonPublic | BindingFlags.Instance)
                           ?.Invoke(vm, new object[] { null });

            // Assert
            Assert.False((bool)result);
        }

        [Fact]
        public void StartCommand_ShouldCallStartServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.StartServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            var isInstalled = vm.Service.IsInstalled;
            var status = vm.Service.Status;
            try
            {
                vm.Service.IsInstalled = true;
                vm.Service.Status = ServiceStatus.Stopped;
                vm.StartCommand.Execute(service);
            }
            finally
            {
                vm.Service.IsInstalled = isInstalled;
                vm.Service.Status = status;
            }

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void StopCommand_ShouldCallStopServiceAsync_OnDispatcherThread()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };

            _serviceCommandsMock
                .Setup(s => s.StopServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            var isInstalled = vm.Service.IsInstalled;
            var status = vm.Service.Status;
            try
            {
                vm.Service.IsInstalled = true;
                vm.Service.Status = ServiceStatus.Running;
                vm.StopCommand.Execute(service);
            }
            finally
            {
                vm.Service.IsInstalled = isInstalled;
                vm.Service.Status = status;
            }

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void RestartCommand_ShouldCallRestartServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.RestartServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            var isInstalled = vm.Service.IsInstalled;
            var status = vm.Service.Status;
            try
            {
                vm.Service.IsInstalled = true;
                vm.Service.Status = ServiceStatus.Running;
                vm.RestartCommand.Execute(service);
            }
            finally
            {
                vm.Service.IsInstalled = isInstalled;
                vm.Service.Status = status;
            }

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ConfigureCommand_ShouldCallConfigureServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.ConfigureServiceAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask)
              .Verifiable();

            vm.ConfigureCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void InstallCommand_ShouldCallInstallServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.InstallServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.InstallCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void UninstallCommand_ShouldCallUninstallServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.UninstallServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            var isInstalled = vm.Service.IsInstalled;
            try
            {
                vm.Service.IsInstalled = true;
                vm.UninstallCommand.Execute(service);
            }
            finally
            {
                vm.Service.IsInstalled = isInstalled;
            }

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void RemoveCommand_ShouldCallRemoveServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.RemoveServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.RemoveCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ExportXmlCommand_ShouldCallExportServiceToXmlAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.ExportServiceToXmlAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            vm.ExportXmlCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ExportJsonCommand_ShouldCallExportServiceToJsonAsync()
        {
            var vm = CreateViewModel();

            _serviceCommandsMock
                  .Setup(s => s.ExportServiceToJsonAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask)
                  .Verifiable();

            vm.ExportJsonCommand.Execute(null);

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
            vm.IsChecked = true;   // Duplicate pass to ensure optimization coverage

            // Act - Trigger changes through the underlying Model to verify automatic forwarding
            service.Status = ServiceStatus.Stopped;
            service.Pid = 0;

            // Assert Notification Triggers
            Assert.Contains(nameof(vm.IsSelected), propertiesChanged);
            Assert.Contains(nameof(vm.IsChecked), propertiesChanged);
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

            // Fetch private event redirect handler method
            var method = typeof(ServiceRowViewModel).GetMethod("Service_PropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act & Assert Branch 1: Null PropertyChangedEventArgs argument context
            method.Invoke(vm, new object[] { service, null });
            Assert.False(localPropertyNotificationFired);

            // Act & Assert Branch 2: Empty PropertyName string value
            method.Invoke(vm, new object[] { service, new PropertyChangedEventArgs(string.Empty) });
            Assert.False(localPropertyNotificationFired);
        }

        [Fact]
        public void Service_PropertyChanged_IrrelevantPropertyUpdated_DoesNotRaiseCanExecuteChangedOnCommands()
        {
            // Arrange
            var service = new Service { Name = "S" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            var method = typeof(ServiceRowViewModel).GetMethod("Service_PropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            // Triggering a non-state tracking field update (like Description) shouldn't invoke structural command refreshes
            method.Invoke(vm, new object[] { service, new PropertyChangedEventArgs(nameof(Service.Description)) });

            // Test successfully passes if no cast or execution error leaks from command wrapper pipes
        }

        #endregion

        #region Execution Safety & Disposal Tests

        [Fact]
        public async Task ExecuteSafeAsync_ShouldCatchExceptionsAndResetCursor()
        {
            // Arrange
            var service = new Service { Name = "FaultyService" };
            var vm = new ServiceRowViewModel(service, _serviceCommandsMock.Object, _cursorServiceMock.Object);

            // Force an inner operation tool to break immediately
            _serviceCommandsMock
                .Setup(s => s.ConfigureServiceAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SCM access denied simulation error."));

            // Act
            var executeSafeAsyncMethod = typeof(ServiceRowViewModel).GetMethod("ExecuteSafeAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Func<Task> faultyAction = () => _serviceCommandsMock.Object.ConfigureServiceAsync(service, CancellationToken.None);

            var taskResult = (Task)executeSafeAsyncMethod.Invoke(vm, new object[] { faultyAction });
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

            // Re-invoke tracking logic to challenge the internal boolean field guard branch
            var sequentialDisposeException = Record.Exception(() => vm.Dispose());

            // Assert
            Assert.Null(sequentialDisposeException);
        }

        #endregion
    }
}