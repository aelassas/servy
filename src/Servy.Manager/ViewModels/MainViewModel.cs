using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        private readonly ILogger _logger;
        private readonly IHelpService _helpService;
        private CancellationTokenSource _cts;
        private DispatcherTimer _refreshTimer;
        private bool _isBusy;
        private List<ServiceRowViewModel> _services = new List<ServiceRowViewModel>();
        private string _searchButtonText = Strings.Button_Search;
        private bool _isConfiguratorEnabled = false;
        private string _searchText;

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

        /// <summary>
        /// The set of service commands available for each service row.
        /// </summary>
        public IServiceCommands ServiceCommands { get; set; }

        /// <summary>
        /// Collection of services displayed in the DataGrid.
        /// </summary>
        public ICollectionView ServicesView { get; private set; }

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
            IHelpService helpService
            )
        {
            _logger = logger;
            _serviceManager = serviceManager;
            _serviceRepository = serviceRepository;
            ServiceCommands = serviceCommands;
            _helpService = helpService;

            ServicesView = new ListCollectionView(_services);

            SearchCommand = new AsyncCommand(SearchServicesAsync);
            ConfigureCommand = new AsyncCommand(ConfigureServiceAsync);
            ImportXmlCommand = new AsyncCommand(ImportXmlConfigAsync);
            ImportJsonCommand = new AsyncCommand(ImportJsonConfigAsync);
            OpenDocumentationCommand = new RelayCommand<object>(_ => OpenDocumentation());
            CheckUpdatesCommand = new AsyncCommand(CheckUpdatesAsync);
            OpenAboutDialogCommand = new AsyncCommand(OpenAboutDialog);

            var app = (App)Application.Current;
            IsConfiguratorEnabled = app.IsConfigurationAppAvailable;

            _cts = new CancellationTokenSource();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(app.RefreshIntervalInSeconds)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();

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
        /// Handles the <see cref="DispatcherTimer.Tick"/> event for refreshing services.
        /// Stops the timer before invoking <see cref="RefreshAllServicesAsync"/> to prevent overlapping ticks,
        /// and restarts the timer afterward if it still exists.
        /// </summary>
        /// <param name="sender">The <see cref="DispatcherTimer"/> that raised the event.</param>
        /// <param name="e">Event data associated with the tick.</param>
        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            _refreshTimer.Stop(); // prevent overlapping ticks
            try
            {
                if (_cts != null && _cts.IsCancellationRequested)
                    return; // Exit if cancellation is requested

                await RefreshAllServicesAsync();
            }
            finally
            {
                if (_refreshTimer != null) // make sure timer still exists
                    _refreshTimer.Start();
            }
        }

        #endregion

        #region Service Commands

        /// <summary>
        /// Performs search of services asynchronously.
        /// </summary>
        /// <summary>
        /// Performs search of services asynchronously.
        /// </summary>
        private async Task SearchServicesAsync(object parameter)
        {
            try
            {
                // Step 0: cancel any previous search
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                // Step 1: show "Searching..." immediately
                Mouse.OverrideCursor = Cursors.Wait;
                SearchButtonText = Strings.Button_Searching;
                IsBusy = true;

                // Step 2: allow WPF to repaint the button and show progress bar
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                // Step 3: fetch data off UI thread
                var sw = Stopwatch.StartNew();
                var results = await Task.Run(() => ServiceCommands.SearchServicesAsync(SearchText, _cts.Token), _cts.Token);
                sw.Stop();
                Debug.WriteLine($"Created {results.Count()} SearchServicesAsync in {sw.ElapsedMilliseconds} ms");

                // Step 4: fetch data & build VMs off UI thread
                sw = Stopwatch.StartNew();
                var vms = await Task.Run(() =>
                    results.Select(s => new ServiceRowViewModel(s, ServiceCommands, _logger)).ToList()
                , _cts.Token);
                sw.Stop();
                Debug.WriteLine($"Created {vms.Count} ServiceRowViewModels in {sw.ElapsedMilliseconds} ms");

                // Step 5: update collection on UI thread
                _services.Clear();
                _services.AddRange(vms);
                ServicesView.Refresh();

                // Setp 5: refresh all service statuses and details in the background
                _ = Task.Run(RefreshAllServicesAsync, _cts.Token);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to search services: {ex}");
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
            await _helpService.OpenAboutDialog(Strings.Text_About, AppConfig.Caption);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stops and cleans up the refresh timer to release resources and prevent references
        /// from keeping the UI Dispatcher alive. Should be called when the window or view model is closing.
        /// </summary>
        public void Cleanup()
        {
            if (_cts != null)
            {
                _cts.Cancel(); // cancel any in-progress async work
            }

            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();           // Stop the timer
                _refreshTimer.Tick -= RefreshTimer_Tick; // Unsubscribe event
                _refreshTimer = null;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Refresh all services description, status, startup type, user and installation state.
        /// </summary>
        private async Task RefreshAllServicesAsync()
        {
            var snapshot = _services.Select(r => r.Service).ToList();

            var tasks = snapshot.Select(s => RefreshServiceInternal(s)).ToArray();
            await Task.WhenAll(tasks);

            if (_cts != null && !_cts.IsCancellationRequested)
                ServicesView.Refresh(); // only touch UI if not cancelled
        }


        /// <summary>
        /// Refresh service description, status, startup type, user and installation state.
        /// </summary>
        private async Task RefreshServiceInternal(Service service)
        {
            try
            {
                _cts?.Token.ThrowIfCancellationRequested();

                var isInstalled = _serviceManager.IsServiceInstalled(service.Name);
                service.IsInstalled = isInstalled;

                if (service.StartupType == null)
                {
                    var dto = await _serviceRepository.GetByNameAsync(service.Name, _cts.Token);
                    service.StartupType = (ServiceStartType)dto.StartupType;
                }

                if (isInstalled)
                {
                    // snapshot current values to avoid overwriting with defaults on cancel
                    var prevDescription = service.Description;
                    var prevStatus = service.Status;
                    var prevStartupType = service.StartupType;
                    var prevUser = service.UserSession;

                    // Run multiple queries in parallel
                    var descriptionTask = Task.Run(() =>
                    {
                        try
                        {
                            var desc = _serviceManager.GetServiceDescription(service.Name, _cts.Token);

                            // Distinguish between "valid empty" and "failure"
                            return desc ?? prevDescription; // only fallback on null, not ""
                        }
                        catch (OperationCanceledException)
                        {
                            return prevDescription; // fallback when canceled
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning($"Failed to get description for {service.Name}: {ex.Message}");
                            return prevDescription; // fallback on real error
                        }
                    }, _cts.Token);

                    var statusTask = Task.Run(() =>
                    {
                        try
                        {
                            return _serviceManager.GetServiceStatus(service.Name, _cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return prevStatus;
                        }
                    }, _cts.Token);

                    var startupTypeTask = Task.Run(() =>
                    {
                        try
                        {
                            return _serviceManager.GetServiceStartupType(service.Name, _cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return prevStartupType ?? ServiceStartType.Automatic;
                        }
                    }, _cts.Token);

                    var userTask = Task.Run(() =>
                    {
                        try
                        {
                            return _serviceManager.GetServiceUser(service.Name, _cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return prevUser ?? AppConfig.LocalSystem;
                        }
                    }, _cts.Token);

                    await Task.WhenAll(descriptionTask, statusTask, startupTypeTask, userTask);

                    service.Description = descriptionTask.Result;
                    service.Status = statusTask.Result;
                    service.StartupType = (ServiceStartType)startupTypeTask.Result;

                    var user = userTask.Result;
                    service.UserSession = string.IsNullOrEmpty(user) ||
                                          user.Trim().Equals("LocalSystem", StringComparison.OrdinalIgnoreCase)
                        ? AppConfig.LocalSystem
                        : user;
                }

                // Repository update
                var dtoUpdate = await _serviceRepository.GetByNameAsync(service.Name, _cts.Token);
                if (dtoUpdate != null &&
                    (!dtoUpdate.Description.Equals(service.Description) ||
                     dtoUpdate.StartupType != (int)service.StartupType))
                {
                    dtoUpdate.Description = service.Description;
                    dtoUpdate.StartupType = (int)service.StartupType;
                    await _serviceRepository.UpsertAsync(dtoUpdate, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // expected when cancelling
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to refresh {service.Name}: {ex}");
            }
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
        public async void Resfresh()
        {
            await SearchServicesAsync(null);
        }

        #endregion

    }
}
