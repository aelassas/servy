using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.Testing;
using Servy.UI.Constants;
using Servy.UI.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Helper = Servy.Testing.Helper;

namespace Servy.Manager.UnitTests.ViewModels
{
    [Collection("Ambient AppServices Dependent Tests")]
    public class DependenciesViewModelTests
    {
        private readonly Mock<IServiceRepository> _mockServiceRepository;
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly Mock<IServiceCommands> _mockServiceCommands;
        private readonly Mock<IAppConfiguration> _mockAppConfig;
        private readonly Mock<ICursorService> _mockCursorService;
        private readonly Mock<IUiDispatcher> _mockUiDispatcher;
        private readonly Mock<IMessageBoxService> _mockMessageBoxService;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public DependenciesViewModelTests()
        {
            _mockServiceRepository = new Mock<IServiceRepository>();
            _mockServiceManager = new Mock<IServiceManager>();
            _mockServiceCommands = new Mock<IServiceCommands>();
            _mockAppConfig = new Mock<IAppConfiguration>();
            _mockCursorService = new Mock<ICursorService>();
            _mockUiDispatcher = new Mock<IUiDispatcher>();
            _mockMessageBoxService = new Mock<IMessageBoxService>();
            _mockProcessKiller = new Mock<IProcessKiller>();

            _mockAppConfig.Setup(c => c.DependenciesRefreshIntervalInMs).Returns(1000);

            _mockUiDispatcher.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                             .Callback<Action>(action => action())
                             .Returns(Task.CompletedTask);
        }

        /// <summary>
        /// Initializer utility that instantiates the target ViewModel.
        /// </summary>
        private DependenciesViewModel CreateViewModel()
        {
            return new DependenciesViewModel(
                _mockServiceRepository.Object,
                _mockServiceManager.Object,
                _mockServiceCommands.Object,
                _mockAppConfig.Object,
                _mockCursorService.Object,
                _mockUiDispatcher.Object,
                _mockMessageBoxService.Object);
        }

        #region Constructor & Initialization Guard Tests

        [Fact]
        public void Constructor_NullServiceRepository_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                null, _mockServiceManager.Object, _mockServiceCommands.Object,
                _mockAppConfig.Object, _mockCursorService.Object, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                _mockServiceRepository.Object, null, _mockServiceCommands.Object,
                _mockAppConfig.Object, _mockCursorService.Object, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullAppConfig_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                _mockServiceRepository.Object, _mockServiceManager.Object, _mockServiceCommands.Object,
                null, _mockCursorService.Object, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullMessageBoxService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                _mockServiceRepository.Object, _mockServiceManager.Object, _mockServiceCommands.Object,
                _mockAppConfig.Object, _mockCursorService.Object, _mockUiDispatcher.Object, null));
        }

        [Fact]
        public void Constructor_NullServiceCommands_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                _mockServiceRepository.Object, _mockServiceManager.Object, null,
                _mockAppConfig.Object, _mockCursorService.Object, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullCursorService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                _mockServiceRepository.Object, _mockServiceManager.Object, _mockServiceCommands.Object,
                _mockAppConfig.Object, null, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
        }

        [Fact]
        public void Constructor_NullUiDispatcher_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                _mockServiceRepository.Object, _mockServiceManager.Object, _mockServiceCommands.Object,
                _mockAppConfig.Object, _mockCursorService.Object, null, _mockMessageBoxService.Object));
        }

        [Fact]
        public void DesignTimeConstructor_InitializesSuccessfully()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel dtViewModel = null;
                    try
                    {
                        // Act
                        dtViewModel = new DependenciesViewModel();

                        // Assert
                        Assert.NotNull(dtViewModel.DependencyTree);
                        Assert.Equal(UiConstants.NotAvailable, dtViewModel.Pid);
                    }
                    finally
                    {
                        dtViewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        #endregion

        #region Property & Selection Mutation Tracking Tests

        [Fact]
        public void SelectedService_ChangeSelection_FiresNotifyPropertyChangedEvents()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "TestService", Pid = 1234 };
                        bool selectionChangedFired = false;
                        bool serviceSelectedFired = false;

                        viewModel.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(viewModel.SelectedService)) selectionChangedFired = true;
                            if (e.PropertyName == nameof(viewModel.IsServiceSelected)) serviceSelectedFired = true;
                        };

                        // Act
                        viewModel.SelectedService = mockService;

                        // Assert
                        Assert.True(selectionChangedFired);
                        Assert.True(serviceSelectedFired);
                        Assert.True(viewModel.IsServiceSelected);
                        Assert.Same(mockService, viewModel.SelectedService);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public void SelectedService_SetSameReference_DoesNotFireEventsOrReload()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "TestService" };

                        // First assignment: Legritimately triggers the initial dependency load pipeline loop
                        viewModel.SelectedService = mockService;

                        bool anyPropertyChangedFired = false;
                        viewModel.PropertyChanged += (s, e) => anyPropertyChangedFired = true;

                        // Act
                        // Second assignment: Same exact reference. Should short-circuit completely.
                        viewModel.SelectedService = mockService;

                        // Assert
                        // 1. Verify the notification suppression pathway holds true
                        Assert.False(anyPropertyChangedFired);

                        // 2. Verify the 'OrReload' optimization contract.
                        // If the same-reference early return guard behaves properly, the backend repository 
                        // round-trip should have been hit EXACTLY once during the first initialization. 
                        // A count of 2 indicates that the redundant second reload leaked past the guard.
                        _mockServiceManager.Verify(m => m.GetDependencies("TestService", It.IsAny<CancellationToken>()), Times.Once);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        #endregion

        #region Command Traversal & Tree Expansion Structure Tests

        [Fact]
        public void ExpandAllCommand_Executes_RecursivelyExpandsNodesWithCycleGuard()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();

                        var childNode = new ServiceDependencyNode("ChildService", "Friendly Child", isRunning: false, isCyclic: false);
                        var rootNode = new ServiceDependencyNode("RootService", "Friendly Root", isRunning: false, isCyclic: false);

                        rootNode.Dependencies.Add(childNode);
                        childNode.Dependencies.Add(rootNode); // Create circular dependency edge reference

                        viewModel.DependencyTree.Add(rootNode);

                        // Act
                        viewModel.ExpandAllCommand.Execute(null);

                        // Assert
                        Assert.True(rootNode.IsExpanded);
                        Assert.True(childNode.IsExpanded);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public void CollapseAllCommand_Executes_RecursivelyCollapsesNodes()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();

                        var childNode = new ServiceDependencyNode("Child", "Child Service") { IsExpanded = true };
                        var rootNode = new ServiceDependencyNode("Root", "Root Service") { IsExpanded = true };
                        rootNode.Dependencies.Add(childNode);

                        viewModel.DependencyTree.Add(rootNode);

                        // Act
                        viewModel.CollapseAllCommand.Execute(null);

                        // Assert
                        Assert.False(rootNode.IsExpanded);
                        Assert.False(childNode.IsExpanded);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public void CopyPidCommand_ValidSelectionWithPid_InvokesServiceCommandsMapping()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "TestService", Pid = 5555 };
                        viewModel.SelectedService = mockService;

                        // Act
                        viewModel.CopyPidCommand.ExecuteAsync(null).GetAwaiter().GetResult();

                        // Assert
                        _mockServiceCommands.Verify(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "TestService"), It.IsAny<CancellationToken>()), Times.Once);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        #endregion

        #region LoadDependencyTreeAsync Core Branch Execution Tests

        [Fact]
        public void LoadDependencyTreeAsync_SelectedServiceNull_ClearsTreeAndReturnsEarly()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();
                        viewModel.DependencyTree.Add(new ServiceDependencyNode("Stale", "Stale"));
                        viewModel.SelectedService = null;

                        // Act
                        viewModel.LoadDependencyTreeAsync(null).GetAwaiter().GetResult();

                        // Assert
                        Assert.Empty(viewModel.DependencyTree);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task LoadDependencyTreeAsync_ManagerReturnsValidRoot_PopulatesAndExpandsTree()
        {
            await Helper.RunOnSTA(async () =>
            {
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        // Arrange
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "ServyCore" };
                        var expectedRoot = new ServiceDependencyNode("ServyCore", "Friendly Core") { IsExpanded = false };

                        _mockServiceManager.Setup(m => m.GetDependencies("ServyCore", It.IsAny<CancellationToken>()))
                                           .Returns(expectedRoot);

                        // Act
                        viewModel.SelectedService = mockService;

                        // allow the STA dispatcher message pump to process incoming UI collection modification updates concurrently.
                        await Helper.WaitUntilAsync(
                            () => viewModel.DependencyTree.Count > 0,
                            TimeSpan.FromSeconds(2),
                            TimeSpan.FromMilliseconds(20),
                            CancellationToken.None);

                        // Assert
                        Assert.Single(viewModel.DependencyTree);
                        Assert.Same(expectedRoot, viewModel.DependencyTree[0]);
                        Assert.True(expectedRoot.IsExpanded);
                        Assert.False(viewModel.IsBusy);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task LoadDependencyTreeAsync_ManagerThrowsException_LogsAndDisplaysErrorMessageBox()
        {
            await Helper.RunOnSTA(async () =>
            {
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        // Arrange
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "FaultyService" };
                        var exception = new InvalidOperationException("SCM Connection Error");

                        _mockServiceManager.Setup(m => m.GetDependencies("FaultyService", It.IsAny<CancellationToken>())).Throws(exception);

                        // Act
                        viewModel.SelectedService = mockService; // Triggers the 1st Load invocation internally

                        // Await until the fire-and-forget task kicked off by the property setter 
                        // completes its internal catch/finally blocks before continuing.
                        await Helper.WaitUntilAsync(
                            () => !viewModel.IsBusy,
                            TimeSpan.FromSeconds(2),
                            TimeSpan.FromMilliseconds(20),
                            CancellationToken.None);

                        // Act: Manual second call to verify explicit refresh command execution paths
                        // Fully await the execution task asynchronously to keep the UI dispatcher pump fluid
                        await viewModel.LoadDependencyTreeAsync(null); // Triggers the 2nd Load invocation

                        // Assert
                        // Verify it was hit exactly twice (once via setter initialization, once via manual call)
                        _mockMessageBoxService.Verify(
                            m => m.ShowErrorAsync(Strings.Msg_FailedToLoadDependencyTree, It.IsAny<string>()),
                            Times.Exactly(2));

                        Assert.False(viewModel.IsBusy);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        #endregion

        #region Background Worker Loop Ticking Framework Evaluation Tests

        [Fact]
        public void BaseMonitoring_OnTickAsync_SelectionNull_ResetsDisplaysAndClearsFlag()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();

                        TestReflection.SetField(viewModel, "_hadSelectedService", true);
                        viewModel.Pid = "1234";

                        // Act
                        var task = (Task)TestReflection.InvokeNonPublic(viewModel, "OnTickAsync");
                        task.GetAwaiter().GetResult();

                        // Assert
                        Assert.Equal(UiConstants.NotAvailable, viewModel.Pid);
                        var flagValue = TestReflection.GetField<bool>(viewModel, "_hadSelectedService");
                        Assert.False(flagValue);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public void BaseMonitoring_OnTickAsync_PidNotFound_ResetsPidDisplay()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "ActiveService", Pid = 999 };
                        viewModel.SelectedService = mockService;

                        _mockServiceRepository.Setup(r => r.GetServicePidAsync("ActiveService", It.IsAny<CancellationToken>()))
                                              .ReturnsAsync((int?)null);

                        // Act
                        var task = (Task)TestReflection.InvokeNonPublic(viewModel, "OnTickAsync");
                        task.GetAwaiter().GetResult();

                        // Assert
                        Assert.Equal(UiConstants.NotAvailable, viewModel.Pid);
                        Assert.Null(mockService.Pid);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public void BaseMonitoring_OnTickAsync_PidChanged_UpdatesModelPropertiesAndText()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "ActiveService", Pid = 100 };
                        viewModel.SelectedService = mockService;

                        _mockServiceRepository.Setup(r => r.GetServicePidAsync("ActiveService", It.IsAny<CancellationToken>()))
                                              .ReturnsAsync(200);

                        // Act
                        var task = (Task)TestReflection.InvokeNonPublic(viewModel, "OnTickAsync");
                        task.GetAwaiter().GetResult();

                        // Assert
                        Assert.Equal("200", viewModel.Pid);
                        Assert.Equal(200, mockService.Pid);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
            }, createApp: true);
        }

        #endregion
    }
}