using Servy.Core.Logging;
using Servy.Manager.Resources;
using Servy.UI.Services;
using Servy.UI.ViewModels;
using System.Diagnostics;
using UiHelper = Servy.UI.Helpers.Helper;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// Base class providing a unified, thread-safe seven-step search pipeline infrastructure with integrated cancellation and footer telemetry tracking.
    /// </summary>
    public abstract class SearchableViewModelBase : ViewModelBase, IDisposable
    {
        #region Fields

        private bool _isBusy;
        private string _searchButtonText = Strings.Button_Search;
        private string _footerText = string.Empty;
        private CancellationTokenSource? _searchCts;

        /// <summary>
        /// Cursor service used to manage visual wait state boundaries.
        /// </summary>
        protected readonly ICursorService _cursorService;

        /// <summary>
        /// Atomic flag representing whether the current instance has been disposed (0 = false, 1 = true).
        /// </summary>
        protected int _isDisposed = 0;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether a background operation is running.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        /// <summary>
        /// Gets or sets footer text displayed in the UI.
        /// </summary>
        public string FooterText
        {
            get => _footerText;
            set => Set(ref _footerText, value);
        }

        /// <summary>
        /// Text displayed on the search button.
        /// </summary>
        public string SearchButtonText
        {
            get => _searchButtonText;
            set => Set(ref _searchButtonText, value);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchableViewModelBase"/> class.
        /// </summary>
        /// <param name="cursorService">The cursor service abstraction handle.</param>
        protected SearchableViewModelBase(ICursorService cursorService)
        {
            _cursorService = cursorService ?? throw new ArgumentNullException(nameof(cursorService));
        }

        #endregion

        #region Protected Pipeline Core

        /// <summary>
        /// Runs the centralized seven-step search pipeline engine thread-safely.
        /// </summary>
        /// <param name="fetchAndApplyAsync">Asynchronous delegate to execute the data query and process collection changes.</param>
        /// <param name="noneFormat">Footer format string used when the search returns no matches.</param>
        /// <param name="oneFormat">Footer format string used when the search returns exactly one match.</param>
        /// <param name="manyFormat">Footer format string used when the search returns multiple matches.</param>
        /// <param name="onPreFetchYieldAsync">Optional hook parameter allowing views to trigger intermediary UI thread dispatcher yielding.</param>
        /// <returns>A task that represents the asynchronous search operation.</returns>
        protected async Task ExecuteSearchPipelineAsync(
            Func<CancellationToken, Task<int>> fetchAndApplyAsync,
            string? noneFormat = null,
            string? oneFormat = null,
            string? manyFormat = null,
            Func<Task>? onPreFetchYieldAsync = null)
        {
            // LIFECYCLE GATE: Bail immediately if the ViewModel has already initiated teardown
            if (Volatile.Read(ref _isDisposed) != 0)
            {
                return;
            }

            // Step 1 & 2: Stopwatch initialization and atomic thread-safe CTS swap
            var stopwatch = Stopwatch.StartNew();
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _searchCts, newCts);

            if (oldCts != null)
            {
                Helpers.Helper.CancelAndDisposeSafely(oldCts);
            }

            CancellationToken token;
            try
            {
                // RACING DISPOSE CHECK: Capture token safely. If a racing Dispose pulled this
                // instance out from under us and processed it, this will throw an ObjectDisposedException.
                token = newCts.Token;

                // Re-verify global disposal state immediately after the swap to handle tight race windows
                if (Volatile.Read(ref _isDisposed) != 0)
                {
                    Helpers.Helper.CancelAndDisposeSafely(newCts);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                // Captured CTS was cancelled and disposed mid-swap by a racing Dispose thread. Exit cleanly.
                return;
            }

            try
            {
                // Step 3 & 4: Clear footer text and present wait state boundaries immediately
                FooterText = string.Empty; // Clear footer text before search
                _cursorService.SetWaitCursor();
                SearchButtonText = Strings.Button_Searching;
                IsBusy = true;

                if (onPreFetchYieldAsync != null)
                {
                    await onPreFetchYieldAsync();
                }

                // Step 5 & 6: Execute site-specific query actions and update underlying collection records
                int matchCount = await fetchAndApplyAsync(token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                stopwatch.Stop();

                if (!string.IsNullOrEmpty(noneFormat) || !string.IsNullOrEmpty(oneFormat) || !string.IsNullOrEmpty(manyFormat))
                {
                    FooterText = UiHelper.GetRowsInfo(
                        count: matchCount,
                        duration: stopwatch.Elapsed,
                        noneFormat: noneFormat ?? string.Empty,
                        oneFormat: oneFormat ?? string.Empty,
                        manyFormat: manyFormat ?? string.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled by a newer request or active termination sequence; exit gracefully.
            }
            catch (Exception ex)
            {
                Logger.Error($"Search pipeline failed in {GetType().Name}.", ex);
                await HandleSearchExceptionAsync(ex);
            }
            finally
            {
                // Step 7: Stale-search recovery gate check. Restore original context states safely.
                if (ReferenceEquals(Volatile.Read(ref _searchCts), newCts))
                {
                    _cursorService.ResetCursor();
                    SearchButtonText = Strings.Button_Search;
                    IsBusy = false;
                }
            }
        }

        /// <summary>
        /// When overridden in a derived class, provides a hook to display modal alert feedback if the underlying fetch sequence encounters an error.
        /// </summary>
        protected virtual Task HandleSearchExceptionAsync(Exception ex) => Task.CompletedTask;

        /// <summary>
        /// Cancels the active search token and restores UI state (cursor, button text, busy flag).
        /// </summary>
        protected void ClearActiveSearchContext()
        {
            var oldCts = Interlocked.Exchange(ref _searchCts, null);
            if (oldCts != null)
            {
                Helpers.Helper.CancelAndDisposeSafely(oldCts);

                // No successor search will run the Step 7 restore - do it here.
                _cursorService.ResetCursor();
                SearchButtonText = Strings.Button_Search;
                IsBusy = false;
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the ViewModel, safely cancelling and releasing the search token.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> when called from <see cref="IDisposable.Dispose()"/>. This type has no finalizer,
        /// so it is never <see langword="false"/>; the parameter exists for derived types to override.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            // Trip the flag FIRST, before cleaning tokens, so racing search threads self-terminate immediately.
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            if (disposing)
            {
                ClearActiveSearchContext();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
