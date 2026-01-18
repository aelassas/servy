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

        #region Private Fields

        private readonly IServiceManager _serviceManager;
        private readonly IServiceRepository _serviceRepository;
        private readonly IMessageBoxService _messageBoxService;
        private readonly ILogger _logger;
        private readonly IHelpService _helpService;
        private CancellationTokenSource _cancellationTokenSource;
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
            get => _serviceCommands; set
            {
                _serviceCommands = value;
                if (PerformanceVM != null)
                {
                    PerformanceVM.ServiceCommands = value;
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
        /// <value>
        /// <c>true</c> if all services are checked;  
        /// <c>false</c> if no services are checked;  
        /// <c>null</c> if some but not all services are checked.
        /// </value>
        /// <remarks>
        /// When the property is set by the user (i.e. not during an internal update),  
        /// all services in <c>_services</c> are updated to match the new value.  
        /// An indeterminate (<c>null</c>) state set by the user is interpreted as <c>true</c>.  
        /// After updating, <see cref="UpdateSelectAllState"/> is called to keep the header
        /// checkbox state in sync.  
        /// The <c>_isUpdatingSelectAll</c> flag prevents recursive updates when the property
        /// is modified programmatically.
        /// </remarks>
        public bool? SelectAll
        {
            get => _selectAll;
            set
            {
                if (_selectAll == value) return;

                //_selectAll = value;
                _selectAll = _selectAll == null ? false : value;
                OnPropertyChanged();

                // Only handle user clicks
                if (!_isUpdatingSelectAll)
                {
                    _isUpdatingSelectAll = true;

                    // Treat null (indeterminate) click as true
                    bool newValue = _selectAll ?? true;

                    //foreach (var s in _services)
                    //{
                    //    s.IsChecked = newValue;
                    //    s.IsSelected = false;
                    //}

                    if (_services.Count(s => s.IsSelected) <= 1)
                    {
                        foreach (var s in _services)
                        {
                            s.IsChecked = newValue;
                            s.IsSelected = false;
                        }
                    }
                    else
                    {
                        foreach (var s in _services.Where(s => s.IsSelected))
                        {
                            s.IsChecked = !s.IsChecked;
                            s.IsSelected = false;
                        }
                    }

                    _isUpdatingSelectAll = false;

                    // After updating rows, update header to reflect current state
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
        public MainViewModel(ILogger logger,
            IServiceManager serviceManager,
            IServiceRepository serviceRepository,
            IServiceCommands serviceCommands,
            IHelpService helpService,
            IMessageBoxService messageBoxService
            )
        {
            _logger = logger;
            _serviceManager = serviceManager;
            _serviceRepository = serviceRepository;
            ServiceCommands = serviceCommands;
            _helpService = helpService;
            _messageBoxService = messageBoxService;
            _selectAll = false;

            // Create PerformanceVM only once
            PerformanceVM = new PerformanceViewModel(serviceRepository, serviceCommands, _logger);

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
                null,
                null
                )
        { }

        #endregion

        #region Private Methods/Events

        /// <summary>
        /// Triggers when a property on a service row changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// Sets it to <c>true</c> if all services are checked, <c>false</c> if none are checked,
        /// or <c>null</c> if there is a mix of checked and unchecked services.
        /// </summary>
        /// <remarks>
        /// The <c>_isUpdatingSelectAll</c> flag is used to prevent recursive updates
        /// when the <see cref="SelectAll"/> property changes.
        /// </remarks>
        private void UpdateSelectAllState()
        {
            if (_isUpdatingSelectAll) return;

            _isUpdatingSelectAll = true;

            var all = _services.All(s => s.IsChecked);
            var none = _services.All(s => !s.IsChecked);

            if (all)
                SelectAll = true;
            else if (none)
                SelectAll = false;
            else
                SelectAll = null;

            _isUpdatingSelectAll = false;
        }

        /// <summary>
        /// Creates a new <see cref="DispatcherTimer"/> configured with the application's refresh interval.
        /// </summary>
        /// <returns>A <see cref="DispatcherTimer"/> instance ready to be started.</returns>
        private DispatcherTimer CreateTimer()
        {
            var app = (App)Application.Current;
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(app.RefreshIntervalInSeconds)
            };

            // Subscribe to the tick event
            timer.Tick += RefreshTimer_Tick;

            return timer;
        }

        /// <summary>
        /// Handles the <see cref="DispatcherTimer.Tick"/> event for refreshing services.
        /// Stops the timer before invoking <see cref="RefreshAllServicesAsync"/> to prevent overlapping ticks,
        /// and restarts the timer afterward if it still exists.
        /// </summary>
        /// <param name="sender">The <see cref="DispatcherTimer"/> that raised the event.</param>
        /// <param name="e">Event data associated with the tick.</param>
        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // Atomically check if 0, and if so, set to 1.
            // If it was already 1, return immediately.
            if (Interlocked.CompareExchange(ref _isRefreshingFlag, 1, 0) == 1)
            {
                return;
            }

            try
            {
                // Check cancellation before starting heavy work
                if (_cancellationTokenSource?.IsCancellationRequested == true)
                    return;

                // This runs the heavy fetching off the UI thread (via the Task.Run calls inside)
                await RefreshAllServicesAsync();
            }
            catch (OperationCanceledException)
            {
                // Clean exit
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed background refresh: {ex.Message}");
            }
            finally
            {
                // Atomically set back to 0
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
            // Step 0: cancel any previous search
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                var stopwatch = Stopwatch.StartNew();

                FooterText = string.Empty; // Clear footer text before search

                // Step 1: show "Searching..." immediately
                Mouse.OverrideCursor = Cursors.Wait;
                SearchButtonText = Strings.Button_Searching;
                IsBusy = true;

                // Step 2: allow WPF to repaint the button and show progress bar
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                // Step 3: fetch data off UI thread
                var sw = Stopwatch.StartNew();
                var results = await Task.Run(() => ServiceCommands.SearchServicesAsync(SearchText, true, token), token);
                sw.Stop();
                Debug.WriteLine($"Created {results.Count} SearchServicesAsync in {sw.ElapsedMilliseconds} ms");

                // Step 4: fetch data & build VMs off UI thread
                sw = Stopwatch.StartNew();
                var vms = await Task.Run(() =>
                    results.Select(s => new ServiceRowViewModel(s, ServiceCommands, _logger)).ToList()
                , token);
                sw.Stop();
                Debug.WriteLine($"Created {vms.Count} ServiceRowViewModels in {sw.ElapsedMilliseconds} ms");

                // Step 5: update collection on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _services.Clear();

                    foreach (var vm in vms)
                    {
                        vm.PropertyChanged += Service_PropertyChanged;
                        _services.Add(vm);
                    }

                    SelectAll = false;

                    // Notify that bulk action availability changed
                    OnPropertyChanged(nameof(HasSelectedServices));

                    //ServicesView.Refresh();
                }, DispatcherPriority.Background);

                stopwatch.Stop();
                FooterText = UI.Helpers.Helper.GetRowsInfo(_services.Count, stopwatch.Elapsed, Strings.Footer_ServiceRowText);

                // Setp 5: refresh all service statuses and details in the background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RefreshAllServicesAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // expected when cancelled
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"RefreshAllServicesAsync failed: {ex}");
                    }
                }, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // expected when cancelled
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to search services from main tab: {ex}");
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
        /// <param name="parameter"></param>
        /// <returns></returns>
        private async Task StartSelectedAsync(object parameter)
        {
            try
            {
                var selectedServices = _services
                    .Where(s => s.IsInstalled && s.IsChecked)
                    .Select(s => s.Service)
                    .ToList();

                if (!selectedServices.Any())
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_NoServicesSelected, AppConfig.Caption);
                    return;
                }

                var res = await _messageBoxService.ShowConfirmAsync(Strings.Confirm_StartSelectedServices, AppConfig.Caption);
                if (!res) return;

                SetIsBusy(true);

                var failed = new List<string>();

                foreach (var service in selectedServices)
                {
                    var ok = await ServiceCommands.StartServiceAsync(service, showMessageBox: false);
                    if (!ok) failed.Add(service.Name);
                }

                if (!failed.Any())
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
            catch (Exception ex)
            {
                _logger.Warning($"Failed to start selected services: {ex}");
            }
            finally
            {
                SetIsBusy(false);
            }
        }


        /// <summary>
        /// Stop all selected services.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private async Task StopSelectedAsync(object parameter)
        {
            try
            {
                var selectedServices = _services.Where(s => s.IsInstalled && s.IsChecked).Select(s => s.Service).ToList();
                if (selectedServices.Count == 0)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_NoServicesSelected, AppConfig.Caption);
                    return;
                }

                var res = await _messageBoxService.ShowConfirmAsync(Strings.Confirm_StopSelectedServices, AppConfig.Caption);

                if (!res) return;

                SetIsBusy(true);

                var failed = new List<string>();

                foreach (var service in selectedServices)
                {
                    var ok = await ServiceCommands.StopServiceAsync(service, showMessageBox: false);
                    if (!ok) failed.Add(service.Name);
                }

                if (!failed.Any())
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
            catch (Exception ex)
            {
                _logger.Warning($"Failed to start selected services: {ex}");
            }
            finally
            {
                SetIsBusy(false);
            }
        }

        /// <summary>
        /// Restart all selected services.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private async Task RestartSelectedAsync(object parameter)
        {
            try
            {
                var selectedServices = _services.Where(s => s.IsInstalled && s.IsChecked).Select(s => s.Service).ToList();
                if (selectedServices.Count == 0)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_NoServicesSelected, AppConfig.Caption);
                    return;
                }

                var res = await _messageBoxService.ShowConfirmAsync(Strings.Confirm_RestartSelectedServices, AppConfig.Caption);

                if (!res) return;

                SetIsBusy(true);

                var failed = new List<string>();

                foreach (var service in selectedServices)
                {
                    var ok = await ServiceCommands.RestartServiceAsync(service, showMessageBox: false);
                    if (!ok) failed.Add(service.Name);
                }

                if (!failed.Any())
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
            catch (Exception ex)
            {
                _logger.Warning($"Failed to start selected services: {ex}");
            }
            finally
            {
                SetIsBusy(false);
            }
        }

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
        /// If a newer version exists, opens the latest release page in the default browser; otherwise shows an informational message.
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
            _cancellationTokenSource?.Cancel(); // cancel any in-progress async work

            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();           // Stop the timer
                _refreshTimer.Tick -= RefreshTimer_Tick; // Unsubscribe event
                _refreshTimer = null;
            }
        }

        /// <summary>
        /// Ensures the refresh timer exists and is running. Creates the timer if it does not exist,
        /// and starts it if it is not already enabled.
        /// </summary>
        public void CreateAndStartTimer()
        {
            if (_refreshTimer == null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
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
        /// Refresh all services description, status, startup type, user and installation state without blocking the UI thread.
        /// </summary>
        private async Task RefreshAllServicesAsync()
        {
            try
            {
                var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
                token.ThrowIfCancellationRequested();

                // 1. Take snapshot of services safely
                List<Service> snapshot;
                lock (_servicesLock)
                {
                    snapshot = _services.Select(r => r.Service).ToList();
                }

                // 2. Fetch OS Info in bulk
                var allServicesList = await Task.Run(() => _serviceManager.GetAllServices(token), token);
                var allServicesDict = allServicesList.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

                // 3. Fetch all Repository DTOs in bulk
                var allDtosList = await _serviceRepository.GetAllAsync(token);
                var allDtosDict = allDtosList.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

                // 4. Process in parallel using the semaphore
                using (var semaphore = new SemaphoreSlim(Environment.ProcessorCount))
                {
                    var tasks = snapshot.Select(async service =>
                    {
                        await semaphore.WaitAsync(token);
                        try
                        {
                            // Pass the pre-fetched DTO from our dictionary
                            allDtosDict.TryGetValue(service.Name, out var dto);
                            await RefreshServiceInternal(service, allServicesDict, dto);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList(); // Force task creation while semaphore is in scope

                    await Task.WhenAll(tasks);
                }

                // Refresh UI only if not cancelled
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ServicesView.Refresh();
                    }, DispatcherPriority.Background);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to refresh all services: {ex}");
            }
        }

        /// <summary>
        /// Refresh service description, status, startup type, user and installation state. Heavy work runs off the UI thread.
        /// </summary>
        /// <param name="service">The service to refresh.</param>
        /// <param name="allServices">Dictionary of all installed services keyed by name.</param>
        /// <param name="serviceDto">The service DTO from the repository, if any.</param>
        private async Task RefreshServiceInternal(Service service, Dictionary<string, ServiceInfo> allServices, ServiceDto serviceDto)
        {
            try
            {
                var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
                token.ThrowIfCancellationRequested();

                // Use the pre-fetched DTO to update PID
                if (serviceDto != null && service.Pid != serviceDto.Pid)
                {
                    service.Pid = serviceDto.Pid;
                    service.IsPidEnabled = service.Pid != null;
                }

                // Heavy work: CPU/RAM
                double? cpuUsage = null;
                long? ramUsage = null;
                if (service.Pid.HasValue)
                {
                    // Parallelizing CPU and RAM tasks that only 1–5ms actually slows down the app
                    // because the time it takes to manage two threads is greater than the time
                    // saved by running them at once.
                    // Parallelism is only a "win" if the tasks are "heavy."
                    var pid = service.Pid.Value;
                    cpuUsage = ProcessHelper.GetCpuUsage(pid);
                    ramUsage = ProcessHelper.GetRamUsage(pid);
                }
                service.CpuUsage = cpuUsage;
                service.RamUsage = ramUsage;

                // Load startup type from repository if null
                if (service.StartupType == null && serviceDto != null)
                {
                    service.StartupType = (ServiceStartType)serviceDto.StartupType;
                }

                // Update from OS Info (Dictionary)
                if (allServices.TryGetValue(service.Name, out var info) && info != null)
                {
                    service.IsInstalled = true;

                    if (service.Status != info.Status)
                        service.Status = info.Status;

                    if (service.StartupType != info.StartupType)
                        service.StartupType = info.StartupType;

                    var user = string.IsNullOrEmpty(info.UserSession) ? AppConfig.LocalSystem : info.UserSession;
                    if (service.UserSession != user)
                        service.UserSession = ServiceMapper.GetUserSessionDisplayName(user);

                    if (service.Description != info.Description)
                        service.Description = info.Description;
                }
                else
                {
                    service.IsInstalled = false;
                    service.Status = ServiceStatus.NotInstalled;
                }

                // Repository update ONLY if something actually changed in the Metadata
                if (serviceDto != null)
                {
                    bool needsUpdate = !string.Equals(serviceDto.Description, service.Description, StringComparison.Ordinal) ||
                                       serviceDto.StartupType != (int)service.StartupType;

                    if (needsUpdate)
                    {
                        serviceDto.Description = service.Description;
                        serviceDto.StartupType = (int)service.StartupType;
                        await _serviceRepository.UpsertAsync(serviceDto, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to refresh {service.Name}: {ex}");
            }
        }

        /// <summary>
        ///  Sets the busy state and updates the mouse cursor accordingly.
        /// </summary>
        /// <param name="busy"></param>
        private void SetIsBusy(bool busy)
        {
            Application.Current.Dispatcher.Invoke(() =>
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
        /// Removes a service from the services collection and refreshes the view.
        /// </summary>
        /// <param name="serviceName">Name of the service to remove.</param>
        public void RemoveService(string serviceName)
        {
            var itemToRemove = _services.FirstOrDefault(s => s.Service.Name == serviceName);
            if (itemToRemove != null)
                _services.Remove(itemToRemove);

            ServicesView.Refresh();
        }

        /// <summary>
        /// Refreshes the services list by re-running the search.
        /// </summary>
        public async Task Resfresh()
        {
            await SearchServicesAsync(null);
        }

        #endregion

    }
}
