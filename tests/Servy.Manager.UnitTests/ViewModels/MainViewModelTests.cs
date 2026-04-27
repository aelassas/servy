using Moq;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI;
using Servy.UI.Services;
using System.Windows.Data;
using System.Windows.Threading;

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
            _cursorServiceMock = new Mock<ICursorService>(); // Initialize CursorService Mock
            _processHelper = new Mock<IProcessHelper>();

            // Setup AppConfig Mock with defaults to prevent null/zero issues
            _appConfigMock = new Mock<IAppConfiguration>();
            _appConfigMock.Setup(c => c.RefreshIntervalInSeconds).Returns(5);
            _appConfigMock.Setup(c => c.PerformanceRefreshIntervalInMs).Returns(1000);
            _appConfigMock.Setup(c => c.ConsoleMaxLines).Returns(500);
            _appConfigMock.Setup(c => c.DependenciesRefreshIntervalInMs).Returns(1000);
            _appConfigMock.Setup(c => c.ConsoleRefreshIntervalInMs).Returns(500);

            // Concrete classes with parameters need the objects passed to the Mock constructor
            // Added _cursorServiceMock.Object to all child ViewModel mocks
            _performanceViewModelMock = new Mock<PerformanceViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object);

            _consoleViewModelMock = new Mock<ConsoleViewModel>(
                _serviceRepositoryMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object);

            _dependenciesViewModelMock = new Mock<DependenciesViewModel>(
                _serviceRepositoryMock.Object,
                _serviceManagerMock.Object,
                _serviceCommandsMock.Object,
                _appConfigMock.Object,
                _cursorServiceMock.Object);
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
                _cursorServiceMock.Object, // Pass the new dependency
                _processHelper.Object,
                dispatcher
            );
        }

        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            Helper.RunOnSTA(async () =>
            {
                // Arrange & Act
                var vm = CreateViewModel();

                // Assert
                Assert.NotNull(vm.ServicesView);
                Assert.NotNull(vm.ServiceCommands);
                Assert.NotNull(vm.SearchCommand);
                Assert.False(vm.IsBusy);
                // "Search" comes from Strings.Button_Search; ensure localized resources are loaded
                Assert.Contains("Search", vm.SearchButtonText);
            }, createApp: true);
        }

        [Fact]
        public void SearchCommand_ShouldPopulateServicesView()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel(Dispatcher.CurrentDispatcher);
                var services = new List<Service?>
                {
                    new Service { Name = "S1" },
                    new Service { Name = "S2" }
                };

                _serviceCommandsMock
                    .Setup(s => s.SearchServicesAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(services);

                await vm.SearchCommand.ExecuteAsync(null);

                var view = (ListCollectionView)vm.ServicesView;
                var items = view.Cast<ServiceRowViewModel>().ToList();

                Assert.Equal(2, items.Count);
                Assert.Contains(items, s => s?.Service?.Name == "S1");
                Assert.Contains(items, s => s?.Service?.Name == "S2");
            }, createApp: true);
        }

        [Fact]
        public void SearchText_Setter_ShouldRaisePropertyChanged()
        {
            Helper.RunOnSTA(async () =>
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
            }, createApp: true);
        }

        [Fact]
        public void RemoveService_ShouldRemoveServiceFromCollection()
        {
            Helper.RunOnSTA(async () =>
            {
                // Arrange
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                var vm = CreateViewModel(currentDispatcher);
                var service1 = new Service { Name = "S1" };
                var service2 = new Service { Name = "S2" };

                var srvm1 = new ServiceRowViewModel(service1, _serviceCommandsMock.Object);
                var srvm2 = new ServiceRowViewModel(service2, _serviceCommandsMock.Object);

                // Use reflection to access the private _services collection for setup
                var servicesField = typeof(MainViewModel).GetField("_services", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var servicesList = (BulkObservableCollection<ServiceRowViewModel>?)servicesField?.GetValue(vm);

                servicesList?.Add(srvm1);
                servicesList?.Add(srvm2);
                vm.ServicesView.Refresh();

                // Act
                vm.RemoveService("S1");

                // Assert
                var items = vm.ServicesView.Cast<ServiceRowViewModel>().ToList();
                Assert.Single(items);
                Assert.Equal("S2", items.First()?.Service?.Name);
            }, createApp: true);
        }

        [Fact]
        public void ConfigureCommand_ShouldCallConfigureServiceAsync()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                var service = new Service { Name = "S" };
                _serviceCommandsMock.Setup(s => s.ConfigureServiceAsync(service))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await vm.ConfigureCommand.ExecuteAsync(service);

                _serviceCommandsMock.Verify();
            }, createApp: true);
        }

        [Fact]
        public void ImportXmlCommand_ShouldCallImportXmlConfigAsync()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                _serviceCommandsMock.Setup(s => s.ImportXmlConfigAsync())
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await vm.ImportXmlCommand.ExecuteAsync(null);

                _serviceCommandsMock.Verify();
            }, createApp: true);
        }

        [Fact]
        public void ImportJsonCommand_ShouldCallImportJsonConfigAsync()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                _serviceCommandsMock.Setup(s => s.ImportJsonConfigAsync())
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await vm.ImportJsonCommand.ExecuteAsync(null);

                _serviceCommandsMock.Verify();
            }, createApp: true);
        }

        [Fact]
        public void OpenDocumentationCommand_ShouldCallHelpService()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                _helpServiceMock.Setup(h => h.OpenDocumentation(It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await vm.OpenDocumentationCommand.ExecuteAsync(null);

                _helpServiceMock.Verify();
            }, createApp: true);
        }

        [Fact]
        public void CheckUpdatesCommand_ShouldCallHelpService()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                _helpServiceMock.Setup(h => h.CheckUpdates(It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await vm.CheckUpdatesCommand.ExecuteAsync(null);

                _helpServiceMock.Verify();
            }, createApp: true);
        }

        [Fact]
        public void OpenAboutDialogCommand_ShouldCallHelpService()
        {
            Helper.RunOnSTA(async () =>
            {
                var vm = CreateViewModel();
                _helpServiceMock.Setup(h => h.OpenAboutDialog(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await vm.OpenAboutDialogCommand.ExecuteAsync(null);

                _helpServiceMock.Verify();
            }, createApp: true);
        }
    }
}