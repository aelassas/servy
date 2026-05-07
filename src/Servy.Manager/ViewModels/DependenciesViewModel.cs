using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Design;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.Constants;
using Servy.UI.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the Dependency view for viewing service dependencies.
    /// </summary>
    public class DependenciesViewModel : MonitoringViewModelBase
    {
        #region Fields

        private readonly IServiceRepository _serviceRepository;
        private readonly IServiceManager _serviceManager;

        private CancellationTokenSource? _loadTreeCts;

        private bool _hadSelectedService;
        private bool _disposedValue;
        private readonly IAppConfiguration _appConfig;
        private readonly IMessageBoxService _messageBoxService;

        #endregion

        #region Properties - Service Data

        private DependencyService? _selectedService;
        /// <summary>
        /// Gets or sets the currently selected service. 
        /// Changing this resets the console history and restarts file tailing for the new service paths.
        /// </summary>
        public DependencyService? SelectedService
        {
            get => _selectedService;
            set
            {
                if (ReferenceEquals(_selectedService, value)) return;
                _selectedService = value;
                OnPropertyChanged(nameof(SelectedService));
                OnPropertyChanged(nameof(IsServiceSelected));

                _ = LoadDependencyTreeAsync(null);

                CopyPidCommand?.RaiseCanExecuteChanged();

                StopMonitoring(false); // Pass false so we don't clear the zeros we just added
                StartMonitoring();
            }
        }

        /// <summary>
        /// Indicates whether a service is selected or not.
        /// </summary>
        public bool IsServiceSelected { get => SelectedService != null; }

        private ObservableCollection<ServiceDependencyNode> _dependencyTree = new ObservableCollection<ServiceDependencyNode>();

        /// <summary>
        /// Service dependency tree.
        /// </summary>
        public ObservableCollection<ServiceDependencyNode> DependencyTree
        {
            get => _dependencyTree;
            private set => Set(ref _dependencyTree, value);
        }

        #endregion

        #region Properties - UI State & Search

        private string _pid = UiConstants.NotAvailable;
        /// <summary>
        /// Gets or sets the Process ID string for display in the UI.
        /// </summary>
        public string Pid
        {
            get => _pid;
            set => Set(ref _pid, value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to copy the current Process ID to the clipboard.
        /// </summary>
        public IAsyncCommand CopyPidCommand { get; set; }

        /// <summary>
        /// Command to refresh dependency tree.
        /// </summary>
        public IAsyncCommand RefreshCommand { get; }

        /// <summary>
        /// Command to expand all dependency tree.
        /// </summary>
        public ICommand ExpandAllCommand { get; }

        /// <summary>
        /// Command to collapse all dependency tree nodes.
        /// </summary>
        public ICommand CollapseAllCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesViewModel"/> class.
        /// </summary>
        /// <param name="serviceRepository">Repository for service data access.</param>
        /// <param name="serviceManager">Service manager.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        /// <param name="appConfig">Application configuration settings.</param>
        /// <param name="cursorService">Service used to control the cursor state.</param>
        /// <param name="uiDispatcher">Dispatcher for UI thread operations.</param>
        /// <param name="messageBoxService">Service used to display modal dialogs (e.g. error popups).</param>
        public DependenciesViewModel(
            IServiceRepository? serviceRepository,
            IServiceManager serviceManager,
            IServiceCommands serviceCommands,
            IAppConfiguration appConfig,
            ICursorService cursorService,
            IUiDispatcher uiDispatcher,
            IMessageBoxService messageBoxService) : base(cursorService, uiDispatcher)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            ServiceCommands = serviceCommands ?? throw new ArgumentNullException(nameof(serviceCommands));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null, name: nameof(CopyPidCommand));
            RefreshCommand = new AsyncCommand(LoadDependencyTreeAsync, name: nameof(RefreshCommand));
            ExpandAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, true));
            CollapseAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, false));

            InitTimer();
        }

        /// <summary>
        /// Design-Time constructor.
        /// </summary>
        public DependenciesViewModel() : this(
            new UI.Design.DesignTimeServiceRepository(),
            new UI.Design.DesignTimeServiceManager(),
            new DesignTimeServiceCommands(),
            new DesignTimeAppConfig(),
            new UI.Design.DesignTimeCursorService(),
            new UI.Design.DesignTimeUiDispatcher(),
            new UI.Design.DesignTimeMessageBoxService()
            )
        { }

        #endregion

        #region MonitoringViewModelBase Implementation

        /// <inheritdoc/>
        protected override int RefreshIntervalMs => _appConfig.DependenciesRefreshIntervalInMs;

        /// <inheritdoc/>
        protected override ServiceItemBase CreateServiceItem(Service? service)
        {
            return new DependencyService { Name = service?.Name, Pid = null };
        }

        /// <inheritdoc/>
        protected override async Task OnTickAsync()
        {
            var token = _monitoringCts?.Token ?? CancellationToken.None;

            try
            {
                var currentSelection = SelectedService;
                if (currentSelection == null)
                {
                    if (_hadSelectedService)
                    {
                        ResetPid();
                        _hadSelectedService = false;
                        CopyPidCommand?.RaiseCanExecuteChanged();
                    }
                    return;
                }
                _hadSelectedService = true;

                var currentPid = await _serviceRepository.GetServicePidAsync(currentSelection.Name, token);

                if (!currentPid.HasValue)
                {
                    ResetPid();
                    if (SelectedService != null)
                    {
                        SelectedService.Pid = null;
                    }
                    CopyPidCommand?.RaiseCanExecuteChanged();
                    return;
                }

                if (currentSelection.Pid != currentPid)
                {
                    currentSelection.Pid = currentPid;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }

                SetPidText(currentSelection);
            }
            catch (OperationCanceledException)
            {
                // Expected during app shutdown or when the ViewModel is deactivated.
                // No logging required as this is a normal lifecycle event.
            }
            catch (Exception ex)
            {
                // Log the error so it's visible in 'Servy.Manager.log'
                // This ensures developers can diagnose why the UI stopped updating.
                Logger.Error($"Background tick failed in {GetType().Name}", ex);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Recursively sets the expansion state of the specified collection of dependency nodes and all their children.
        /// </summary>
        /// <param name="nodes">The collection of <see cref="ServiceDependencyNode"/> to process.</param>
        /// <param name="isExpanded">
        /// <see langword="true"/> to expand the nodes; <see langword="false"/> to collapse them.
        /// </param>
        private void SetExpansion(IEnumerable<ServiceDependencyNode> nodes, bool isExpanded)
        {
            if (nodes == null) return;

            // Use a HashSet to track visited nodes and prevent infinite recursion in case of 
            // circular dependencies or shared nodes.
            var visited = new HashSet<ServiceDependencyNode>();
            SetExpansionRecursive(nodes, isExpanded, visited);
        }

        /// <summary>
        /// Performs the recursive traversal to update the expansion state of dependency nodes while 
        /// protecting against circular references.
        /// </summary>
        /// <param name="nodes">The collection of <see cref="ServiceDependencyNode"/> to process.</param>
        /// <param name="isExpanded">
        /// <see langword="true"/> to expand the nodes; <see langword="false"/> to collapse them.
        /// </param>
        /// <param name="visited">
        /// A tracking set used to detect already-processed nodes. This prevents infinite recursion 
        /// and potential <see cref="StackOverflowException"/> crashes if the dependency tree 
        /// contains cycles or shared nodes.
        /// </param>
        /// <remarks>
        /// This internal helper ensures the stability of the management layer 
        /// by providing a cycle guard during deep tree traversal, a critical safety measure when 
        /// handling complex Win32 SCM dependency graphs.
        /// </remarks>
        private void SetExpansionRecursive(IEnumerable<ServiceDependencyNode> nodes, bool isExpanded, HashSet<ServiceDependencyNode> visited)
        {
            foreach (var node in nodes)
            {
                // If we've already processed this node in this traversal, skip it.
                if (!visited.Add(node)) continue;

                node.IsExpanded = isExpanded;

                if (node.Dependencies != null)
                {
                    SetExpansionRecursive(node.Dependencies, isExpanded, visited);
                }
            }
        }

        /// <summary>
        /// Updates the PID display text based on the selected service's current state.
        /// </summary>
        /// <param name="service">Service model.</param>
        private void SetPidText(ServiceItemBase service)
        {
            var pidTxt = service.Pid?.ToString() ?? UiConstants.NotAvailable;
            if (Pid != pidTxt) Pid = pidTxt;
        }

        /// <summary>
        /// Resets PID text.
        /// </summary>
        private void ResetPid()
        {
            Pid = UiConstants.NotAvailable;
        }

        /// <summary>
        /// Copies the Process ID of the currently selected service to the system clipboard.
        /// </summary>
        /// <param name="parameter">Unused command parameter.</param>
        private async Task CopyPidAsync(object? parameter)
        {
            if (ServiceCommands == null)
            {
                Logger.Warn("ServiceCommands is null. Cannot copy PID.");
                return;
            }

            if (SelectedService?.Pid != null)
            {
                var service = ServiceMapper.ToModel(SelectedService);
                await ServiceCommands.CopyPid(service);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Asynchronously retrieves and sets the dependency tree for the current selected service.
        /// </summary>
        public async Task LoadDependencyTreeAsync(object? parameter)
        {
            // 1. Thread-safe atomic swap to cancel any existing load operation
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _loadTreeCts, newCts);

            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var token = newCts.Token;

            try
            {
                IsBusy = true;
                DependencyTree.Clear();

                if (SelectedService == null) return;

                var serviceName = SelectedService.Name;

                // 2. Offload the synchronous SCM call to a background thread
                var root = await Task.Run(() =>
                    _serviceManager.GetDependencies(serviceName), token);

                // 3. Check if we are still on the same service/task before updating UI
                if (token.IsCancellationRequested) return;

                if (root != null)
                {
                    root.IsExpanded = true;
                    DependencyTree.Add(root);
                }

                OnPropertyChanged(nameof(DependencyTree));
            }
            catch (OperationCanceledException) { /* Ignored */ }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load dependency tree for {SelectedService?.Name}", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_FailedToLoadDependencyTree, AppConfig.Caption);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Cleans up resources, cancels background tasks, and explicitly unsubscribes 
        /// from timer events to prevent memory leaks.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Dispose Tree Loading CTS
                    var oldLoadTreeCts = Interlocked.Exchange(ref _loadTreeCts, null);
                    if (oldLoadTreeCts != null)
                    {
                        oldLoadTreeCts.Cancel();
                        oldLoadTreeCts.Dispose();
                    }
                }

                base.Dispose(disposing);
                _disposedValue = true;
            }
        }

        #endregion
    }
}