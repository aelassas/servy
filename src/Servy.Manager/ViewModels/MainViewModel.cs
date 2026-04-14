using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the main window of Servy Manager.
    /// Holds the list of services and exposes commands for managing them.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Constants

        /// <summary>
        /// Maximum number of concurrent SCM operations during bulk start/stop/restart.
        /// Caps at this value to prevent SCM saturation regardless of core count.
        /// </summary>
        private const int MaxBulkOperationParallelism = 8;

        #endregion

        #region Private Fields

        private readonly Dispatcher _dispatcher;
        private readonly IServiceManager _serviceManager;
        private readonly IServiceRepository _serviceRepository;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IHelpService _helpService;

        private CancellationTokenSource _cts;

        private DispatcherTimer _refreshTimer;
        private readonly ObservableCollection<ServiceRowViewModel> _services = new ObservableCollection<ServiceRowViewModel>();
        private bool _isBusy;
        private string _searchButtonText = Strings.Button_Search;
        private bool _isConfiguratorEnabled = false;
        private string _searchText;
        private string _footerText;
        private bool? _selectAll;
        private bool _isUpdatingSelectAll;
        private readonly object _servicesLock = new object();
        private int _isRefreshingFlag = 0; // 0 = false, 1 = true

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// Used for data binding updates.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property name.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the performance view model.
        /// </summary>
        public PerformanceViewModel PerformanceVM { get; }

        /// <summary>
        /// Gets the console view model.
        /// </summary>
        public ConsoleViewModel ConsoleVM { get; }

        /// <summary>
        /// Get the dependencies view model.
        /// </summary>
        public DependenciesViewModel DependenciesVM { get; }

        /// <summary>
        /// Indicates whether a background operation is running.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets footer text displayed in the UI.
        /// </summary>
        public string FooterText
        {
            get => _footerText;
            set
            {
                if (_footerText != value)
                {
                    _footerText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Text displayed on the search button.
        /// </summary>
        public string SearchButtonText
        {
            get => _searchButtonText;
            set
            {
                if (_searchButtonText != value)
                {
                    _searchButtonText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the search text used for filtering or querying services.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                }
            }
        }

        private IServiceCommands _serviceCommands;

        /// <summary>
        /// The set of service commands available for each service row.
        /// </summary>
        public IServiceCommands ServiceCommands
        {
            get => _serviceCommands;
            set
            {
                _serviceCommands = value;

                if (PerformanceVM != null)
                {
                    PerformanceVM.ServiceCommands = value;
                }
                if (ConsoleVM != null)
                {
                    ConsoleVM.ServiceCommands = value;
                }
                if (DependenciesVM != null)
                {
                    DependenciesVM.ServiceCommands = value;
                }
            }
        }

        /// <summary>
        /// Collection of services displayed in the DataGrid.
        /// </summary>
        public ICollectionView ServicesView { get; private set; }

        /// <summary>
        /// Indicates whether any services are currently selected.
        /// </summary>
        public bool HasSelectedServices => _services.Any(s => s.IsChecked);

        /// <summary>
        /// Gets or sets the tri-state "Select All" value for the services.
        /// </summary>
        public bool? SelectAll
        {
            get => _selectAll;
            set
            {
                if (_selectAll == value) return;

                _selectAll = value;
                OnPropertyChanged();

                if (!_isUpdatingSelectAll)
                {
                    _isUpdatingSelectAll = true;

                    bool targetState = (value == true);

                    // Optimization: Use a local list to avoid multiple enumeration if _services is large
                    foreach (var service in _services)
                    {
                        service.IsChecked = targetState;
                        service.IsSelected = false;
                    }

                    _isUpdatingSelectAll = false;

                    // This updates the header state based on the children's new values
                    UpdateSelectAllState();
                }
            }
        }

        /// <summary>
        /// Determines whether the configuration app launch button is enabled.
        /// </summary>
        public bool IsConfiguratorEnabled
        {
            get => _isConfiguratorEnabled;
            set
            {
                if (_isConfiguratorEnabled != value)
                {
                    _isConfiguratorEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to search services.
        /// </summary>
        public IAsyncCommand SearchCommand { get; }

        /// <summary>
        /// Command to open the configuration for a service.
        /// </summary>
        public IAsyncCommand ConfigureCommand { get; }

        /// <summary>
        /// Command to browse and import an XML configuration file.
        /// </summary>
        public IAsyncCommand ImportXmlCommand { get; }

        /// <summary>
        /// Command to browse and import a JSON configuration file.
        /// </summary>
        public IAsyncCommand ImportJsonCommand { get; }

        /// <summary>
        /// Start selected services command.
        /// </summary>
        public IAsyncCommand StartSelectedCommand { get; }

        /// <summary>
        /// Stop selected services command.
        /// </summary>
        public IAsyncCommand StopSelectedCommand { get; }

        /// <summary>
        /// Restart selected services command.
        /// </summary>
        public IAsyncCommand RestartSelectedCommand { get; }

        /// <summary>
        /// Command to open documentation.
        /// </summary>
        public ICommand OpenDocumentationCommand { get; }

        /// <summary>
        /// Command to check for updates.
        /// </summary>
        public IAsyncCommand CheckUpdatesCommand { get; }

        /// <summary>
        /// Command to open about dialog.
        /// </summary>
        public IAsyncCommand OpenAboutDialogCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="MainViewModel"/>.
        /// </summary>
        public MainViewModel(
            IServiceManager serviceManager,
            IServiceRepository serviceRepository,
            IServiceCommands serviceCommands,
            IHelpService helpService,
            IMessageBoxService messageBoxService,
            Dispatcher dispatcher = null
            )
        {
            _serviceManager = serviceManager;
            _serviceRepository = serviceRepository;
            ServiceCommands = serviceCommands;
            _helpService = helpService;
            _messageBoxService = messageBoxService;
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _selectAll = false;

            // Create PerformanceVM only once
            PerformanceVM = new PerformanceViewModel(serviceRepository, serviceCommands);

            // Create ConsoleVM only once
            ConsoleVM = new ConsoleViewModel(serviceRepository, serviceCommands);

            // Create DependenciesVM only once
            DependenciesVM = new DependenciesViewModel(serviceRepository, serviceManager, serviceCommands);

            ServicesView = new ListCollectionView(_services);

            SearchCommand = new AsyncCommand(SearchServicesAsync);
            ConfigureCommand = new AsyncCommand(ConfigureServiceAsync);
            ImportXmlCommand = new AsyncCommand(ImportXmlConfigAsync);
            ImportJsonCommand = new AsyncCommand(ImportJsonConfigAsync);
            StartSelectedCommand = new AsyncCommand(StartSelectedAsync);
            StopSelectedCommand = new AsyncCommand(StopSelectedAsync);
            RestartSelectedCommand = new AsyncCommand(RestartSelectedAsync);
            OpenDocumentationCommand = new RelayCommand<object>(_ => OpenDocumentation());
            CheckUpdatesCommand = new AsyncCommand(CheckUpdatesAsync);
            OpenAboutDialogCommand = new AsyncCommand(OpenAboutDialog);

            var app = Application.Current as App;
            if (app != null)
            {
                IsConfiguratorEnabled = app.IsConfigurationAppAvailable;

                CreateAndStartTimer();
            }

        }

        /// <summary>
        /// Parameterless constructor for XAML designer support.
        /// </summary>
        public MainViewModel() :
            this(
                null,
                null,
                null,
                null,
                null
                )
        { }

        #endregion

        #region Private Methods/Events

        /// <summary>
        /// Triggers when a property on a service row changes.
        /// </summary>
        private void Service_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ServiceRowViewModel.IsChecked))
            {
                UpdateSelectAllState();
                OnPropertyChanged(nameof(HasSelectedServices));
            }
        }

        /// <summary>
        /// Updates the <see cref="SelectAll"/> property based on the current state of all services.
        /// </summary>
        private void UpdateSelectAllState()
        {
            if (_isUpdatingSelectAll) return;

            _isUpdatingSelectAll = true;
            try
            {
                if (_services.Any() && _services.All(s => s.IsChecked))
                {
                    SelectAll = true;
                }
                else if (_services.Any(s => s.IsChecked))
                {
                    SelectAll = null;
                }
                else
                {
                    SelectAll = false;
                }
            }
            finally
            {
                _isUpdatingSelectAll = false;
            }
        }

        /// <summary>
        /// Creates a new <see cref="DispatcherTimer"/> configured with the application's refresh interval.
        /// </summary>
        private DispatcherTimer CreateTimer()
        {
            var app = (App)Application.Current;
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(app.RefreshIntervalInSeconds)
            };

            timer.Tick += OnTick;

            return timer;
        }

        /// <summary>
        /// Handles the <see cref="DispatcherTimer.Tick"/> event for refreshing services.
        /// Stops the timer before invoking <see cref="RefreshAllServicesAsync"/> to prevent overlapping ticks,
        /// and restarts the timer afterward if it still exists.
        /// </summary>
        private async void OnTick(object sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref _isRefreshingFlag, 1, 0) == 1)
            {
                return;
            }

            try
            {
                // Local copy pattern to protect against UI navigation/Search resets
                var cts = _cts;
                if (cts == null || cts.IsCancellationRequested) return;

                var token = cts.Token;

                await RefreshAllServicesAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Clean exit
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed background refresh.", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isRefreshingFlag, 0);
            }
        }

        #endregion

        #region Service Commands

        /// <summary>
        /// Performs search of services asynchronously.
        /// </summary>
        private async Task SearchServicesAsync(object parameter)
        {
            // Thread-safe CTS swap
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _cts, newCts);
            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var token = newCts.Token;

            try
            {
                var stopwatch = Stopwatch.StartNew();

                FooterText = string.Empty; // Clear footer text before search

                // Step 1: show "Searching..." immediately
                Mouse.OverrideCursor = Cursors.Wait;
                SearchButtonText = Strings.Button_Searching;
                IsBusy = true;

                // Step 2: allow WPF to repaint the button and show progress bar
                await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                // Step 3: fetch data off UI thread
                var sw = Stopwatch.StartNew();
                var results = await Task.Run(() => ServiceCommands.SearchServicesAsync(SearchText, true, token), token);
                sw.Stop();
                Debug.WriteLine($"Created {results.Count} SearchServicesAsync in {sw.ElapsedMilliseconds} ms");

                // Step 4: fetch data & build VMs off UI thread
                sw = Stopwatch.StartNew();
                var vms = await Task.Run(() =>
                    results.Select(s => new ServiceRowViewModel(s, ServiceCommands)).ToList()
                , token);
                sw.Stop();
                Debug.WriteLine($"Created {vms.Count} ServiceRowViewModels in {sw.ElapsedMilliseconds} ms");

                // Step 5: update collection on UI thread
                await _dispatcher.InvokeAsync(() =>
                {
                    // Explicitly dispose of existing ViewModels before clearing the collection
                    foreach (var oldVm in _services)
                    {
                        oldVm.Dispose();
                    }

                    _services.Clear();

                    foreach (var vm in vms)
                    {
                        vm.PropertyChanged += Service_PropertyChanged;
                        _services.Add(vm);
                    }

                    SelectAll = false;

                    // Notify that bulk action availability changed
                    OnPropertyChanged(nameof(HasSelectedServices));
                }, DispatcherPriority.Background);

                stopwatch.Stop();
                SetFooterText(stopwatch);

                // Setp 5: refresh all service statuses and details in the background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RefreshAllServicesAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        // expected when cancelled
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"RefreshAllServicesAsync failed.", ex);
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                // expected when cancelled
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to search services from main tab.", ex);
            }
            finally
            {
                // Step 7: restore button text and IsBusy
                Mouse.OverrideCursor = null;
                SearchButtonText = Strings.Button_Search;
                IsBusy = false;
            }
        }

        /// <summary>
        /// Launches configuration for the given service.
        /// </summary>
        private async Task ConfigureServiceAsync(object parameter)
        {
            await ServiceCommands.ConfigureServiceAsync(parameter as Service);
        }

        /// <summary>
        /// Imports XML configuration for services.
        /// </summary>
        private async Task ImportXmlConfigAsync(object parameter)
        {
            await ServiceCommands.ImportXmlConfigAsync();
        }

        /// <summary>
        /// Imports JSON configuration for services.
        /// </summary>
        private async Task ImportJsonConfigAsync(object parameter)
        {
            await ServiceCommands.ImportJsonConfigAsync();
        }

        /// <summary>
        /// Start all selected services.
        /// </summary>
        private Task StartSelectedAsync(object parameter) =>
            ExecuteBulkOperationAsync(
                s => ServiceCommands.StartServiceAsync(s, showMessageBox: false),
                Strings.Confirm_StartSelectedServices,
                "Failed to start selected services");

        /// <summary>
        /// Stop all selected services.
        /// </summary>
        private Task StopSelectedAsync(object parameter) =>
            ExecuteBulkOperationAsync(
                s => ServiceCommands.StopServiceAsync(s, showMessageBox: false),
                Strings.Confirm_StopSelectedServices,
                "Failed to stop selected services");

        /// <summary>
        /// Restart all selected services.
        /// </summary>
        private Task RestartSelectedAsync(object parameter) =>
            ExecuteBulkOperationAsync(
                s => ServiceCommands.RestartServiceAsync(s, showMessageBox: false),
                Strings.Confirm_RestartSelectedServices,
                "Failed to restart selected services");

        #endregion

        #region Help/Updates/About Commands

        /// <summary>
        /// Opens the Servy documentation page in the default browser.
        /// </summary>
        private void OpenDocumentation()
        {
            _helpService.OpenDocumentation();
        }

        /// <summary>
        /// Checks for the latest Servy release on GitHub and prompts the user if an update is available.
        /// </summary>
        private async Task CheckUpdatesAsync(object parameter)
        {
            await _helpService.CheckUpdates(AppConfig.Caption);
        }

        /// <summary>
        /// Displays the "About Servy" dialog with version and copyright information.
        /// </summary>
        private async Task OpenAboutDialog(object parameter)
        {
            await _helpService.OpenAboutDialog(
               string.Format(Strings.Text_About,
               Core.Config.AppConfig.Version,
               Helper.GetBuiltWithFramework(),
               DateTime.Now.Year),
               AppConfig.Caption);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stops and cleans up the refresh timer to release resources and prevent references
        /// from keeping the UI Dispatcher alive. Should be called when the window or view model is closing.
        /// </summary>
        public void Cleanup()
        {
            // Thread-safe disposal pattern
            var oldCts = Interlocked.Exchange(ref _cts, null);
            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();           // Stop the timer
                _refreshTimer.Tick -= OnTick; // Unsubscribe event
                _refreshTimer = null;
            }

            // Dispose the command engine to clean up semaphores
            ServiceCommands?.Dispose();
        }

        /// <summary>
        /// Ensures the refresh timer exists and is running. Creates the timer if it does not exist,
        /// and starts it if it is not already enabled.
        /// </summary>
        public void CreateAndStartTimer()
        {
            if (_cts == null)
            {
                Interlocked.CompareExchange(ref _cts, new CancellationTokenSource(), null);
            }

            if (_refreshTimer == null)
            {
                _refreshTimer = CreateTimer();
            }

            // Start the timer only if it is not already running
            if (!_refreshTimer.IsEnabled)
            {
                _refreshTimer.Start();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Updates the <see cref="FooterText"/> with the current service count and the time elapsed 
        /// during the last operation.
        /// </summary>
        private void SetFooterText(Stopwatch stopwatch) =>
            FooterText = UI.Helpers.Helper.GetRowsInfo(_services.Count, stopwatch.Elapsed, Strings.Footer_ServiceRowText);

        /// <summary>
        /// Executes a bulk operation on all selected and installed services.
        /// </summary>
        private async Task ExecuteBulkOperationAsync(
            Func<Service, Task<bool>> operation,
            string confirmMessage,
            string logErrorMessage)
        {
            try
            {
                // 1. Identify selected and installed services
                var selectedServices = _services
                    .Where(s => s.IsInstalled && s.IsChecked)
                    .Select(s => s.Service)
                    .ToList();

                if (selectedServices.Count == 0)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_NoServicesSelected, AppConfig.Caption);
                    return;
                }

                // 2. Request user confirmation
                if (!await _messageBoxService.ShowConfirmAsync(confirmMessage, AppConfig.Caption))
                    return;

                SetIsBusy(true);

                // 3. Dispatch all operations concurrently: Scale based on hardware
                int maxDegreeOfParallelism = Math.Min(Environment.ProcessorCount * 2, MaxBulkOperationParallelism);

                using (var throttler = new SemaphoreSlim(maxDegreeOfParallelism))
                {
                    var operationTasks = selectedServices.Select(async service =>
                    {
                        await throttler.WaitAsync();
                        try
                        {
                            bool success = await operation(service);
                            return new { ServiceName = service.Name, Success = success };
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    });

                    var results = await Task.WhenAll(operationTasks);

                    // Filter out the failed ones
                    var failed = results.Where(r => !r.Success).Select(r => r.ServiceName).ToList();

                    // 4. Handle results and UI feedback
                    if (failed.Count == 0)
                    {
                        await _messageBoxService.ShowInfoAsync(Strings.Msg_OperationCompletedSuccessfully, AppConfig.Caption);
                    }
                    else
                    {
                        var message = failed.Count == selectedServices.Count
                            ? Strings.Msg_AllOperationsFailed
                            : string.Format(Strings.Msg_OperationCompletedWithErrorsDetails, string.Join(", ", failed));

                        await _messageBoxService.ShowWarningAsync(message, AppConfig.Caption);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{logErrorMessage}.", ex);
            }
            finally
            {
                SetIsBusy(false);
            }
        }

        /// <summary>
        /// Refresh all services description, status, startup type, user and installation state without blocking the UI thread.
        /// </summary>
        /// <param name="token">Explicit cancellation token to prevent mid-execution NullReferenceExceptions.</param>
        private async Task RefreshAllServicesAsync(CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                // 1. Take snapshot of services safely
                List<Service> snapshot;
                lock (_servicesLock)
                {
                    snapshot = _services.Select(r => r.Service).ToList();
                }

                // 2. Fetch OS Info in bulk
                var stopwatch = Stopwatch.StartNew();
                var allServicesList = await Task.Run(() => _serviceManager.GetAllServices(token), token);
                stopwatch.Stop();
                Debug.WriteLine($"GetAllServices finished in {stopwatch.ElapsedMilliseconds}ms");
                var allServicesDict = allServicesList.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

                // 3. Fetch all Repository DTOs in bulk
                var allDtosList = await _serviceRepository.GetAllAsync(decrypt: true, token);
                var allDtosDict = allDtosList.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

                // 4. Process in parallel using the semaphore
                var changedDtos = new System.Collections.Concurrent.ConcurrentBag<ServiceDto>();

                using (var semaphore = new SemaphoreSlim(Environment.ProcessorCount))
                {
                    var tasks = snapshot.Select(async service =>
                    {
                        await semaphore.WaitAsync(token);
                        try
                        {
                            allDtosDict.TryGetValue(service.Name, out var dto);

                            var updatedDto = await RefreshServiceInternal(service, allServicesDict, dto, token);

                            if (updatedDto != null)
                            {
                                changedDtos.Add(updatedDto);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList();

                    await Task.WhenAll(tasks);

                    // Refresh UI only if not cancelled
                    if (_cts != null && !_cts.IsCancellationRequested)
                    {
                        await _dispatcher.InvokeAsync(() =>
                        {
                            ServicesView.Refresh();
                        }, DispatcherPriority.Background);
                    }
                }

                // 5. Execute a single atomic database batch write for all drifted services
                if (changedDtos.Any())
                {
                    await _serviceRepository.UpsertBatchAsync(changedDtos, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to refresh all services.", ex);
            }
        }

        /// <summary>
        /// Synchronizes a service's live state with both the Windows Service Control Manager (SCM) and process performance counters.
        /// </summary>
        /// <param name="service">The UI-bound <see cref="Service"/> model to update.</param>
        /// <param name="allServices">A pre-fetched dictionary of OS-level service information to reduce syscall overhead.</param>
        /// <param name="serviceDto">The current database state for the service, used to detect metadata drift.</param>
        /// <param name="token">Cancellation token injected from the parent caller.</param>
        private async Task<ServiceDto> RefreshServiceInternal(Service service, Dictionary<string, ServiceInfo> allServices, ServiceDto serviceDto, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                // Heavy work: CPU/RAM (Kept on background thread)
                double? cpuUsage = null;
                long? ramUsage = null;
                if (service.Pid.HasValue)
                {
                    ProcessHelper.MaintainCache();

                    var processMetrics = ProcessHelper.GetProcessTreeMetrics(service.Pid.Value);
                    cpuUsage = processMetrics.CpuUsage;
                    ramUsage = processMetrics.RamUsage;
                }

                // Push all UI model updates to the Dispatcher to prevent ICommand cross-thread exceptions
                _dispatcher.Invoke(() =>
                {
                    if (serviceDto != null && service.Pid != serviceDto.Pid)
                    {
                        service.Pid = serviceDto.Pid;
                        service.IsPidEnabled = service.Pid != null;
                    }

                    service.CpuUsage = cpuUsage;
                    service.RamUsage = ramUsage;

                    if (service.StartupType == null && serviceDto != null)
                    {
                        service.StartupType = (ServiceStartType)serviceDto.StartupType;
                    }

                    if (allServices.TryGetValue(service.Name, out var info) && info != null)
                    {
                        service.IsInstalled = true;

                        if (service.Status != info.Status)
                            service.Status = info.Status;

                        if (service.StartupType != info.StartupType)
                            service.StartupType = info.StartupType;

                        var user = string.IsNullOrEmpty(info.LogOnAs) ? AppConfig.LocalSystem : info.LogOnAs;
                        if (service.LogOnAs != user)
                            service.LogOnAs = ServiceMapper.GetLogOnAsDisplayName(user);

                        if (service.Description != info.Description)
                            service.Description = info.Description;
                    }
                    else
                    {
                        service.IsInstalled = false;
                        service.Status = ServiceStatus.NotInstalled;
                    }
                });

                if (serviceDto != null)
                {
                    bool needsUpdate = !string.Equals(serviceDto.Description, service.Description, StringComparison.Ordinal) ||
                                       serviceDto.StartupType != (int)service.StartupType;

                    if (needsUpdate)
                    {
                        serviceDto.Description = service.Description;
                        serviceDto.StartupType = (int)service.StartupType;
                        return serviceDto;
                    }
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to refresh {service.Name}.", ex);
            }

            return null;
        }

        /// <summary>
        ///  Sets the busy state and updates the mouse cursor accordingly.
        /// </summary>
        private void SetIsBusy(bool busy)
        {
            _dispatcher.Invoke(() =>
            {
                IsBusy = busy;

                if (busy)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                }
                else
                {
                    Mouse.OverrideCursor = null;
                }
            });
        }

        /// <summary>
        /// Removes a service from the services collection, unsubscribes from its events,
        /// and refreshes the view. This method is thread-safe and dispatches to the UI thread.
        /// </summary>
        public void RemoveService(string serviceName)
        {
            Action action = () =>
            {
                var stopwatch = Stopwatch.StartNew();
                ServiceRowViewModel itemToRemove;

                lock (_servicesLock)
                {
                    itemToRemove = _services.FirstOrDefault(s => s.Service.Name == serviceName);
                    if (itemToRemove == null) return;

                    itemToRemove.PropertyChanged -= Service_PropertyChanged;
                    _services.Remove(itemToRemove);
                }

                itemToRemove.Dispose();
                ServicesView.Refresh();

                stopwatch.Stop();
                SetFooterText(stopwatch);
                UpdateSelectAllState();
            };

            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// Refreshes the services list by re-running the search.
        /// </summary>
        public async Task Refresh()
        {
            await SearchServicesAsync(null);
        }

        #endregion
    }
}