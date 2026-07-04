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
        public async Task DesignTimeConstructor_InitializesSuccessfully()
        {
            await Helper.RunOnSTA(async () =>
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
                await Task.CompletedTask;
            }, createApp: true);
        }

        #endregion

        #region Property & Selection Mutation Tracking Tests

        [Fact]
        public async Task SelectedService_ChangeSelection_FiresNotifyPropertyChangedEvents()
        {
            await Helper.RunOnSTA(async () =>
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
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task SelectedService_SetSameReference_DoesNotFireEventsOrReload()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();
                        var mockService = new DependencyService { Name = "TestService" };
                        viewModel.SelectedService = mockService;

                        bool anyPropertyChangedFired = false;
                        viewModel.PropertyChanged += (s, e) => anyPropertyChangedFired = true;

                        // Act
                        viewModel.SelectedService = mockService;

                        // Assert
                        Assert.False(anyPropertyChangedFired);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
                await Task.CompletedTask;
            }, createApp: true);
        }

        #endregion

        #region Command Traversal & Tree Expansion Structure Tests

        [Fact]
        public async Task ExpandAllCommand_Executes_RecursivelyExpandsNodesWithCycleGuard()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();

                        // Create a circular Dependencies edge (root -> child -> root) to exercise the ExpandAll recursion/cycle guard;
                        // the isCycle constructor flag is unrelated and left false.
                        var childNode = new ServiceDependencyNode("ChildService", "Friendly Child", isRunning: false, isCyclic: false);
                        var rootNode = new ServiceDependencyNode("RootService", "Friendly Root", isRunning: false, isCyclic: false);

                        // Add components directly to the get-only Collection instance instead of assigning properties
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
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task CollapseAllCommand_Executes_RecursivelyCollapsesNodes()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                using (new AmbientAppServicesScope(sc => sc.AddSingleton(_mockProcessKiller.Object)))
                {
                    DependenciesViewModel viewModel = null;
                    try
                    {
                        viewModel = CreateViewModel();

                        // Use initialization constructors, then populate expansion state parameters sequentially
                        var childNode = new ServiceDependencyNode("Child", "Root Service") { IsExpanded = true };
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
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task CopyPidCommand_ValidSelectionWithPid_InvokesServiceCommandsMapping()
        {
            await Helper.RunOnSTA(async () =>
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
                        // Directly await the asynchronous execution flow instead of slamming message loops synchronously
                        viewModel.CopyPidCommand.ExecuteAsync(null).GetAwaiter().GetResult();

                        // Assert
                        _mockServiceCommands.Verify(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "TestService"), It.IsAny<CancellationToken>()), Times.Once);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
                await Task.CompletedTask;
            }, createApp: true);
        }

        #endregion

        #region LoadDependencyTreeAsync Core Branch Execution Tests

        [Fact]
        public async Task LoadDependencyTreeAsync_SelectedServiceNull_ClearsTreeAndReturnsEarly()
        {
            await Helper.RunOnSTA(async () =>
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
                        // Directly await the tree reset operation asynchronously inside the test pipeline boundary
                        viewModel.LoadDependencyTreeAsync(null).GetAwaiter().GetResult();

                        // Assert
                        Assert.Empty(viewModel.DependencyTree);
                    }
                    finally
                    {
                        viewModel?.Dispose();
                    }
                }
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task LoadDependencyTreeAsync_ManagerReturnsValidRoot_PopulatesAndExpandsTree()
        {
            // Await the underlying asynchronous STA execution task natively
            await Helper.RunOnSTA(async () =>
            {
                // REMOVED lock: xUnit's Collection Fixture handles serialization safely.
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
                        // Triggers the first asynchronous fire-and-forget load routine
                        viewModel.SelectedService = mockService;

                        // Use a true non-blocking await loop to allow the STA dispatcher message pump 
                        // to process incoming UI collection modification updates concurrently.
                        int retries = 0;
                        while (viewModel.DependencyTree.Count == 0 && retries < 25)
                        {
                            await Task.Delay(20, CancellationToken.None); // Safe asynchronous yield
                            retries++;
                        }

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
            // Await the underlying asynchronous STA execution task natively
            await Helper.RunOnSTA(async () =>
            {
                // REMOVED lock: xUnit's Collection Fixture handles serialization safely.
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
                        int retries = 0;
                        while (viewModel.IsBusy && retries < 25)
                        {
                            await Task.Delay(20);
                            retries++;
                        }

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
        public async Task BaseMonitoring_OnTickAsync_SelectionNull_ResetsDisplaysAndClearsFlag()
        {
            await Helper.RunOnSTA(async () =>
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
                        // Explicitly await the reflected task continuation instead of slamming the thread context synchronously
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
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task BaseMonitoring_OnTickAsync_PidNotFound_ResetsPidDisplay()
        {
            await Helper.RunOnSTA(async () =>
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
                        // Explicitly await the structural worker tick thread asynchronously
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
                await Task.CompletedTask;
            }, createApp: true);
        }

        [Fact]
        public async Task BaseMonitoring_OnTickAsync_PidChanged_UpdatesModelPropertiesAndText()
        {
            await Helper.RunOnSTA(async () =>
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
                        // Explicitly await the monitoring tick background routine asynchronously
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
                await Task.CompletedTask;
            }, createApp: true);
        }

        #endregion
    }
}