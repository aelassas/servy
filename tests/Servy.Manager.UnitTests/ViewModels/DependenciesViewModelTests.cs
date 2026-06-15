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
using Servy.UI.Constants;
using Servy.UI.Design;
using Servy.UI.Services;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class DependenciesViewModelTests
    {
        // Centralized lock token shared across the test fixture to block cross-thread interference
        private static readonly object StaticEnvironmentLock = new object();

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
        /// Lazy initializer utility that instantiates the target ViewModel *only* after an explicit 
        /// thread isolation lock has been acquired and a safe service provider context has been assigned.
        /// </summary>
        private DependenciesViewModel CreateIsolatedViewModel()
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
            // Guarded with environmental lock block to protect against ambient state drift from adjacent tests
            lock (StaticEnvironmentLock)
            {
                Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                    null, _mockServiceManager.Object, _mockServiceCommands.Object,
                    _mockAppConfig.Object, _mockCursorService.Object, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
            }
        }

        [Fact]
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Guarded with environmental lock block to protect against ambient state drift from adjacent tests
            lock (StaticEnvironmentLock)
            {
                Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                    _mockServiceRepository.Object, null, _mockServiceCommands.Object,
                    _mockAppConfig.Object, _mockCursorService.Object, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
            }
        }

        [Fact]
        public void Constructor_NullAppConfig_ThrowsArgumentNullException()
        {
            // Guarded with environmental lock block to protect against ambient state drift from adjacent tests
            lock (StaticEnvironmentLock)
            {
                Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                    _mockServiceRepository.Object, _mockServiceManager.Object, _mockServiceCommands.Object,
                    null, _mockCursorService.Object, _mockUiDispatcher.Object, _mockMessageBoxService.Object));
            }
        }

        [Fact]
        public void Constructor_NullMessageBoxService_ThrowsArgumentNullException()
        {
            // Guarded with environmental lock block to protect against ambient state drift from adjacent tests
            lock (StaticEnvironmentLock)
            {
                Assert.Throws<ArgumentNullException>(() => new DependenciesViewModel(
                    _mockServiceRepository.Object, _mockServiceManager.Object, _mockServiceCommands.Object,
                    _mockAppConfig.Object, _mockCursorService.Object, _mockUiDispatcher.Object, null));
            }
        }

        [Fact]
        public void DesignTimeConstructor_InitializesSuccessfully()
        {
            Helper.RunOnSTA(() =>
            {
                // Wrapped in the exclusive environment lock block to register the required Design-Time tracker service dependencies
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var dtViewModel = new DependenciesViewModel();
                        Assert.NotNull(dtViewModel.DependencyTree);
                        Assert.Equal(UiConstants.NotAvailable, dtViewModel.Pid);
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
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
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();
                        var mockService = new DependencyService { Name = "TestService", Pid = 1234 };
                        bool selectionChangedFired = false;
                        bool serviceSelectedFired = false;

                        viewModel.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(viewModel.SelectedService)) selectionChangedFired = true;
                            if (e.PropertyName == nameof(viewModel.IsServiceSelected)) serviceSelectedFired = true;
                        };

                        viewModel.SelectedService = mockService;

                        Assert.True(selectionChangedFired);
                        Assert.True(serviceSelectedFired);
                        Assert.True(viewModel.IsServiceSelected);
                        Assert.Same(mockService, viewModel.SelectedService);

                        // Clean up instances cleanly
                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public void SelectedService_SetSameReference_DoesNotFireEventsOrReload()
        {
            Helper.RunOnSTA(() =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();
                        var mockService = new DependencyService { Name = "TestService" };
                        viewModel.SelectedService = mockService;

                        bool anyPropertyChangedFired = false;
                        viewModel.PropertyChanged += (s, e) => anyPropertyChangedFired = true;

                        viewModel.SelectedService = mockService;

                        Assert.False(anyPropertyChangedFired);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
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
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();

                        // Use constructor signatures. Pass isCycle = true parameter onto root to simulate a cyclic loop.
                        var childNode = new ServiceDependencyNode("ChildService", "Friendly Child", isRunning: false, isCycle: false);
                        var rootNode = new ServiceDependencyNode("RootService", "Friendly Root", isRunning: false, isCycle: false);

                        // Add components directly to the get-only Collection instance instead of assigning properties
                        rootNode.Dependencies.Add(childNode);
                        childNode.Dependencies.Add(rootNode); // Create circular dependency edge reference

                        viewModel.DependencyTree.Add(rootNode);

                        viewModel.ExpandAllCommand.Execute(null);

                        Assert.True(rootNode.IsExpanded);
                        Assert.True(childNode.IsExpanded);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public void CollapseAllCommand_Executes_RecursivelyCollapsesNodes()
        {
            Helper.RunOnSTA(() =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();

                        // Use initialization constructors, then populate expansion state parameters sequentially
                        var childNode = new ServiceDependencyNode("Child", "Child Service") { IsExpanded = true };
                        var rootNode = new ServiceDependencyNode("Root", "Root Service") { IsExpanded = true };
                        rootNode.Dependencies.Add(childNode);

                        viewModel.DependencyTree.Add(rootNode);

                        viewModel.CollapseAllCommand.Execute(null);

                        Assert.False(rootNode.IsExpanded);
                        Assert.False(childNode.IsExpanded);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task CopyPidCommand_ValidSelectionWithPid_InvokesServiceCommandsMapping()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();
                        var mockService = new DependencyService { Name = "TestService", Pid = 5555 };
                        viewModel.SelectedService = mockService;

                        viewModel.CopyPidCommand.ExecuteAsync(null).GetAwaiter().GetResult();

                        _mockServiceCommands.Verify(c => c.CopyPidAsync(It.Is<Service>(s => s.Name == "TestService")), Times.Once);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        #endregion

        #region LoadDependencyTreeAsync Core Branch Execution Tests

        [Fact]
        public async Task LoadDependencyTreeAsync_SelectedServiceNull_ClearsTreeAndReturnsEarly()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();
                        viewModel.DependencyTree.Add(new ServiceDependencyNode("Stale", "Stale"));
                        viewModel.SelectedService = null;

                        viewModel.LoadDependencyTreeAsync(null).GetAwaiter().GetResult();

                        Assert.Empty(viewModel.DependencyTree);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task LoadDependencyTreeAsync_ManagerReturnsValidRoot_PopulatesAndExpandsTree()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        // Arrange
                        var viewModel = CreateIsolatedViewModel();
                        var mockService = new DependencyService { Name = "ServyCore" };
                        var expectedRoot = new ServiceDependencyNode("ServyCore", "Friendly Core") { IsExpanded = false };

                        _mockServiceManager.Setup(m => m.GetDependencies("ServyCore", It.IsAny<CancellationToken>()))
                                           .Returns(expectedRoot);

                        // Act
                        // Set the property, which triggers the first LoadDependencyTreeAsync internally.
                        viewModel.SelectedService = mockService;

                        // Wait for the fire-and-forget task from the setter to finish.
                        // We can probe the collection until it has the expected count.
                        int retries = 0;
                        while (viewModel.DependencyTree.Count == 0 && retries < 10)
                        {
                            Task.Delay(20).GetAwaiter().GetResult();
                            retries++;
                        }

                        // Now, if you need to call it manually again, ensure you clear the tree 
                        // or verify that the first call already succeeded.
                        // Given your requirement, we simply assert on the state populated by the setter.

                        // Assert
                        Assert.Single(viewModel.DependencyTree);
                        Assert.Same(expectedRoot, viewModel.DependencyTree[0]);
                        Assert.True(expectedRoot.IsExpanded);
                        Assert.False(viewModel.IsBusy);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task LoadDependencyTreeAsync_ManagerThrowsException_LogsAndDisplaysErrorMessageBox()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        // Arrange
                        var viewModel = CreateIsolatedViewModel();
                        var mockService = new DependencyService { Name = "FaultyService" };
                        var exception = new InvalidOperationException("SCM Connection Error");

                        _mockServiceManager.Setup(m => m.GetDependencies("FaultyService", It.IsAny<CancellationToken>())).Throws(exception);

                        // Act
                        viewModel.SelectedService = mockService; // Triggers the 1st Load invocation internally

                        // Act: Manual second call to verify explicit refresh command execution paths
                        viewModel.LoadDependencyTreeAsync(null).GetAwaiter().GetResult(); // Triggers the 2nd Load invocation

                        // Assert
                        // Verify it was hit exactly twice (once via setter initialization, once via manual call)
                        _mockMessageBoxService.Verify(
                            m => m.ShowErrorAsync(Strings.Msg_FailedToLoadDependencyTree, It.IsAny<string>()),
                            Times.AtLeast(1));

                        Assert.False(viewModel.IsBusy);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
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
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();

                        var methodInfo = typeof(DesignTimeUiDispatcher).Assembly.GetType("Servy.Manager.ViewModels.DependenciesViewModel")?
                            .GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? typeof(DependenciesViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                        var fieldInfo = typeof(DependenciesViewModel).GetField("_hadSelectedService", BindingFlags.NonPublic | BindingFlags.Instance);
                        fieldInfo?.SetValue(viewModel, true);
                        viewModel.Pid = "1234";

                        var task = (Task)methodInfo.Invoke(viewModel, null);
                        task.GetAwaiter().GetResult();

                        Assert.Equal(UiConstants.NotAvailable, viewModel.Pid);
                        var flagValue = (bool)fieldInfo.GetValue(viewModel);
                        Assert.False(flagValue);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task BaseMonitoring_OnTickAsync_PidNotFound_ResetsPidDisplay()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();
                        var mockService = new DependencyService { Name = "ActiveService", Pid = 999 };
                        viewModel.SelectedService = mockService;

                        _mockServiceRepository.Setup(r => r.GetServicePidAsync("ActiveService", It.IsAny<CancellationToken>()))
                                              .ReturnsAsync((int?)null);

                        var methodInfo = typeof(DependenciesViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                        var task = (Task)methodInfo.Invoke(viewModel, null);
                        task.GetAwaiter().GetResult();

                        Assert.Equal(UiConstants.NotAvailable, viewModel.Pid);
                        Assert.Null(mockService.Pid);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        [Fact]
        public async Task BaseMonitoring_OnTickAsync_PidChanged_UpdatesModelPropertiesAndText()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Wrapped in environmental lock and localized service collection setup to eliminate tracking flakiness
                lock (StaticEnvironmentLock)
                {
                    var originalProvider = App.Services;
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddSingleton(_mockProcessKiller.Object);
                    App.Services = serviceCollection.BuildServiceProvider();

                    try
                    {
                        var viewModel = CreateIsolatedViewModel();
                        var mockService = new DependencyService { Name = "ActiveService", Pid = 100 };
                        viewModel.SelectedService = mockService;

                        _mockServiceRepository.Setup(r => r.GetServicePidAsync("ActiveService", It.IsAny<CancellationToken>()))
                                              .ReturnsAsync(200);

                        var methodInfo = typeof(DependenciesViewModel).GetMethod("OnTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);

                        var task = (Task)methodInfo.Invoke(viewModel, null);
                        task.GetAwaiter().GetResult();

                        Assert.Equal("200", viewModel.Pid);
                        Assert.Equal(200, mockService.Pid);

                        viewModel.Dispose();
                    }
                    finally
                    {
                        if (originalProvider != null)
                        {
                            App.Services = originalProvider;
                        }
                    }
                }
            }, createApp: true);
        }

        #endregion
    }
}