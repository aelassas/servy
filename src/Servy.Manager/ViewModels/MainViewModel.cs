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
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
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

        private readonly Dispatcher? _dispatcher;
        private readonly IServiceManager? _serviceManager;
        private readonly IServiceRepository? _serviceRepository;
        private readonly IMessageBoxService? _messageBoxService;
        private readonly IHelpService? _helpService;
        private readonly ICursorService? _cursorService;

        private CancellationTokenSource? _cts;

        private DispatcherTimer? _refreshTimer;
        private readonly BulkObservableCollection<ServiceRowViewModel> _services = new BulkObservableCollection<ServiceRowViewModel>();
        private bool _isBusy;
        private string _searchButtonText = Strings.Button_Search;
        private bool _isConfiguratorEnabled = false;
        private string? _searchText;
        private string? _footerText;
        private bool? _selectAll;
        private bool _isUpdatingSelectAll;
        private readonly object _servicesLock = new object();
        private int _isRefreshingFlag = 0; // 0 = false, 1 = true
        private readonly IAppConfiguration _appConfig;
        private readonly IProcessHelper _processHelper;

        private IDisposable? _busyCursor; // Manages the busy cursor explicitly for Sequential tasks

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// Used for data binding updates.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

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
        public string? FooterText
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
        public string? SearchText
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

        private IServiceCommands? _serviceCommands;

        /// <summary>
        /// The set of service commands available for each service row.
        /// </summary>
        public IServiceCommands? ServiceCommands
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
        public IAsyncCommand OpenDocumentationCommand { get; }

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
            IServiceManager? serviceManager,
            IServiceRepository? serviceRepository,
            IServiceCommands? serviceCommands,
            IHelpService? helpService,
            IMessageBoxService? messageBoxService,
            PerformanceViewModel? performanceVM,
            ConsoleViewModel? consoleVM,
            DependenciesViewModel? dependenciesVM,
            IAppConfiguration? appConfig,
            ICursorService? cursorService,
            IProcessHelper? processHelper,
            Dispatcher? dispatcher = null
            )
        {
            _serviceManager = serviceManager;
            _serviceRepository = serviceRepository;
            ServiceCommands = serviceCommands;
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _cursorService = cursorService ?? throw new ArgumentNullException(nameof(cursorService));
            _helpService = helpService;
            _messageBoxService = messageBoxService;
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _selectAll = false;

            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));

            // Assign child ViewModels injected via DI
            PerformanceVM = performanceVM ?? throw new ArgumentNullException(nameof(performanceVM));
            ConsoleVM = consoleVM ?? throw new ArgumentNullException(nameof(consoleVM));
            DependenciesVM = dependenciesVM ?? throw new ArgumentNullException(nameof(dependenciesVM));

            ServicesView = new ListCollectionView(_services);

            SearchCommand = new AsyncCommand(SearchServicesAsync);
            ConfigureCommand = new AsyncCommand(ConfigureServiceAsync);
            ImportXmlCommand = new AsyncCommand(ImportXmlConfigAsync);
            ImportJsonCommand = new AsyncCommand(ImportJsonConfigAsync);
            StartSelectedCommand = new AsyncCommand(StartSelectedAsync);
            StopSelectedCommand = new AsyncCommand(StopSelectedAsync);
            RestartSelectedCommand = new AsyncCommand(RestartSelectedAsync);
            OpenDocumentationCommand = new AsyncCommand(OpenDocumentation);
            CheckUpdatesCommand = new AsyncCommand(CheckUpdatesAsync);
            OpenAboutDialogCommand = new AsyncCommand(OpenAboutDialog);

            IsConfiguratorEnabled = _appConfig.IsDesktopAppAvailable;
            _appConfig.PropertyChanged += AppConfig_PropertyChanged;

            CreateAndStartTimer();
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
                        null,
                        null,
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
        /// PropertyChanged event handler to capture dynamically updated settings from the application.
        /// </summary>
        private void AppConfig_PropertyChanged(object? sender, PropertyChangedEventArgs? e)
        {
            if (e?.PropertyName == nameof(IAppConfiguration.IsDesktopAppAvailable))
            {
                IsConfiguratorEnabled = _appConfig.IsDesktopAppAvailable;
            }
        }

        /// <summary>
        /// Triggers when a property on a service row changes.
        /// </summary>
        private void Service_PropertyChanged(object? sender, PropertyChangedEventArgs? e)
        {
            if (e?.PropertyName == nameof(ServiceRowViewModel.IsChecked))
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
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_appConfig.RefreshIntervalInSeconds)
            };

            timer.Tick += OnTick;

            return timer;
        }

        /// <summary>
        /// Handles the <see cref="DispatcherTimer.Tick"/> event for refreshing services.
        /// Stops the timer before invoking <see cref="RefreshAllServicesAsync"/> to prevent overlapping ticks,
        /// and restarts the timer afterward if it still exists.
        /// </summary>
        private async void OnTick(object? sender, EventArgs? e)
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
        private async Task SearchServicesAsync(object? parameter)
        {
            if (_dispatcher == null)
            {
                Logger.Warn("Dispatcher is not available. Cannot perform search.");
                return;
            }

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
                _cursorService?.SetWaitCursor();
                SearchButtonText = Strings.Button_Searching;
                IsBusy = true;

                // Step 2: allow WPF to repaint the button and show progress bar
                await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                // Step 3: fetch data off UI thread
                var sw = Stopwatch.StartNew();
                var results = await Task.Run(() => ServiceCommands?.SearchServicesAsync(SearchText, true, token), token);
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
                    // Mutual exclusion: prevents the background refresh thread from
                    // accessing the collection while we are rebuilding it.
                    lock (_servicesLock)
                    {
                        // Explicitly dispose of existing ViewModels before clearing the collection
                        foreach (var oldVm in _services)
                        {
                            oldVm.Dispose();
                        }

                        _services.Clear();

                        // 1. Hook up property changed events first
                        foreach (var vm in vms)
                        {
                            vm.PropertyChanged += Service_PropertyChanged;
                        }

                        // 2. Add all items at once to trigger a single UI layout pass
                        _services.AddRange(vms);
                    }

                    // Properties updated outside the lock to avoid potential nested UI notifications
                    // while holding a synchronization primitive.
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
                _cursorService?.ResetCursor();
                SearchButtonText = Strings.Button_Search;
                IsBusy = false;
            }
        }

        /// <summary>
        /// Launches configuration for the given service.
        /// </summary>
        private async Task ConfigureServiceAsync(object? parameter)
        {
            if (ServiceCommands == null) throw new InvalidOperationException("ServiceCommands is not initialized.");
            await ServiceCommands.ConfigureServiceAsync(parameter as Service);
        }

        /// <summary>
        /// Imports XML configuration for services.
        /// </summary>
        private async Task ImportXmlConfigAsync(object? parameter)
        {
            if (ServiceCommands == null) throw new InvalidOperationException("ServiceCommands is not initialized.");
            await ServiceCommands.ImportXmlConfigAsync();
        }

        /// <summary>
        /// Imports JSON configuration for services.
        /// </summary>
        private async Task ImportJsonConfigAsync(object? parameter)
        {
            if (ServiceCommands == null) throw new InvalidOperationException("ServiceCommands is not initialized.");
            await ServiceCommands.ImportJsonConfigAsync();
        }

        /// <summary>
        /// Start all selected services.
        /// </summary>
        private Task StartSelectedAsync(object? parameter) =>
            ExecuteBulkOperationAsync(
                s =>
                {
                    if (ServiceCommands == null) throw new InvalidOperationException("ServiceCommands is not initialized.");
                    return ServiceCommands.StartServiceAsync(s, showMessageBox: false);
                },
                Strings.Confirm_StartSelectedServices,
                "Failed to start selected services");

        /// <summary>
        /// Stop all selected services.
        /// </summary>
        private Task StopSelectedAsync(object? parameter) =>
            ExecuteBulkOperationAsync(
                s =>
                {
                    if (ServiceCommands == null) throw new InvalidOperationException("ServiceCommands is not initialized.");
                    return ServiceCommands.StopServiceAsync(s, showMessageBox: false);
                },
                Strings.Confirm_StopSelectedServices,
                "Failed to stop selected services");

        /// <summary>
        /// Restart all selected services.
        /// </summary>
        private Task RestartSelectedAsync(object? parameter) =>
            ExecuteBulkOperationAsync(
                s =>
                {
                    if (ServiceCommands == null) throw new InvalidOperationException("ServiceCommands is not initialized.");
                    return ServiceCommands.RestartServiceAsync(s, showMessageBox: false);
                },
                Strings.Confirm_RestartSelectedServices,
                "Failed to restart selected services");

        #endregion

        #region Help/Updates/About Commands

        /// <summary>
        /// Opens the Servy documentation page in the default browser.
        /// </summary>
        private async Task OpenDocumentation(object? parameter)
        {
            if (_helpService == null)
            {
                Logger.Warn("Help service is not available.");
                return;
            }
            await _helpService.OpenDocumentation(AppConfig.Caption);
        }

        /// <summary>
        /// Checks for the latest Servy release on GitHub and prompts the user if an update is available.
        /// </summary>
        private async Task CheckUpdatesAsync(object? parameter)
        {
            if (_helpService == null)
            {
                Logger.Warn("Help service is not available.");
                return;
            }
            await _helpService.CheckUpdates(AppConfig.Caption);
        }

        /// <summary>
        /// Displays the "About Servy" dialog with version and copyright information.
        /// </summary>
        private async Task OpenAboutDialog(object? parameter)
        {
            if (_helpService == null)
            {
                Logger.Warn("Help service is not available.");
                return;
            }
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
            if (_appConfig != null)
            {
                _appConfig.PropertyChanged -= AppConfig_PropertyChanged;
            }

            // Thread-safe disposal pattern
            var oldCts = Interlocked.Exchange(ref _cts, null);
            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();            // Stop the timer
                _refreshTimer.Tick -= OnTick; // Unsubscribe event
                _refreshTimer = null;
            }

            // Dispose the command engine to clean up semaphores
            ServiceCommands?.Dispose();

            _busyCursor?.Dispose();
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
            Func<Service?, Task<bool>> operation,
            string confirmMessage,
            string logErrorMessage)
        {
            try
            {
                if (_messageBoxService == null)
                {
                    Logger.Warn("MessageBoxService is not available. Cannot confirm bulk operation.");
                    return;
                }

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

                await SetBusyStateAsync(true);

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
                            return new { ServiceName = service?.Name ?? string.Empty, Success = success };
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
                await SetBusyStateAsync(false);
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

                if (_serviceRepository == null)
                {
                    Logger.Warn("ServiceRepository is not available. Cannot refresh services.");
                    return;
                }

                if (_serviceManager == null)
                {
                    Logger.Warn("ServiceManager is not available. Cannot refresh services.");
                    return;
                }

                // 1. Take snapshot of services safely
                List<Service?> snapshot;
                lock (_servicesLock)
                {
                    snapshot = _services.Select(r => r.Service).ToList();
                }

                // 2. Fetch OS Info in bulk (Off UI thread)
                var stopwatch = Stopwatch.StartNew();
                var allServicesList = await Task.Run(() => _serviceManager.GetAllServices(token), token);
                stopwatch.Stop();
                Debug.WriteLine($"GetAllServices finished in {stopwatch.ElapsedMilliseconds}ms");
                var allServicesDict = allServicesList.ToDictionary(s => s.Name!, StringComparer.OrdinalIgnoreCase);

                // 3. Fetch all Repository DTOs in bulk
                var allDtosList = await _serviceRepository.GetAllAsync(decrypt: true, token);
                var allDtosDict = allDtosList.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

                // 4. Process data collection in parallel
                // We collect the updates in thread-safe bags instead of applying them immediately
                var changedDtos = new System.Collections.Concurrent.ConcurrentBag<ServiceDto>();
                var uiUpdates = new System.Collections.Concurrent.ConcurrentBag<ServiceUpdateInfo>();

                using (var semaphore = new SemaphoreSlim(Environment.ProcessorCount))
                {
                    var tasks = snapshot.Select(async service =>
                    {
                        await semaphore.WaitAsync(token);
                        try
                        {
                            if (service == null || string.IsNullOrWhiteSpace(service.Name)) return;
                            allDtosDict.TryGetValue(service.Name, out var dto);

                            // Collect updates without touching the UI model yet
                            var result = await GetServiceUpdateInfoAsync(service, allServicesDict, dto, token);

                            if (result.UpdateInfo != null)
                                uiUpdates.Add(result.UpdateInfo);

                            if (result.UpdatedDto != null)
                                changedDtos.Add(result.UpdatedDto);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList();

                    await Task.WhenAll(tasks);
                }

                // 5. Batch-Apply all UI updates to the UI thread in one go
                // This prevents the "Collection modified" crash by ensuring 
                // property changes happen in a controlled, sequential batch.
                if (!uiUpdates.IsEmpty)
                {
                    await _dispatcher!.InvokeAsync(() =>
                    {
                        foreach (var info in uiUpdates)
                        {
                            ApplyServiceUpdate(info);
                        }

                        // Run this once after the batch finishes to stabilize the UI state
                        UpdateSelectAllState();
                    }, DispatcherPriority.Background);
                }

                // 6. Execute a single atomic database batch write for all drifted services
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
        /// Pure logic method to calculate what needs to change without touching UI models.
        /// </summary>
        private async Task<(ServiceUpdateInfo? UpdateInfo, ServiceDto? UpdatedDto)> GetServiceUpdateInfoAsync(
            Service service,
            Dictionary<string, ServiceInfo>? allServices,
            ServiceDto? serviceDto,
            CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var update = new ServiceUpdateInfo(service);

                // 1. Evaluate OS Status First
                if (allServices != null && !string.IsNullOrWhiteSpace(service.Name) && allServices.TryGetValue(service.Name, out var info) && info != null)
                {
                    update.IsInstalled = true;
                    update.Status = info.Status;
                    update.StartupType = info.StartupType;
                    update.LogOnAs = ServiceMapper.GetLogOnAsDisplayName(info.LogOnAs ?? AppConfig.LocalSystem);
                    update.Description = info.Description;
                }
                else
                {
                    update.IsInstalled = false;
                    update.Status = ServiceStatus.NotInstalled;
                }

                // 2. Determine if the wrapper is actually dead according to Windows
                bool isProcessDead = update.Status == ServiceStatus.Stopped || update.Status == ServiceStatus.NotInstalled;

                // 3. FIX: Prioritize DB PID, but force to null if the process is dead (Ignore Ghost PIDs)
                int? targetPid = isProcessDead ? null : (serviceDto?.Pid ?? service.Pid);

                // Gather metrics using the safe targetPid
                double? cpu = null;
                long? ram = null;
                if (targetPid.HasValue && targetPid.Value > 0)
                {
                    _processHelper.MaintainCache();
                    var metrics = _processHelper.GetProcessTreeMetrics(targetPid.Value);
                    cpu = metrics.CpuUsage;
                    ram = metrics.RamUsage;
                }

                update.CpuUsage = cpu;
                update.RamUsage = ram;

                if (service.StartupType == null && serviceDto != null)
                {
                    update.StartupType = serviceDto.StartupType.HasValue ? (ServiceStartType?)serviceDto.StartupType.Value : null;
                }

                ServiceDto? resultDto = null;
                if (serviceDto != null)
                {
                    // 4. Sync PID to UI. Because we use targetPid, this correctly pushes 'null' 
                    // to the UI if the service crashed, even if the DB is stuck on 7020.
                    if (service.Pid != targetPid)
                    {
                        update.RequiresPidUpdate = true;
                        update.NewPid = targetPid;
                    }

                    // Check for DB metadata drift
                    var currentDescription = update.Description ?? service.Description;
                    var currentStartupType = update.StartupType ?? service.StartupType;

                    var dbDesc = serviceDto.Description ?? string.Empty;
                    var currDesc = currentDescription ?? string.Empty;

                    bool descDrifted = !string.Equals(dbDesc, currDesc, StringComparison.Ordinal);
                    bool startupDrifted = currentStartupType.HasValue && serviceDto.StartupType != (int)currentStartupType.Value;

                    if (descDrifted || startupDrifted)
                    {
                        serviceDto.Description = currentDescription;
                        if (currentStartupType.HasValue)
                        {
                            serviceDto.StartupType = (int)currentStartupType.Value;
                        }

                        // Note: We are deliberately NOT modifying serviceDto.Pid here, 
                        // leaving database writes exclusively to the wrapper.
                        resultDto = serviceDto;
                    }
                }
                else
                {
                    update.RequiresPidUpdate = true;
                    update.NewPid = null;
                }

                return (update, resultDto);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to prepare refresh for {service.Name}.", ex);
                return (null, null);
            }
        }

        /// <summary>
        /// Safely applies a gathered update to the UI model. Must be called from the UI thread.
        /// </summary>
        private void ApplyServiceUpdate(ServiceUpdateInfo info)
        {
            var service = info.Target;

            if (info.RequiresPidUpdate)
            {
                service.Pid = info.NewPid;
                service.IsPidEnabled = service.Pid != null;
            }

            service.CpuUsage = info.CpuUsage;
            service.RamUsage = info.RamUsage;
            service.IsInstalled = info.IsInstalled;

            if (info.Status.HasValue && service.Status != info.Status.Value)
                service.Status = info.Status.Value;

            if (info.StartupType.HasValue && service.StartupType != info.StartupType.Value)
                service.StartupType = info.StartupType.Value;

            if (info.LogOnAs != null && service.LogOnAs != info.LogOnAs)
                service.LogOnAs = info.LogOnAs;

            if (info.Description != null && service.Description != info.Description)
                service.Description = info.Description;
        }

        /// <summary>
        /// Simple nested class to hold the results of background work securely
        /// </summary>
        private sealed class ServiceUpdateInfo
        {
            public Service Target { get; }

            public bool RequiresPidUpdate { get; set; }
            public int? NewPid { get; set; }
            public double? CpuUsage { get; set; }
            public long? RamUsage { get; set; }
            public bool IsInstalled { get; set; }
            public ServiceStatus? Status { get; set; }
            public ServiceStartType? StartupType { get; set; }
            public string? LogOnAs { get; set; }
            public string? Description { get; set; }

            public ServiceUpdateInfo(Service target)
            {
                Target = target;
            }
        }

        /// <summary>
        /// Sets the busy state and updates the mouse cursor accordingly without blocking the calling thread.
        /// </summary>
        private async Task SetBusyStateAsync(bool busy)
        {
            await _dispatcher!.InvokeAsync(() =>
            {
                IsBusy = busy;
                if (busy)
                {
                    if (_busyCursor == null)
                    {
                        _busyCursor = _cursorService?.SetWaitCursor();
                    }
                }
                else
                {
                    _busyCursor?.Dispose();
                    _busyCursor = null;
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
                ServiceRowViewModel? itemToRemove;

                lock (_servicesLock)
                {
                    itemToRemove = _services.FirstOrDefault(s => s.Service?.Name == serviceName);
                    if (itemToRemove == null) return;

                    itemToRemove.PropertyChanged -= Service_PropertyChanged!;
                    _services.Remove(itemToRemove);
                }

                itemToRemove.Dispose();
                ServicesView.Refresh();

                stopwatch.Stop();
                SetFooterText(stopwatch);
                UpdateSelectAllState();
            };

            if (_dispatcher!.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.InvokeAsync(action, DispatcherPriority.Background);
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