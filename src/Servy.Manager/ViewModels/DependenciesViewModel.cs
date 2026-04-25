using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.Constants;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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

        private CancellationTokenSource _loadTreeCts;

        private bool _hadSelectedService;
        private bool _disposedValue;
        private readonly IAppConfiguration _appConfig;

        #endregion

        #region Properties - Service Data

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

                _ = LoadDependencyTreeAsync(null);

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
        /// Command to refresh depdency treer.
        /// </summary>
        public IAsyncCommand RefreshCommand { get; }

        /// <summary>
        /// Command to expand all depdency treer.
        /// </summary>
        public ICommand ExpandAllCommand { get; }

        /// <summary>
        /// Command to collapse all depdency treer.
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
        public DependenciesViewModel(
            IServiceRepository serviceRepository,
            IServiceManager serviceManager,
            IServiceCommands serviceCommands,
            IAppConfiguration appConfig)
        {
            _serviceRepository = serviceRepository;
            _serviceManager = serviceManager;
            ServiceCommands = serviceCommands;
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));

            SearchCommand = new AsyncCommand(SearchServicesAsync);
            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null);
            RefreshCommand = new AsyncCommand(LoadDependencyTreeAsync);
            ExpandAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, true));
            CollapseAllCommand = new RelayCommand<object>(_ => SetExpansion(DependencyTree, false));

            InitTimer();
        }

        #endregion

        #region MonitoringViewModelBase Implementation

        /// <inheritdoc/>
        protected override int RefreshIntervalMs => _appConfig.DependenciesRefreshIntervalInMs;

        /// <inheritdoc/>
        protected override ServiceItemBase CreateServiceItem(Service service)
        {
            return new DependencyService { Name = service.Name, Pid = null };
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
                    SelectedService.Pid = null;
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
        /// Searches for services based on the <see cref="SearchText"/>.
        /// Updates the <see cref="Services"/> collection on the UI thread.
        /// </summary>
        /// <param name="parameter">Unused command parameter.</param>
        private async Task SearchServicesAsync(object parameter)
        {
            // Thread-safe atomic swap for left-panel search
            // This prevents searching from destroying the active monitoring token
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _serviceSearchCts, newCts);
            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var token = newCts.Token;

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

        #endregion

        #region Public Methods

        /// <summary>
        /// Asynchronously retrieves and sets the dependency tree for the current selected service.
        /// </summary>
        public async Task LoadDependencyTreeAsync(object parameter)
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
                    // 1. Dispose Monitoring CTS
                    var oldMonitoringCts = Interlocked.Exchange(ref _monitoringCts, null);
                    if (oldMonitoringCts != null)
                    {
                        oldMonitoringCts.Cancel();
                        oldMonitoringCts.Dispose();
                    }

                    // 2. Dispose Tree Loading CTS
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