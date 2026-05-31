using Servy.Core.Logging;
using Servy.Manager.Services;
using Servy.UI.Services;
using System.Windows.Threading;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// Base class that provides robust, re-entrant-safe monitoring timer logic.
    /// </summary>
    /// <remarks>
    /// This class utilizes <see cref="Interlocked"/> operations to ensure that overlapping timer ticks 
    /// do not result in concurrent execution of the polling logic. It also prevents the timer 
    /// from "resurrecting" if a stop request is issued while a tick is currently processing.
    /// </remarks>
    public abstract class MonitoringViewModelBase : ServiceSearchViewModelBase
    {
        /// <summary>
        /// The timer used to trigger periodic monitoring updates on the UI thread dispatcher.
        /// </summary>
        protected DispatcherTimer? _timer;

        /// <summary>
        /// Provides a cancellation token to background tasks associated with the current active monitoring session.
        /// </summary>
        protected CancellationTokenSource? _monitoringCts;

        /// <summary>
        /// Atomic flag representing the overall monitoring state. 
        /// 0 = Stopped, 1 = Monitoring.
        /// </summary>
        protected int _isMonitoringFlag = 0;

        /// <summary>
        /// Atomic flag representing the execution state of the current tick. 
        /// 0 = Idle, 1 = Processing.
        /// </summary>
        protected int _isTickRunningFlag = 0;

        /// <summary>
        /// Tracks the total number of sequential monitoring failures to support log rate-limiting.
        /// </summary>
        private long _tickErrorCount = 0;

        /// <summary>
        /// Tracks whether the instance has already been disposed to prevent redundant cleanup.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// Gets the refresh interval in milliseconds for the monitoring timer.
        /// </summary>
        /// <value>The delay between consecutive monitoring ticks.</value>
        protected abstract int RefreshIntervalMs { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringViewModelBase"/> class.
        /// </summary>
        /// <param name="cursorService">Service to manage cursor state.</param>
        /// <param name="uiDispatcher">Dispatcher for UI thread operations.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        protected MonitoringViewModelBase(ICursorService cursorService, IUiDispatcher uiDispatcher, IServiceCommands serviceCommands)
            : base(cursorService, uiDispatcher, serviceCommands)
        {
        }

        /// <summary>
        /// Initializes the <see cref="DispatcherTimer"/> if it has not been created yet, 
        /// binding it to the defined <see cref="RefreshIntervalMs"/> and hooking the tick event.
        /// </summary>
        protected void InitTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs) };
                _timer.Tick += OnTick;
            }
        }

        /// <summary>
        /// Handles the <see cref="DispatcherTimer.Tick"/> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        /// <remarks>
        /// This method implements an atomic guard to prevent re-entrancy. The timer is explicitly stopped 
        /// during the asynchronous execution of <see cref="OnTickAsync"/> and restarted in the finally block 
        /// only if <see cref="_isMonitoringFlag"/> indicates monitoring is still requested.
        /// </remarks>
        private async void OnTick(object? sender, EventArgs? e)
        {
            // 1. Atomic Guard: Must be monitoring AND not already running a tick
            if (Volatile.Read(ref _isMonitoringFlag) == 0 ||
                Interlocked.CompareExchange(ref _isTickRunningFlag, 1, 0) == 1)
            {
                return;
            }

            _timer?.Stop();

            try
            {
                await OnTickAsync();

                // Reset error counter upon a completely successful operation sequence
                Interlocked.Exchange(ref _tickErrorCount, 0);
            }
            catch (OperationCanceledException)
            {
                // Expected behavior during shutdown or monitoring reset.
                Interlocked.Exchange(ref _tickErrorCount, 0);
            }
            catch (Exception ex)
            {
                // Unify error policy: Throttles background logging without losing observability traces.
                // Increments atomically to maintain accurate tracking if multi-tab components ever fire concurrently.
                long currentErrorCount = Interlocked.Increment(ref _tickErrorCount);

                if (currentErrorCount % 10 == 1)
                {
                    Logger.Warn($"Background monitoring tick failed in {GetType().Name} (Consecutive Failure Count: {currentErrorCount}).", ex);
                }

                // Do NOT rethrow - async void would terminate the dispatcher and crash the Manager UI.
            }
            finally
            {
                // Release the execution flag
                Interlocked.Exchange(ref _isTickRunningFlag, 0);

                // 2. Safety Check: Only restart if we are STILL supposed to be monitoring
                if (Volatile.Read(ref _isMonitoringFlag) == 1)
                {
                    _timer?.Start();
                }
            }
        }

        /// <summary>
        /// When overridden in a derived class, performs the asynchronous monitoring and polling logic for the specific view.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected abstract Task OnTickAsync();

        /// <summary>
        /// Thread-safely cancels any active monitoring operations and creates a fresh <see cref="CancellationTokenSource"/>.
        /// </summary>
        protected void ResetMonitoringCts()
        {
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _monitoringCts, newCts);
            if (oldCts != null)
            {
                Helpers.Helper.CancelAndDisposeSafely(oldCts);
            }
        }

        /// <summary>
        /// Starts the performance monitoring timer, initializes the cancellation context, 
        /// and atomically sets the monitoring flag to active.
        /// </summary>
        public virtual void StartMonitoring()
        {
            ResetMonitoringCts();
            Interlocked.Exchange(ref _isMonitoringFlag, 1);
            InitTimer();
            _timer?.Start();
        }

        /// <summary>
        /// Stops the performance monitoring timer, cancels any in-flight background operations, 
        /// and atomically sets the monitoring flag to stopped.
        /// </summary>
        /// <param name="clearView">If <see langword="true"/>, signals the derived view model to clear its data to reflect a stopped state.</param>
        public virtual void StopMonitoring(bool clearView = false)
        {
            _monitoringCts?.Cancel();
            Interlocked.Exchange(ref _isMonitoringFlag, 0);
            _timer?.Stop();

            // Unconditionally fire the stop extension point so derived models can run teardown logic
            OnMonitoringStopped(clearView);
        }

        /// <summary>
        /// Invoked unconditionally by <see cref="StopMonitoring"/> to allow derived classes to execute 
        /// teardown logic, state persistence, or view clearing operations.
        /// </summary>
        /// <param name="clearView">Indicates whether the caller explicitly requested the view model to clear its visual state.</param>
        protected virtual void OnMonitoringStopped(bool clearView)
        {
            // Base implementation is empty. Derived view models (e.g., PerformanceViewModel)
            // should override this to handle shutdown tasks, such as flushing snapshots,
            // disposing helpers, or clearing their observable collections if clearView is true.
        }

        /// <summary>
        /// Safely retrieves the current monitoring cancellation token.
        /// If the monitoring source has not been initialized, returns <see cref="CancellationToken.None"/>.
        /// If the source has been disposed, returns a pre-cancelled token to safely prevent new operations.
        /// </summary>
        /// <returns>A valid <see cref="CancellationToken"/> linked to the current monitoring lifecycle.</returns>
        protected CancellationToken GetCurrentMonitoringToken()
        {
            var cts = Volatile.Read(ref _monitoringCts);
            if (cts == null) return CancellationToken.None;
            try { return cts.Token; }
            catch (ObjectDisposedException) { return new CancellationToken(canceled: true); }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Explicitly unhooks timer events to prevent <see cref="DispatcherTimer"/> memory leaks.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release both managed and unmanaged resources; 
        /// <see langword="false"/> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    var oldMonitoringCts = Interlocked.Exchange(ref _monitoringCts, null);
                    if (oldMonitoringCts != null)
                    {
                        Helpers.Helper.CancelAndDisposeSafely(oldMonitoringCts);
                    }

                    if (_timer != null)
                    {
                        _timer.Stop();
                        _timer.Tick -= OnTick; // CRITICAL: Prevents the Dispatcher leak
                        _timer = null;
                    }
                }

                base.Dispose(disposing);
                _isDisposed = true;
            }
        }
    }
}