using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Manager.Config;
using Servy.Manager.Design;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.Manager.Utils;
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Constants;
using Servy.UI.Services;
using System.ComponentModel;
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
        #region Fields

        private readonly IServiceRepository _serviceRepository;

        // Controls console log filter debouncing
        private CancellationTokenSource? _logFilterCts;
        // Controls the lifecycle of the background LogTailer streams
        private CancellationTokenSource? _tailingCts;

        private bool _hadSelectedService;
        private string? _consoleSearchText;
        private readonly int _maxLines;
        private string? _stdoutPath;
        private string? _stderrPath;
        private int _currentSessionId = 0; // Track the "active" switch request
        private volatile bool _isSelectionActive;
        private bool _isDisposed;
        private readonly IAppConfiguration _appConfig;

        // Active tailers and their handlers to prevent memory leaks during service switching
        private LogTailer? _activeStdoutTailer;
        private NewLinesHandler? _stdoutTailerHandler;

        private LogTailer? _activeStderrTailer;
        private NewLinesHandler? _stderrTailerHandler;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the view should scroll to the bottom. 
        /// Boolean parameter indicates if the scroll is a "forced" reset (e.g., after clearing search).
        /// </summary>
        public event Action<bool>? RequestScroll;

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
        public string? ConsoleSearchText
        {
            get => _consoleSearchText;
            set
            {
                _consoleSearchText = value;
                OnPropertyChanged(nameof(ConsoleSearchText));

                // Trigger debounced refresh instead of immediate refresh
                _ = ApplyFilterWithDebounceAsync();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the selection process is currently paused.
        /// </summary>
        public bool IsPaused => _isSelectionActive;

        #endregion

        #region Properties - Service Data

        private ConsoleService? _selectedService;

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
        public ConsoleService? SelectedService
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

        #region Commands

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
        /// <param name="appConfig">Application configuration settings.</param>
        /// <param name="cursorService">Service used to control the cursor state.</param>
        /// <param name="uiDispatcher">Dispatcher for UI thread operations.</param>
        public ConsoleViewModel(
            IServiceRepository serviceRepository,
            IServiceCommands serviceCommands,
            IAppConfiguration appConfig,
            ICursorService cursorService,
            IUiDispatcher uiDispatcher
            ) : base(cursorService, uiDispatcher, serviceCommands)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            ClearSelectionCommand = new RelayCommand<object>(_ => SetSelectionActive(false));

            // Capture while on the UI thread during creation
            _maxLines = _appConfig.ConsoleMaxLines;

            InitTimer();

            VisibleLines = new ListCollectionView(RawLines)
            {
                Filter = (obj) =>
                {
                    if (string.IsNullOrWhiteSpace(ConsoleSearchText)) return true;
                    var line = obj as LogLine;
                    return line?.Text.IndexOf(ConsoleSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            };
        }

        /// <summary>
        /// Design-Time constructor.
        /// </summary>
        public ConsoleViewModel() : this(
            new UI.Design.DesignTimeServiceRepository(),
            new DesignTimeServiceCommands(),
            new DesignTimeAppConfig(),
            new UI.Design.DesignTimeCursorService(),
            new UI.Design.DesignTimeUiDispatcher()
            )
        { }

        #endregion

        #region MonitoringViewModelBase Implementation

        /// <inheritdoc/>
        protected override ServiceItemBase? SelectedServiceItem => SelectedService;

        /// <inheritdoc/>
        protected override int RefreshIntervalMs => _appConfig.ConsoleRefreshIntervalInMs;

        /// <inheritdoc/>
        protected override ServiceItemBase CreateServiceItem(Service? service)
        {
            return new ConsoleService { Name = service?.Name, Pid = null, StdoutPath = null, StderrPath = null };
        }

        /// <inheritdoc/>
        protected override async Task OnTickAsync()
        {
            var token = GetCurrentMonitoringToken();

            var currentSelection = SelectedService;
            if (currentSelection == null)
            {
                if (_hadSelectedService)
                {
                    ResetConsole();
                    _hadSelectedService = false;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }
                return;
            }
            _hadSelectedService = true;

            var serviceDto = await _serviceRepository.GetServiceConsoleStateAsync(currentSelection.Name, token);
            var stateSnapshot = serviceDto?.Clone() as ServiceConsoleStateDto;

            // Drop this tick if the user switched services while we were awaiting the DB call.
            if (!ReferenceEquals(currentSelection, _selectedService) || token.IsCancellationRequested) return;

            if (stateSnapshot?.Pid == null)
            {
                if (currentSelection.Pid != null)      // only act on the running -> stopped transition
                {
                    ResetConsole();
                    currentSelection.Pid = null;
                    currentSelection.StdoutPath = null;
                    currentSelection.StderrPath = null;
                    CopyPidCommand?.RaiseCanExecuteChanged();
                }
                return;
            }

            if (currentSelection.Pid != stateSnapshot.Pid
                || !string.Equals(_stdoutPath, stateSnapshot.ActiveStdoutPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(_stderrPath, stateSnapshot.ActiveStderrPath, StringComparison.OrdinalIgnoreCase))
            {
                currentSelection.Pid = stateSnapshot.Pid;
                _stdoutPath = stateSnapshot.ActiveStdoutPath;
                _stderrPath = stateSnapshot.ActiveStderrPath;
                currentSelection.StdoutPath = stateSnapshot.ActiveStdoutPath;
                currentSelection.StderrPath = stateSnapshot.ActiveStderrPath;

                _ = SwitchServiceAsync(stateSnapshot.ActiveStdoutPath, stateSnapshot.ActiveStderrPath);
                CopyPidCommand?.RaiseCanExecuteChanged();
            }

            SetPidText(currentSelection);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Waits for the configured debounce delay (<see cref="IAppConfiguration.SearchDebounceDelayMs"/>)
        /// after the last keystroke before refreshing the visible lines collection to prevent UI jank.
        /// </summary>
        private async Task ApplyFilterWithDebounceAsync()
        {
            // Thread-safe CTS swap for debouncing
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _logFilterCts, newCts);
            if (oldCts != null)
            {
                Helpers.Helper.CancelAndDisposeSafely(oldCts);
            }

            var token = newCts.Token;

            try
            {
                // Wait for the user to stop typing
                await Task.Delay(_appConfig.SearchDebounceDelayMs, token);

                // Return to the UI thread to refresh the View safely via the injected abstraction.
                // The abstraction inherently protects against null dispatchers during shutdown.
                await _uiDispatcher.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        VisibleLines.Refresh();

                        // If the search is cleared, trigger a scroll to the bottom
                        // Request the scroll AFTER the refresh is processed by the CollectionView
                        if (string.IsNullOrWhiteSpace(ConsoleSearchText))
                        {
                            RequestScroll?.Invoke(true);
                        }
                    }
                }, DispatcherPriority.Background);
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled by a newer keystroke; exit gracefully.
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply console filter.", ex);
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
        private async Task SwitchServiceAsync(string? stdoutPath, string? stderrPath)
        {
            try
            {
                // 1. Increment session and cancel previous work
                int sessionId = Interlocked.Increment(ref _currentSessionId);

                var newCts = new CancellationTokenSource();
                var oldCts = Interlocked.Exchange(ref _tailingCts, newCts);
                if (oldCts != null)
                {
                    Helpers.Helper.CancelAndDisposeSafely(oldCts);
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
                        ? stdoutHistoryTailer.GetHistoryAsync(stdoutPath, LogType.StdOut, historyLimit, cancellationToken: token)
                        : Task.FromResult<HistoryResult?>(null);

                    var stderrTask = hasUniqueStderr
                        ? stderrHistoryTailer.GetHistoryAsync(stderrPath, LogType.StdErr, historyLimit, cancellationToken: token)
                        : Task.FromResult<HistoryResult?>(null);

                    // Wait for the necessary reads to complete
                    var results = await Task.WhenAll(stdoutTask, stderrTask);

                    // 3. STALE-SESSION GUARD: a newer SelectedService was set while history was loading;
                    // drop these results to avoid mixing logs from the previous service.
                    if (sessionId != _currentSessionId) return;

                    var outRes = results[0];
                    var errRes = results[1];

                    // 4. Merge and Sort history
                    var combinedHistory = new List<LogLine>();
                    if (outRes != null) combinedHistory.AddRange(outRes.Lines);
                    if (errRes != null) combinedHistory.AddRange(errRes.Lines);

                    if (combinedHistory.Count > 0)
                    {
                        // Enforce sorting stability during Introspective Sort operations by capturing original sequence indexes 
                        // as a secondary tie-breaker constraint. This prevents stdout/stderr line interleaving jank on tied timestamps.
                        var indexedHistory = new List<(LogLine Line, int Index)>(combinedHistory.Count);
                        for (int i = 0; i < combinedHistory.Count; i++)
                        {
                            indexedHistory.Add((combinedHistory[i], i));
                        }

                        // Perform an in-place sort on a background thread.
                        // This avoids the GC pressure of the previous anonymous object LINQ chain.
                        await Task.Run(() =>
                        {
                            indexedHistory.Sort((a, b) =>
                            {
                                int byTime = DateTime.Compare(a.Line.Timestamp, b.Line.Timestamp);
                                return byTime != 0 ? byTime : a.Index.CompareTo(b.Index);
                            });
                        });

                        // CRITICAL RE-CHECK: Validate session state again after returning from the threaded background sorting await block.
                        // This prevents stale log data injections if a user switched selections while Task.Run was active.
                        if (sessionId != _currentSessionId) return;

                        combinedHistory.Clear();
                        for (int i = 0; i < indexedHistory.Count; i++)
                        {
                            combinedHistory.Add(indexedHistory[i].Line);
                        }

                        RawLines.AddRange(combinedHistory);
                        RequestScroll?.Invoke(true);
                    }

                    // CRITICAL RE-CHECK: Ensure session equilibrium before initializing new active log tailer handles.
                    if (sessionId != _currentSessionId) return;

                    // 5. Start Live Tailing, passing the Session ID
                    // Start StdOut tailer if the result and path are valid
                    if (outRes != null && !string.IsNullOrWhiteSpace(stdoutPath))
                    {
                        StartLiveTail(stdoutPath, LogType.StdOut, outRes.Position, outRes.CreationTime, sessionId, token);
                    }

                    // Start StdErr tailer ONLY if it's a different file to prevent duplicate UI entries
                    if (errRes != null && hasUniqueStderr)
                    {
                        StartLiveTail(stderrPath, LogType.StdErr, errRes.Position, errRes.CreationTime, sessionId, token);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Logger.Debug("Attempted to switch logs after disposal; ignoring.");
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
        private void ResetConsole()
        {
            Pid = UiConstants.NotAvailable;
            _stdoutPath = null; // Clear these so Resume doesn't re-trigger
            _stderrPath = null;

            _ = SwitchServiceAsync(string.Empty, string.Empty);
        }

        /// <summary>
        /// Initializes a live tailer for a single stream and subscribes to its updates.
        /// </summary>
        /// <param name="path">The log file path.</param>
        /// <param name="type">The type of log (StdOut/StdErr).</param>
        /// <param name="pos">The byte position to start reading from.</param>
        /// <param name="created">The file creation time for rotation detection.</param>
        /// <param name="sessionId">The session ID this tailer belongs to.</param>
        /// <param name="cancellationToken">Cancellation token for the tailing task.</param>
        private void StartLiveTail(string? path, LogType type, long pos, DateTime created, int sessionId, CancellationToken cancellationToken)
        {
            var tailer = new LogTailer();

            // Store the lambda in a local variable so we can safely register and track it
            NewLinesHandler handler = (lines) =>
            {
                // Rely on the injected IUiDispatcher instead of Application.Current.
                // The abstraction provides internal defensiveness against null dispatchers during background ticks.
                _uiDispatcher.InvokeAsync(() =>
                {
                    // ONLY add the lines if this tailer still belongs to the ACTIVE session
                    if (sessionId != _currentSessionId) return;

                    // HARD STOP: do not mutate collection while user is selecting
                    if (_isSelectionActive)
                        return;

                    RawLines.AddRange(lines);
                    RawLines.TrimToSize(_maxLines);

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

            _ = tailer.RunFromPosition(path, type, pos, created, cancellationToken)
                .ContinueWith(t =>
                {
                    var innerEx = t.Exception?.Flatten().InnerException;

                    if (innerEx is ObjectDisposedException)
                    {
                        Logger.Debug("LogTailer disposed.");
                        return;
                    }

                    if (t.IsFaulted)
                    {
                        Logger.Warn($"Log tailing failed: {innerEx?.Message}");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Handles the specific logic for resetting the console view when 
        /// monitoring is stopped with the clearView flag set to true.
        /// </summary>
        /// <param name="clearView">A value indicating whether to clear the console view.</param>
        protected override void OnMonitoringStopped(bool clearView)
        {
            if (!clearView) return;

            // Resets the console history and UI labels as required by 
            // the console-specific implementation.
            ResetConsole();
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
            if (!_isDisposed)
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

                    // 3. Dispose Log Filter Debounce CTS
                    var oldFilterCts = Interlocked.Exchange(ref _logFilterCts, null);
                    if (oldFilterCts != null)
                    {
                        oldFilterCts.Cancel();
                        oldFilterCts.Dispose();
                    }

                    // 4. Clear event invocation lists so the View can be collected even if it
                    // forgot to unsubscribe (and so stray ticks don't reach a disposed View).
                    RequestScroll = null;
                }

                base.Dispose(disposing);
                _isDisposed = true;
            }
        }

        #endregion
    }
}