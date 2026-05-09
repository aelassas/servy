using Moq;
using Servy.Core.Enums;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;

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
                new Service { Name = "S" },
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );
        }

        [Fact]
        public void CanExecuteServiceCommand_ShouldReturnFalse_WhenInternalServiceNameIsEmpty()
        {
            // Arrange
            var vm = new ServiceRowViewModel(
                new Service { Name = "" }, // internal service has empty name
                _serviceCommandsMock.Object,
                _cursorServiceMock.Object
            );

            // Act
            var result = vm.GetType()
                           .GetMethod("CanExecuteServiceCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                           ?.Invoke(vm, new object[] { null! }); // parameter is ignored

            // Assert
            Assert.False((bool)result!);
        }

        [Fact]
        public void StartCommand_ShouldCallStartServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.StartServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Verifiable();

            var isInstalled = vm.Service!.IsInstalled;
            var status = vm.Service!.Status;
            try
            {
                vm.Service!.IsInstalled = true;
                vm.Service!.Status = ServiceStatus.Stopped;
                vm.StartCommand.Execute(service);
            }
            finally
            {
                vm.Service!.IsInstalled = isInstalled;
                vm.Service!.Status = status;
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

            var isInstalled = vm.Service!.IsInstalled;
            var status = vm.Service!.Status;
            try
            {
                vm.Service!.IsInstalled = true;
                vm.Service!.Status = ServiceStatus.Running;
                vm.StopCommand.Execute(service);
            }
            finally
            {
                vm.Service!.IsInstalled = isInstalled;
                vm.Service!.Status = status;
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

            var isInstalled = vm.Service!.IsInstalled;
            var status = vm.Service!.Status;
            try
            {
                vm.Service!.IsInstalled = true;
                vm.Service!.Status = ServiceStatus.Running;
                vm.RestartCommand.Execute(service);
            }
            finally
            {
                vm.Service!.IsInstalled = isInstalled;
                vm.Service!.Status = status;
            }

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ConfigureCommand_ShouldCallConfigureServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.ConfigureServiceAsync(It.IsAny<Service>()))
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

            var isInstalled = vm.Service!.IsInstalled;
            try
            {
                vm.Service!.IsInstalled = true;
                vm.UninstallCommand.Execute(service);
            }
            finally
            {
                vm.Service!.IsInstalled = isInstalled;
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
            _serviceCommandsMock.Setup(s => s.ExportServiceToXmlAsync(It.Is<Service>(srv => srv.Name == "S")))
                .Returns(Task.CompletedTask)
                .Verifiable();

            vm.ExportXmlCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ExportJsonCommand_ShouldCallExportServiceToJsonAsync()
        {
            var vm = CreateViewModel();

            // Setup the mock to accept the Service instance from the ViewModel
            _serviceCommandsMock
                  .Setup(s => s.ExportServiceToJsonAsync(It.Is<Service>(srv => srv.Name == "S")))
                  .Returns(Task.CompletedTask)
                  .Verifiable();

            // Execute command
            vm.ExportJsonCommand.Execute(null); // or vm.Service

            // Verify
            _serviceCommandsMock.Verify();
        }



    }
}
