using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Constants;
using Servy.UI.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the Dependency view, responsible for real-time log tailing, 
    /// service monitoring, and log filtering.
    /// </summary>
    public class DependenciesViewModel : ViewModelBase
    {

        #region Fields

        private readonly IServiceRepository _serviceRepository;
        private readonly IServiceManager _serviceManager;
        private readonly DispatcherTimer _timer;
        private readonly ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _hadSelectedService;
        private int _isMonitoringFlag = 0; // 0 = Stopped, 1 = Monitoring
        private int _isTickRunningFlag = 0; // 0 = Idle, 1 = Processing

        #endregion

        #region Properties - Log Data

        /// <summary>
        /// Gets the full collection of log lines received from the service.
        /// </summary>
        public BulkObservableCollection<LogLine> RawLines { get; } = new BulkObservableCollection<LogLine>();

        /// <summary>
        /// Gets the filtered view of log lines based on the <see cref="DependencySearchText"/>.
        /// </summary>
        public ICollectionView VisibleLines { get; }

        #endregion

        #region Properties - Service Data

        /// <summary>
        /// Gets the collection of services available for console viewing and monitoring.
        /// </summary>
        public ObservableCollection<ServiceItemBase> Services { get; } = new ObservableCollection<ServiceItemBase>();

        private DependencyService _selectedService;
        /// <summary>
        /// Gets or sets the currently selected service. 
        /// Changing this resets the console history and restarts file tailing for the new service paths.
        /// </summary>
        public DependencyService SelectedService
        {
            get => _selectedService;
            set
            {
                if (ReferenceEquals(_selectedService, value)) return;
                _selectedService = value;
                OnPropertyChanged(nameof(SelectedService));
                OnPropertyChanged(nameof(IsServiceSelected));

                LoadDependencyTree();

                CopyPidCommand?.RaiseCanExecuteChanged();

                StopMonitoring(); // Pass false so we don't clear the zeros we just added
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

        private string _searchText;
        /// <summary>
        /// Gets or sets the text used for searching the service list (left panel).
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => Set(ref _searchText, value);
        }

        private string _searchButtonText;
        /// <summary>
        /// Gets or sets the text displayed on the search button.
        /// </summary>
        public string SearchButtonText
        {
            get => _searchButtonText;
            set => Set(ref _searchButtonText, value);
        }

        private bool _isBusy;
        /// <summary>
        /// Gets or sets a value indicating whether an asynchronous operation is in progress.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

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
        /// Gets or sets the commands for managing service state (Start/Stop/Restart).
        /// </summary>
        public IServiceCommands ServiceCommands { get; set; }

        /// <summary>
        /// Command to execute the service search.
        /// </summary>
        public IAsyncCommand SearchCommand { get; }

        /// <summary>
        /// Command to copy the current Process ID to the clipboard.
        /// </summary>
        public IAsyncCommand CopyPidCommand { get; set; }

        /// <summary>
        /// Command to refresh depdency treer.
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// Command to expand all depdency treer.
        /// </summary>
        public ICommand ExpandAllCommand { get; }

        /// <summary>
        /// Command to collapse all depdency treer.
        /// </summary>
        public ICommand CollapseAllCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyViewModel"/> class.
        /// Sets up log filtering and refresh timers.
        /// </summary>
        /// <param name="serviceRepository">Repository for service data access.</param>
        /// <param name="serviceManager">Service manager.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        /// <param name="logger">Logger for diagnostic operations.</param>
        public DependenciesViewModel(IServiceRepository serviceRepository,
            IServiceManager serviceManager,
            IServiceCommands serviceCommands,
            ILogger logger)
        {
            _serviceRepository = serviceRepository;
            _serviceManager = serviceManager;
            ServiceCommands = serviceCommands;
            SearchCommand = new AsyncCommand(SearchServicesAsync);
            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null);
            RefreshCommand = new RelayCommand<object>(_ => LoadDependencyTree());
            ExpandAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, true));
            CollapseAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, false));
            _logger = logger;

            var app = (App)Application.Current;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(app.DependenciesRefreshIntervalInMs) };
            _timer.Tick += OnTick;
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
        /// <remarks>
        /// This method performs a deep traversal of the dependency tree. 
        /// Ensure that the UI is bound to the <see cref="ServiceDependencyNode.IsExpanded"/> 
        /// property for the changes to reflect visually.
        /// </remarks>
        private void SetExpansion(IEnumerable<ServiceDependencyNode> nodes, bool isExpanded)
        {
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                node.IsExpanded = isExpanded;
                SetExpansion(node.Dependencies, isExpanded);
            }
        }

        /// <summary>
        /// Updates the PID display text based on the selected service's current state.
        /// </summary>
        private void SetPidText()
        {
            var pidTxt = SelectedService.Pid?.ToString() ?? UiConstants.NotAvailable;
            if (Pid != pidTxt) Pid = pidTxt;
        }

        /// <summary>
        /// Handles the timer tick for performance monitoring. 
        /// Uses interlocked flags to prevent re-entrancy and "resurrection" of the timer after stopping.
        /// </summary>
        private async void OnTick(object sender, EventArgs e)
        {
            // 1. Atomic Guard: Must be monitoring AND not already running a tick
            if (Interlocked.CompareExchange(ref _isMonitoringFlag, 1, 1) == 0 ||
                Interlocked.CompareExchange(ref _isTickRunningFlag, 1, 0) == 1)
            {
                return;
            }

            _timer.Stop();
            try
            {
                await OnTickAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _isTickRunningFlag, 0);

                // 2. Only restart if we are STILL supposed to be monitoring
                if (Interlocked.CompareExchange(ref _isMonitoringFlag, 1, 1) == 1)
                {
                    _timer.Start();
                }
            }
        }

        /// <summary>
        /// Asynchronously fetches the latest service status and updates the UI accordingly.
        /// Handles log path changes if the service restarts with a new PID.
        /// </summary>
        private async Task OnTickAsync()
        {
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

                var serviceDto = await _serviceRepository.GetByNameAsync(currentSelection.Name);

                if (serviceDto?.Pid == null)
                {
                    ResetPid();
                    SelectedService.Pid = null;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                    return;
                }

                if (currentSelection.Pid != serviceDto.Pid)
                {
                    SelectedService.Pid = serviceDto.Pid;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }

                SetPidText();
            }
            catch
            {
                // Silently ignore errors to prevent log bloating and keep the UI stable.
            }
        }

        /// <summary>
        /// Resets PID text.
        /// </summary>
        private void ResetPid()
        {
            Pid = UiConstants.NotAvailable;
        }

        /// <summary>
        /// Searches for services based on the <see cref="SearchText"/>.
        /// Updates the <see cref="Services"/> collection on the UI thread.
        /// </summary>
        /// <param name="parameter">Unused command parameter.</param>
        private async Task SearchServicesAsync(object parameter)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                IsBusy = true;
                SearchButtonText = Strings.Button_Searching;

                if (Application.Current?.Dispatcher != null && !Helper.IsRunningInUnitTest())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }

                var results = await ServiceCommands.SearchServicesAsync(SearchText, false, token);

                Services.Clear();
                foreach (var s in results)
                {
                    Services.Add(new DependencyService { Name = s.Name, Pid = null });
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled; no action needed.
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to search services: {ex}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
                SearchButtonText = Strings.Button_Search;
            }
        }

        /// <summary>
        /// Copies the Process ID of the currently selected service to the system clipboard.
        /// </summary>
        /// <param name="parameter">Unused command parameter.</param>
        private async Task CopyPidAsync(object parameter)
        {
            if (SelectedService?.Pid != null)
            {
                var service = ServiceMapper.ToModel(SelectedService);
                await ServiceCommands.CopyPid(service);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Retrives and sets the depdency tree for the current selected service.
        /// </summary>
        public void LoadDependencyTree()
        {
            DependencyTree.Clear(); // Reset the collection
            if (_selectedService != null)
            {
                // Get the root node
                var root = _serviceManager.GetDependencies(_selectedService.Name);

                if (root != null)
                {
                    DependencyTree.Add(root); // Wrap the single root in the collection
                }
            }

            // Notify UI (The Set method usually handles this, but good to be explicit if using Clear/Add)
            OnPropertyChanged(nameof(DependencyTree));
        }

        /// <summary>
        /// Enables the monitoring timer to begin polling for service status updates.
        /// </summary>
        public void StartMonitoring()
        {
            Interlocked.Exchange(ref _isMonitoringFlag, 1);
            _timer.Start();
        }

        /// <summary>
        /// Disables the monitoring timer and optionally resets the console view.
        /// </summary>
        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
            Interlocked.Exchange(ref _isMonitoringFlag, 0);
            _timer.Stop();
        }

        #endregion

    }
}