using Moq;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Commands;
using Servy.UI.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Xunit;

namespace Servy.Manager.UnitTests
{
    public class MainViewModelTests
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IServiceRepository> _serviceRepositoryMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IHelpService> _helpServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;

        public MainViewModelTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
            _serviceRepositoryMock = new Mock<IServiceRepository>();
            _loggerMock = new Mock<ILogger>();
            _helpServiceMock = new Mock<IHelpService>();
            _serviceCommandsMock = new Mock<IServiceCommands>();

            if (Application.Current == null)
            {
                new App(); // Application instance
            }
        }

        private MainViewModel CreateViewModel()
        {
            return new MainViewModel(
                _loggerMock.Object,
                _serviceManagerMock.Object,
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _helpServiceMock.Object
            );
        }

        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm.ServicesView);
            Assert.NotNull(vm.ServiceCommands);
            Assert.NotNull(vm.SearchCommand);
            Assert.False(vm.IsBusy);
            Assert.Equal("Search", vm.SearchButtonText);
        }

        [Fact]
        public void SearchCommand_ShouldPopulateServicesView()
        {

            Helper.RunOnSTA(async () =>
            {
                    var vm = CreateViewModel();
                    var services = new List<Service>
                    {
                        new Service { Name = "S1" },
                        new Service { Name = "S2" }
                    };

                    _serviceCommandsMock
                        .Setup(s => s.SearchServicesAsync(It.IsAny<string>()))
                        .ReturnsAsync(services);

                    await ((IAsyncCommand)vm.SearchCommand).ExecuteAsync(null);

                    var view = (ListCollectionView)vm.ServicesView;
                    Assert.Equal(2, view.Cast<ServiceRowViewModel>().Count());
                    Assert.Contains(view.Cast<ServiceRowViewModel>(), s => s.Service.Name == "S1");
                    Assert.Contains(view.Cast<ServiceRowViewModel>(), s => s.Service.Name == "S2");
            });
        }


        [Fact]
        public void SearchText_Setter_ShouldRaisePropertyChanged()
        {
            // Arrange
            var vm = CreateViewModel();
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "SearchText")
                    propertyChangedRaised = true;
            };

            // Act
            vm.SearchText = "Test";

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal("Test", vm.SearchText);
        }

        [Fact]
        public void RemoveService_ShouldRemoveServiceFromCollection()
        {
            // Arrange
            var vm = CreateViewModel();
            var service1 = new Service { Name = "S1" };
            var service2 = new Service { Name = "S2" };

            var srvm1 = new ServiceRowViewModel(service1, _serviceCommandsMock.Object, _loggerMock.Object);
            var srvm2 = new ServiceRowViewModel(service2, _serviceCommandsMock.Object, _loggerMock.Object);

            var servicesField = vm.GetType().GetField("_services", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var servicesList = (List<ServiceRowViewModel>)servicesField?.GetValue(vm);
            servicesList?.AddRange(new[] { srvm1, srvm2 });
            vm.ServicesView.Refresh();

            // Act
            vm.RemoveService("S1");

            // Assert
            Assert.Single(vm.ServicesView.Cast<ServiceRowViewModel>());
            Assert.Equal("S2", vm.ServicesView.Cast<ServiceRowViewModel>().First().Service.Name);
        }

        [Fact]
        public void ConfigureCommand_ShouldCallConfigureServiceAsync()
        {
            var vm = CreateViewModel();
            var service = new Service { Name = "S" };
            _serviceCommandsMock.Setup(s => s.ConfigureServiceAsync(service))
                .Returns(Task.CompletedTask)
                .Verifiable();

            vm.ConfigureCommand.Execute(service);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ImportXmlCommand_ShouldCallImportXmlConfigAsync()
        {
            var vm = CreateViewModel();
            _serviceCommandsMock.Setup(s => s.ImportXmlConfigAsync()).Returns(Task.CompletedTask).Verifiable();

            vm.ImportXmlCommand.Execute(null);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void ImportJsonCommand_ShouldCallImportJsonConfigAsync()
        {
            var vm = CreateViewModel();
            _serviceCommandsMock.Setup(s => s.ImportJsonConfigAsync()).Returns(Task.CompletedTask).Verifiable();

            vm.ImportJsonCommand.Execute(null);

            _serviceCommandsMock.Verify();
        }

        [Fact]
        public void OpenDocumentationCommand_ShouldCallHelpService()
        {
            var vm = CreateViewModel();
            _helpServiceMock.Setup(h => h.OpenDocumentation()).Verifiable();

            vm.OpenDocumentationCommand.Execute(null);

            _helpServiceMock.Verify();
        }

        [Fact]
        public void CheckUpdatesCommand_ShouldCallHelpService()
        {
            var vm = CreateViewModel();
            _helpServiceMock.Setup(h => h.CheckUpdates(It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            vm.CheckUpdatesCommand.Execute(null);

            _helpServiceMock.Verify();
        }

        [Fact]
        public void OpenAboutDialogCommand_ShouldCallHelpService()
        {
            var vm = CreateViewModel();
            _helpServiceMock.Setup(h => h.OpenAboutDialog(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            vm.OpenAboutDialogCommand.Execute(null);

            _helpServiceMock.Verify();
        }

    }
}
