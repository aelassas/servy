using Servy.Core.Logging;
using Servy.Manager.Resources;
using Servy.UI.Services;
using Servy.UI.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UiHelper = Servy.UI.Helpers.Helper;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// Base class providing a unified, thread-safe seven-step search pipeline infrastructure with integrated cancellation and footer telemetry tracking.
    /// </summary>
    public abstract class SearchableViewModelBase : ViewModelBase
    {
        #region Fields

        private bool _isBusy;
        private string _searchButtonText = Strings.Button_Search;
        private string _footerText = string.Empty;
        private CancellationTokenSource _searchCts;

        /// <summary>
        /// Cursor service used to manage visual wait state boundaries.
        /// </summary>
        protected readonly ICursorService _cursorService;

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
        /// <param name="noneFormat">String format constraint applied when zero matches return.</param>
        /// <param name="oneFormat">String format constraint applied when exactly one match returns.</param>
        /// <param name="manyFormat">String format constraint applied when multiple matches return.</param>
        /// <param name="onPreFetchYieldAsync">Optional hook parameter allowing views to trigger intermediary UI thread dispatcher yielding.</param>
        /// <returns>A tracking execution task container handle.</returns>
        protected async Task ExecuteSearchPipelineAsync(
            Func<CancellationToken, Task<int>> fetchAndApplyAsync,
            string noneFormat,
            string oneFormat,
            string manyFormat,
            Func<Task> onPreFetchYieldAsync = null)
        {
            // Step 1 & 2: Stopwatch initialization and atomic thread-safe CTS swap
            var stopwatch = Stopwatch.StartNew();
            var newCts = new CancellationTokenSource();
            var token = newCts.Token;
            var oldCts = Interlocked.Exchange(ref _searchCts, newCts);

            if (oldCts != null)
            {
                Helpers.Helper.CancelAndDisposeSafely(oldCts);
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

                token.ThrowIfCancellationRequested();
                stopwatch.Stop();

                FooterText = UiHelper.GetRowsInfo(
                    count: matchCount,
                    duration: stopwatch.Elapsed,
                    noneFormat: noneFormat,
                    oneFormat: oneFormat,
                    manyFormat: manyFormat);
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled by a newer request or active termination sequence; exit gracefully.
            }
            catch (Exception ex)
            {
                Logger.Error($"Search pipeline executed with a critical anomaly in {GetType().Name}.", ex);
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
        /// Safely terminates ongoing script execution channels and tears down backing components.
        /// </summary>
        protected void ClearActiveSearchContext()
        {
            var oldCts = Interlocked.Exchange(ref _searchCts, null);
            if (oldCts != null)
            {
                Helpers.Helper.CancelAndDisposeSafely(oldCts);
            }
        }

        #endregion
    }
}