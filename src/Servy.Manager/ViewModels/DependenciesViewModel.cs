using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.Constants;
using Servy.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the Dependency view for viewing service dependencies.
    /// </summary>
    public class DependenciesViewModel : ViewModelBase
    {
        #region Fields

        private readonly IServiceRepository _serviceRepository;
        private readonly IServiceManager _serviceManager;
        private DispatcherTimer _timer;
        private CancellationTokenSource _cts;
        private bool _hadSelectedService;
        private int _isMonitoringFlag = 0; // 0 = Stopped, 1 = Monitoring
        private int _isTickRunningFlag = 0; // 0 = Idle, 1 = Processing

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
        /// Initializes a new instance of the <see cref="DependenciesViewModel"/> class.
        /// </summary>
        /// <param name="serviceRepository">Repository for service data access.</param>
        /// <param name="serviceManager">Service manager.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        public DependenciesViewModel(
            IServiceRepository serviceRepository,
            IServiceManager serviceManager,
            IServiceCommands serviceCommands)
        {
            _serviceRepository = serviceRepository;
            _serviceManager = serviceManager;
            ServiceCommands = serviceCommands;
            _cts = new CancellationTokenSource();
            SearchCommand = new AsyncCommand(SearchServicesAsync);
            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null);
            RefreshCommand = new RelayCommand<object>(_ => LoadDependencyTree());
            ExpandAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, true));
            CollapseAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, false));

            InitTimer();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures the <see cref="DispatcherTimer"/> used to poll process performance metrics.
        /// </summary>
        /// <remarks>
        /// The timer interval is retrieved from the global <see cref="App.PerformanceRefreshIntervalInMs"/> configuration.
        /// This method hooks the <see cref="OnTick"/> event handler, which is responsible for triggering 
        /// the asynchronous update of CPU and RAM counters.
        /// </remarks>
        private void InitTimer()
        {
            if (_timer == null)
            {
                var app = (App)Application.Current;
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(app.PerformanceRefreshIntervalInMs) };
                _timer.Tick += OnTick;
            }
        }

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

            _timer?.Stop();
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
                    _timer?.Start();
                }
            }
        }

        /// <summary>
        /// Asynchronously fetches the latest service status and updates the UI accordingly.
        /// Handles log path changes if the service restarts with a new PID.
        /// </summary>
        private async Task OnTickAsync()
        {
            // Capture the token at the start of the tick. 
            // If _cts is null, we shouldn't be here, but we guard it anyway.
            var token = _cts?.Token ?? CancellationToken.None;

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
                    SelectedService.Pid = null;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                    return;
                }

                if (currentSelection.Pid != currentPid)
                {
                    currentSelection.Pid = currentPid;   // write to captured local, not SelectedService
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }

                SetPidText();
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
            ResetCts();
            var token = _cts.Token;

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
                Logger.Error($"Failed to search services.", ex);
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

        /// <summary>
        /// Resets the <see cref="CancellationTokenSource"/> by cancelling any in-flight operations 
        /// and disposing of the existing instance before creating a fresh one.
        /// </summary>
        /// <remarks>
        /// This is used to ensure that when monitoring restarts (e.g., after a service restart or 
        /// tab navigation), the new polling cycle is controlled by an active, non-cancelled token.
        /// </remarks>
        private void ResetCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
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
                    root.IsExpanded = true;
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
            // Ensure we have a fresh, active cancellation token
            ResetCts();

            // Atomically signal start
            Interlocked.Exchange(ref _isMonitoringFlag, 1);

            // Start timer
            InitTimer();
            _timer?.Start();
        }

        /// <summary>
        /// Disables the monitoring timer and optionally resets the console view.
        /// </summary>
        public void StopMonitoring()
        {
            _cts?.Cancel();
            Interlocked.Exchange(ref _isMonitoringFlag, 0);
            _timer?.Stop();
        }

        /// <summary>
        /// Cleans up resources, cancels background tasks, and explicitly unsubscribes 
        /// from timer events to prevent memory leaks during tab navigation.
        /// </summary>
        public void Cleanup()
        {
            // 1. Cancel and dispose the CancellationTokenSource
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            // 2. Stop the timer, unsubscribe from the Tick event, and release the reference
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick; // CRITICAL: Prevents the Dispatcher from keeping the VM alive
                _timer = null;
            }
        }

        #endregion
    }
}