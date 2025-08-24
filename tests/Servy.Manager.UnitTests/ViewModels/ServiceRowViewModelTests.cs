using Moq;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class ServiceRowViewModelTests
    {
        private readonly Mock<Service> _serviceMock;
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IServiceRepository> _serviceRepositoryMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IHelpService> _helpServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;

        public ServiceRowViewModelTests()
        {
            _serviceMock = new Mock<Service>();
            _serviceManagerMock = new Mock<IServiceManager>();
            _serviceRepositoryMock = new Mock<IServiceRepository>();
            _loggerMock = new Mock<ILogger>();
            _helpServiceMock = new Mock<IHelpService>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
        }

        private ServiceRowViewModel CreateViewModel()
        {
            return new ServiceRowViewModel(
                new Service { Name = "S" },
                _serviceCommandsMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public void CanExecuteServiceCommand_ShouldReturnFalse_WhenInternalServiceNameIsEmpty()
        {
            // Arrange
            var vm = new ServiceRowViewModel(
                new Service { Name = "" }, // internal service has empty name
                _serviceCommandsMock.Object,
                _loggerMock.Object
            );

            // Act
            var result = vm.GetType()
                           .GetMethod("CanExecuteServiceCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                           ?.Invoke(vm, new object[] { null }); // parameter is ignored

            // Assert
            Assert.False((bool)result);
        }

        [Fact]
        public void StartCommand_ShouldCallStartServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.StartServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<bool>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.StartCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }


        [Fact]
        public void StopCommand_ShouldCallStopServiceAsync_OnDispatcherThread()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };

            _serviceCommandsMock
                .Setup(s => s.StopServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<bool>()))
                .ReturnsAsync(true)
                .Verifiable();

            // Run async command on dispatcher
            vm.StopCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }


        [Fact]
        public void RestartCommand_ShouldCallRestartServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.RestartServiceAsync(It.Is<Service>(srv => srv.Name == "S"), It.IsAny<bool>()))
                .ReturnsAsync(true)
                .Verifiable();

            vm.RestartCommand.Execute(service);

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
            _serviceCommandsMock.Setup(s => s.InstallServiceAsync(It.Is<Service>(srv => srv.Name == "S")))
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
            _serviceCommandsMock.Setup(s => s.UninstallServiceAsync(It.Is<Service>(srv => srv.Name == "S")))
                .ReturnsAsync(true)
                .Verifiable();

            vm.UninstallCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void RemoveCommand_ShouldCallRemoveServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.RemoveServiceAsync(It.Is<Service>(srv => srv.Name == "S")))
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
