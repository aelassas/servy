using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.Utils;
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Constants;
using Servy.UI.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the Console view, responsible for real-time log tailing, 
    /// service monitoring, and log filtering.
    /// </summary>
    public class ConsoleViewModel : ViewModelBase
    {
        #region Fields

        private readonly IServiceRepository _serviceRepository;
        private DispatcherTimer _timer;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationTokenSource _searchCts;
        private bool _hadSelectedService;
        private int _isMonitoringFlag = 0; // 0 = Stopped, 1 = Monitoring
        private int _isTickRunningFlag = 0; // 0 = Idle, 1 = Processing
        private CancellationTokenSource _cts;
        private string _consoleSearchText;
        private readonly int _maxLines;
        private string _stdoutPath;
        private string _stderrPath;
        private int _currentSessionId = 0; // Track the "active" switch request
        private volatile bool _isSelectionActive;
        private int _tickErrorCount = 0;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the view should scroll to the bottom. 
        /// Boolean parameter indicates if the scroll is a "forced" reset (e.g., after clearing search).
        /// </summary>
        public event Action<bool> RequestScroll;

        /// <summary>
        /// Event to signal the view to capture its state.
        /// </summary>
        public event Action<bool> RequestStatePreservation;

        #endregion

        #region Properties - Log Data

        /// <summary>
        /// Gets the full collection of log lines received from the service.
        /// </summary>
        public BulkObservableCollection<LogLine> RawLines { get; } = new BulkObservableCollection<LogLine>();

        /// <summary>
        /// Gets the filtered view of log lines based on the <see cref="ConsoleSearchText"/>.
        /// </summary>
        public ICollectionView VisibleLines { get; }

        /// <summary>
        /// Gets or sets the text used to filter the console logs.
        /// Triggering a refresh on the <see cref="VisibleLines"/> and requesting a scroll if cleared.
        /// </summary>
        public string ConsoleSearchText
        {
            get => _consoleSearchText;
            set
            {
                _consoleSearchText = value;
                OnPropertyChanged(nameof(ConsoleSearchText));

                // Trigger debounced refresh instead of immediate refresh
                _ = ApplyFilterWithDebounceAsync();

                // If the search is cleared, trigger a scroll to the bottom
                if (string.IsNullOrWhiteSpace(value))
                {
                    RequestScroll?.Invoke(true);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the selection process is currently paused.
        /// </summary>
        public bool IsPaused => _isSelectionActive;

        #endregion

        #region Properties - Service Data

        /// <summary>
        /// Gets the collection of services available for console viewing and monitoring.
        /// </summary>
        public ObservableCollection<ServiceItemBase> Services { get; } = new ObservableCollection<ServiceItemBase>();

        private ConsoleService _selectedService;
        /// <summary>
        /// Gets or sets the currently selected service. 
        /// Changing this resets the console history and restarts file tailing for the new service paths.
        /// </summary>
        public ConsoleService SelectedService
        {
            get => _selectedService;
            set
            {
                if (ReferenceEquals(_selectedService, value)) return;
                _selectedService = value;
                _stdoutPath = value?.StdoutPath;
                _stderrPath = value?.StderrPath;
                OnPropertyChanged(nameof(SelectedService));

                CopyPidCommand?.RaiseCanExecuteChanged();

                SwitchService(_stdoutPath, _stderrPath);

                StopMonitoring(false); // Pass false so we don't clear the console
                StartMonitoring();
            }
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
        /// Command to clear selection.
        /// </summary>
        public ICommand ClearSelectionCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleViewModel"/> class.
        /// Sets up log filtering and refresh timers.
        /// </summary>
        /// <param name="serviceRepository">Repository for service data access.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        public ConsoleViewModel(IServiceRepository serviceRepository, IServiceCommands serviceCommands)
        {
            _serviceRepository = serviceRepository;
            ServiceCommands = serviceCommands;
            SearchCommand = new AsyncCommand(SearchServicesAsync);
            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null);
            ClearSelectionCommand = new RelayCommand<object>(_ => SetSelectionActive(false));

            var app = (App)Application.Current;
            _maxLines = app.ConsoleMaxLines;

            InitTimer();

            VisibleLines = CollectionViewSource.GetDefaultView(RawLines);
            VisibleLines.Filter = (obj) =>
            {
                if (string.IsNullOrWhiteSpace(ConsoleSearchText)) return true;
                var line = obj as LogLine;
                return line?.Text.IndexOf(ConsoleSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Waits for a specified delay (300ms) after the last keystroke before 
        /// refreshing the visible lines collection to prevent UI jank.
        /// </summary>
        private async Task ApplyFilterWithDebounceAsync()
        {
            // Cancel any pending search task
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();

            var token = _searchCts.Token;

            try
            {
                // Wait for the user to stop typing
                await Task.Delay(300, token);

                // Return to the UI thread to refresh the View
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        VisibleLines.Refresh();
                    }
                }, DispatcherPriority.Background);
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled by a newer keystroke; exit gracefully.
            }
        }

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
        /// Orchestrates the transition between services. This method synchronizes the history 
        /// loading of both StdOut and StdErr streams before initiating live tailing.
        /// </summary>
        /// <remarks>
        /// Uses an incrementing Session ID to ensure that log updates from previously 
        /// selected services (still in the Dispatcher queue) are ignored.
        /// </remarks>
        /// <param name="stdoutPath">Path to the standard output log.</param>
        /// <param name="stderrPath">Path to the standard error log.</param>
        private async void SwitchService(string stdoutPath, string stderrPath)
        {
            try
            {
                // 1. Increment session and cancel previous work
                int sessionId = Interlocked.Increment(ref _currentSessionId);

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                RawLines.Clear();
                OnPropertyChanged(nameof(Pid));

                // 2. Load History in parallel
                int historyLimit = _maxLines / 2;

                // Check if stderr is actually a different file from stdout
                bool hasUniqueStderr = !string.IsNullOrEmpty(stderrPath) &&
                                       !string.Equals(stdoutPath, stderrPath, StringComparison.OrdinalIgnoreCase);

                // Always prepare StdOut task if path exists
                var stdoutTask = !string.IsNullOrEmpty(stdoutPath)
                    ? new LogTailer().GetHistoryAsync(stdoutPath, LogType.StdOut, historyLimit)
                    : Task.FromResult<HistoryResult>(null);

                // Only prepare StdErr task if it is a different file
                var stderrTask = hasUniqueStderr
                    ? new LogTailer().GetHistoryAsync(stderrPath, LogType.StdErr, historyLimit)
                    : Task.FromResult<HistoryResult>(null);

                // Wait for the necessary reads to complete
                var results = await Task.WhenAll(stdoutTask, stderrTask);

                // 3. SECURITY CHECK: If sessionId doesn't match, the user switched again while we were loading
                if (sessionId != _currentSessionId) return;

                var outRes = results[0];
                var errRes = results[1];

                // 4. Merge and Sort history
                var combinedHistory = new List<LogLine>();
                if (outRes != null) combinedHistory.AddRange(outRes.Lines);
                if (errRes != null) combinedHistory.AddRange(errRes.Lines);

                // Stable sort: 
                // 1. By Time (Primary)
                // 2. Instead of using l.Id (which is raced), we use the list index as the tie-breaker.
                // 3. Move the heavy lifting off the UI thread
                var sortedHistory = await Task.Run(() =>
                    combinedHistory
                        .Select((line, index) => new { line, index })
                        .OrderBy(x => x.line.Timestamp)
                        .ThenBy(x => x.index)
                        .Select(x => x.line)
                        .ToList()
                );

                if (sortedHistory.Any())
                {
                    RawLines.AddRange(sortedHistory);
                    RequestScroll?.Invoke(true);
                }

                // 5. Start Live Tailing, passing the Session ID
                // Start StdOut tailer if the result and path are valid
                if (outRes != null && !string.IsNullOrEmpty(stdoutPath))
                {
                    StartLiveTail(stdoutPath, LogType.StdOut, outRes.Position, outRes.CreationTime, token, sessionId);
                }

                // Start StdErr tailer ONLY if it's a different file to prevent duplicate UI entries
                if (errRes != null && hasUniqueStderr)
                {
                    StartLiveTail(stderrPath, LogType.StdErr, errRes.Position, errRes.CreationTime, token, sessionId);
                }
            }
            catch (Exception ex)
            {
                // Log the error so we know why the resume failed
                Logger.Error("Failed to resume/switch logs.", ex);
            }
        }

        /// <summary>
        /// Clears the console buffers and resets UI labels.
        /// </summary>
        /// <param name="resetLabels">If true, sets the PID display to N/A.</param>
        private void ResetConsole(bool resetLabels)
        {
            if (resetLabels)
            {
                Pid = UiConstants.NotAvailable;
                _stdoutPath = null; // Clear these so Resume doesn't re-trigger
                _stderrPath = null;
            }

            SwitchService(string.Empty, string.Empty);
        }

        /// <summary>
        /// Initializes a live tailer for a single stream and subscribes to its updates.
        /// </summary>
        /// <param name="path">The log file path.</param>
        /// <param name="type">The type of log (StdOut/StdErr).</param>
        /// <param name="pos">The byte position to start reading from.</param>
        /// <param name="created">The file creation time for rotation detection.</param>
        /// <param name="token">Cancellation token for the tailing task.</param>
        /// <param name="sessionId">The session ID this tailer belongs to.</param>
        private void StartLiveTail(string path, LogType type, long pos, DateTime created, CancellationToken token, int sessionId)
        {
            var tailer = new LogTailer();
            tailer.OnNewLines += (lines) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // ONLY add the lines if this tailer still belongs to the ACTIVE session
                    if (sessionId != _currentSessionId) return;

                    // Signal the View to "Look at the selection now!"
                    RequestStatePreservation?.Invoke(true);

                    // HARD STOP: do not mutate collection while user is selecting
                    if (_isSelectionActive)
                        return;

                    RawLines.AddRange(lines);
                    RawLines.TrimToSize(_maxLines);

                    // Signal the View to "Restore it now!"
                    RequestStatePreservation?.Invoke(false);

                    RequestScroll?.Invoke(false);
                }, DispatcherPriority.Background);
            };
            _ = tailer.RunFromPosition(path, type, pos, created, token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Logger.Warn($"Log tailing failed: {t.Exception?.InnerException?.Message}");
                }, TaskContinuationOptions.OnlyOnFaulted);
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
            try
            {
                var currentSelection = SelectedService;

                if (currentSelection == null)
                {
                    if (_hadSelectedService)
                    {
                        ResetConsole(true);
                        _hadSelectedService = false;
                        CopyPidCommand?.RaiseCanExecuteChanged();
                    }
                    return;
                }
                _hadSelectedService = true;

                var serviceDto = await _serviceRepository.GetByNameAsync(currentSelection.Name, decrypt: false);

                if (serviceDto?.Pid == null)
                {
                    ResetConsole(true);
                    SelectedService.Pid = null;
                    SelectedService.StdoutPath = null;
                    SelectedService.StderrPath = null;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                    return;
                }

                if (currentSelection.Pid != serviceDto.Pid
                    || _stdoutPath != serviceDto.ActiveStdoutPath
                    || _stderrPath != serviceDto.ActiveStderrPath
                    )
                {
                    currentSelection.Pid = serviceDto.Pid;   // write to captured local, not SelectedService
                    _stdoutPath = serviceDto.ActiveStdoutPath;
                    _stderrPath = serviceDto.ActiveStderrPath;
                    SelectedService.StdoutPath = serviceDto.ActiveStdoutPath;
                    SelectedService.StderrPath = serviceDto.ActiveStderrPath;
                    SwitchService(serviceDto.ActiveStdoutPath, serviceDto.ActiveStderrPath);
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
                _tickErrorCount++;
                // Log every 10th error to prevent bloating while maintaining observability
                if (_tickErrorCount % 10 == 1)
                {
                    Logger.Warn($"OnTickAsync error (count: {_tickErrorCount}): {ex.Message}");
                }
            }
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
                    // Start with null Pid and paths; they will be updated later by the monitoring tick
                    Services.Add(new ConsoleService { Name = s.Name, Pid = null, StdoutPath = null, StderrPath = null });
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
        /// Enables the monitoring timer to begin polling for service status updates.
        /// </summary>
        public void StartMonitoring()
        {
            Interlocked.Exchange(ref _isMonitoringFlag, 1);
            
            // Start timer
            InitTimer();
            _timer?.Start();
        }

        /// <summary>
        /// Disables the monitoring timer and optionally resets the console view.
        /// </summary>
        /// <param name="clearConsole">If true, clears the console history and resets UI labels.</param>
        public void StopMonitoring(bool clearConsole)
        {
            _cancellationTokenSource?.Cancel();
            Interlocked.Exchange(ref _isMonitoringFlag, 0);
            _timer?.Stop();

            if (clearConsole)
            {
                ResetConsole(true);
            }
        }

        /// <summary>
        /// Sets the selection active state, used to manage UI selection preservation during log updates.
        /// </summary>
        /// <param name="active"></param>
        public void SetSelectionActive(bool isActive)
        {
            if (_isSelectionActive == isActive) return;

            _isSelectionActive = isActive;
            OnPropertyChanged(nameof(IsPaused));

            // When the user "Resumes" (Selection cleared)
            if (!_isSelectionActive)
            {
                // Restart the process (This will reload history + start new tailer)
                // Re-run the switch logic using the current paths.
                // This will stop the old tailer, reload the merged history, 
                // and start a fresh live tail.
                SwitchService(_stdoutPath, _stderrPath);
            }
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