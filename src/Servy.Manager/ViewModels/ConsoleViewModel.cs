using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.Utils;
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using static Servy.Manager.Utils.LogTailer;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel for the Console view, responsible for real-time log tailing, 
    /// service monitoring, and log filtering.
    /// </summary>
    public class ConsoleViewModel : MonitoringViewModelBase
    {
        #region Constants

        /// <summary>
        /// Delay in milliseconds to debounce search keystrokes before filtering.
        /// </summary>
        private const int SearchDebounceDelayMs = 300;

        #endregion

        #region Fields

        private readonly IServiceRepository _serviceRepository;

        // Controls console log filter debouncing
        private CancellationTokenSource _logFilterCts;
        // Controls the lifecycle of the background LogTailer streams
        private CancellationTokenSource _tailingCts;

        private bool _hadSelectedService;
        private string _consoleSearchText;
        private readonly int _maxLines;
        private string _stdoutPath;
        private string _stderrPath;
        private int _currentSessionId = 0; // Track the "active" switch request
        private volatile bool _isSelectionActive;
        private int _tickErrorCount = 0;
        private bool _disposedValue;

        // Active tailers and their handlers to prevent memory leaks during service switching
        private LogTailer _activeStdoutTailer;
        private NewLinesHandler _stdoutTailerHandler;

        private LogTailer _activeStderrTailer;
        private NewLinesHandler _stderrTailerHandler;

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

        private ConsoleService _selectedService;

        /// <summary>
        /// Gets or sets the currently selected service for console monitoring.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>WARNING:</b> This setter has significant side effects and is NOT a lightweight operation.
        /// Setting this property triggers the following:
        /// </para>
        /// <list type="number">
        /// <item>
        /// <description><b>State Capture:</b> Synchronizes internal <c>_stdoutPath</c> and <c>_stderrPath</c> fields.</description>
        /// </item>
        /// <item>
        /// <description><b>File I/O:</b> Initiates <see cref="SwitchServiceAsync"/>, which clears the current log buffer, 
        /// performs asynchronous disk reads to load history, and starts new background tailing tasks.</description>
        /// </item>
        /// <item>
        /// <description><b>Timer Orchestration:</b> Restarts the performance monitoring loop by calling 
        /// <see cref="StopMonitoring"/> and <see cref="StartMonitoring"/>, resetting the cancellation tokens.</description>
        /// </item>
        /// </list>
        /// </remarks>
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

                // Safe fire-and-forget
                _ = SwitchServiceAsync(_stdoutPath, _stderrPath);

                StopMonitoring(false); // Pass false so we don't clear the console
                StartMonitoring();
            }
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
        /// Command to clear selection.
        /// </summary>
        public ICommand ClearSelectionCommand { get; }

        #endregion

        #region Constructors

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
            CopyPidCommand = new AsyncCommand(CopyPidAsync, _ => SelectedService?.Pid != null);
            ClearSelectionCommand = new RelayCommand<object>(_ => SetSelectionActive(false));

            // Capture while on the UI thread during creation
            if (Application.Current is App app)
            {
                _maxLines = app.ConsoleMaxLines;
            }
            else
            {
                // Sane defaults if created during a weird state (like unit tests)
                _maxLines = AppConfig.DefaultConsoleMaxLines;
            }

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

        #region MonitoringViewModelBase Implementation

        /// <inheritdoc/>
        protected override int RefreshIntervalMs
        {
            get
            {
                var intervalMs = AppConfig.DefaultConsoleRefreshIntervalInMs;
                if (Application.Current is App app)
                {
                    intervalMs = app.ConsoleRefreshIntervalInMs;
                }
                return intervalMs;
            }
        }

        /// <inheritdoc/>
        protected override ServiceItemBase CreateServiceItem(Service service)
        {
            return new ConsoleService { Name = service.Name, Pid = null, StdoutPath = null, StderrPath = null };
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
                        ResetConsole(true);
                        _hadSelectedService = false;
                        CopyPidCommand?.RaiseCanExecuteChanged();
                    }
                    return;
                }
                _hadSelectedService = true;

                var serviceDto = await _serviceRepository.GetServiceConsoleStateAsync(currentSelection.Name, token);
                var stateSnapshot = serviceDto?.Clone() as ServiceConsoleStateDto;

                if (stateSnapshot?.Pid == null)
                {
                    ResetConsole(true);
                    SelectedService.Pid = null;
                    SelectedService.StdoutPath = null;
                    SelectedService.StderrPath = null;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                    return;
                }

                if (currentSelection.Pid != stateSnapshot.Pid
                    || _stdoutPath != stateSnapshot.ActiveStdoutPath
                    || _stderrPath != stateSnapshot.ActiveStderrPath)
                {
                    currentSelection.Pid = stateSnapshot.Pid;
                    _stdoutPath = stateSnapshot.ActiveStdoutPath;
                    _stderrPath = stateSnapshot.ActiveStderrPath;
                    SelectedService.StdoutPath = stateSnapshot.ActiveStdoutPath;
                    SelectedService.StderrPath = stateSnapshot.ActiveStderrPath;

                    _ = SwitchServiceAsync(stateSnapshot.ActiveStdoutPath, stateSnapshot.ActiveStderrPath);
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
                _tickErrorCount++;
                // Log every 10th error to prevent bloating while maintaining observability
                if (_tickErrorCount % 10 == 1)
                {
                    Logger.Warn($"OnTickAsync error (count: {_tickErrorCount}): {ex.Message}");
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Waits for a specified delay (300ms) after the last keystroke before 
        /// refreshing the visible lines collection to prevent UI jank.
        /// </summary>
        private async Task ApplyFilterWithDebounceAsync()
        {
            // Thread-safe CTS swap for debouncing
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _logFilterCts, newCts);
            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var token = newCts.Token;

            try
            {
                // Wait for the user to stop typing
                await Task.Delay(SearchDebounceDelayMs, token);

                // Return to the UI thread to refresh the View safely
                if (Application.Current?.Dispatcher is Dispatcher dispatcher)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            VisibleLines.Refresh();
                        }
                    }, DispatcherPriority.Background);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled by a newer keystroke; exit gracefully.
            }
        }

        /// <summary>
        /// Disposes of active log tailers and unsubscribes from their events to prevent memory leaks.
        /// </summary>
        private void StopActiveTailers()
        {
            if (_activeStdoutTailer != null)
            {
                if (_stdoutTailerHandler != null)
                {
                    _activeStdoutTailer.OnNewLines -= _stdoutTailerHandler;
                    _stdoutTailerHandler = null;
                }
                _activeStdoutTailer.Dispose();
                _activeStdoutTailer = null;
            }

            if (_activeStderrTailer != null)
            {
                if (_stderrTailerHandler != null)
                {
                    _activeStderrTailer.OnNewLines -= _stderrTailerHandler;
                    _stderrTailerHandler = null;
                }
                _activeStderrTailer.Dispose();
                _activeStderrTailer = null;
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
        private async Task SwitchServiceAsync(string stdoutPath, string stderrPath)
        {
            try
            {
                // 1. Increment session and cancel previous work
                int sessionId = Interlocked.Increment(ref _currentSessionId);

                var newCts = new CancellationTokenSource();
                var oldCts = Interlocked.Exchange(ref _tailingCts, newCts);
                if (oldCts != null)
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }
                var token = newCts.Token;

                // Explicitly stop and dispose old tailers to sever event handler closures
                StopActiveTailers();

                RawLines.Clear();
                OnPropertyChanged(nameof(Pid));

                // 2. Load History in parallel
                int historyLimit = _maxLines / 2;

                bool hasUniqueStderr = !string.IsNullOrWhiteSpace(stderrPath) &&
                                       !string.Equals(stdoutPath, stderrPath, StringComparison.OrdinalIgnoreCase);

                using (var stdoutHistoryTailer = new LogTailer())
                using (var stderrHistoryTailer = new LogTailer())
                {
                    var stdoutTask = !string.IsNullOrWhiteSpace(stdoutPath)
                        ? stdoutHistoryTailer.GetHistoryAsync(stdoutPath, LogType.StdOut, historyLimit)
                        : Task.FromResult<HistoryResult>(null);

                    var stderrTask = hasUniqueStderr
                        ? stderrHistoryTailer.GetHistoryAsync(stderrPath, LogType.StdErr, historyLimit)
                        : Task.FromResult<HistoryResult>(null);

                    // Wait for the necessary reads to complete
                    var results = await Task.WhenAll(stdoutTask, stderrTask);

                    // 3. SECURITY CHECK: If sessionId doesn't match, user switched again while loading
                    if (sessionId != _currentSessionId) return;

                    var outRes = results[0];
                    var errRes = results[1];

                    // 4. Merge and Sort history
                    var combinedHistory = new List<LogLine>();
                    if (outRes != null) combinedHistory.AddRange(outRes.Lines);
                    if (errRes != null) combinedHistory.AddRange(errRes.Lines);

                    // Perform an in-place sort on a background thread.
                    // This avoids the GC pressure of the previous anonymous object LINQ chain.
                    await Task.Run(() =>
                    {
                        combinedHistory.Sort((a, b) => DateTime.Compare(a.Timestamp, b.Timestamp));
                    });

                    if (combinedHistory.Count > 0)
                    {
                        RawLines.AddRange(combinedHistory);
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
            }
            catch (Exception ex)
            {
                try
                {
                    // Log the error so we know why the resume failed
                    Logger.Error("Failed to resume/switch logs.", ex);
                }
                catch
                {
                    // Secondary catch blocks unhandled exception propagation if Logger itself fails
                }
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

            _ = SwitchServiceAsync(string.Empty, string.Empty);
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

            // Store the lambda in a local variable so we can safely register and track it
            NewLinesHandler handler = (lines) =>
            {
                // Guard against null dispatcher during shutdown background ticks
                if (!(Application.Current?.Dispatcher is Dispatcher dispatcher)) return;

                dispatcher.InvokeAsync(() =>
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

            tailer.OnNewLines += handler;

            // Track the tailer and handler for explicit disposal
            if (type == LogType.StdOut)
            {
                _activeStdoutTailer = tailer;
                _stdoutTailerHandler = handler;
            }
            else if (type == LogType.StdErr)
            {
                _activeStderrTailer = tailer;
                _stderrTailerHandler = handler;
            }

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
        /// <param name="service">Service model.</param>
        private void SetPidText(ServiceItemBase service)
        {
            var pidTxt = service.Pid?.ToString() ?? UiConstants.NotAvailable;
            if (Pid != pidTxt) Pid = pidTxt;
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
        /// Disables the monitoring timer and optionally resets the console view.
        /// </summary>
        /// <param name="clearConsole">If true, clears the console history and resets UI labels.</param>
        public override void StopMonitoring(bool clearConsole)
        {
            base.StopMonitoring(clearConsole);

            if (clearConsole)
            {
                ResetConsole(true);
            }
        }

        /// <summary>
        /// Sets the selection active state, used to manage UI selection preservation during log updates.
        /// </summary>
        /// <param name="isActive"></param>
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
                _ = SwitchServiceAsync(_stdoutPath, _stderrPath);
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
                    // 1. Dispose active log tailers
                    StopActiveTailers();

                    // 2. Dispose Tailing CTS
                    var oldTailingCts = Interlocked.Exchange(ref _tailingCts, null);
                    if (oldTailingCts != null)
                    {
                        oldTailingCts.Cancel();
                        oldTailingCts.Dispose();
                    }

                    // 3. Dispose Monitoring CTS
                    var oldMonitoringCts = Interlocked.Exchange(ref _monitoringCts, null);
                    if (oldMonitoringCts != null)
                    {
                        oldMonitoringCts.Cancel();
                        oldMonitoringCts.Dispose();
                    }

                    // 4. Dispose Log Filter Debounce CTS
                    var oldFilterCts = Interlocked.Exchange(ref _logFilterCts, null);
                    if (oldFilterCts != null)
                    {
                        oldFilterCts.Cancel();
                        oldFilterCts.Dispose();
                    }
                }

                base.Dispose(disposing);
                _disposedValue = true;
            }
        }

        #endregion
    }
}