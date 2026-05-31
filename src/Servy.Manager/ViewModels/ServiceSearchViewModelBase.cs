using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Services;
using Servy.UI.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// Abstract base class providing shared logic for searching and listing Windows services.
    /// </summary>
    /// <remarks>
    /// This class consolidates common UI state management (Busy indicators, search button text) 
    /// and ensures that asynchronous search operations are properly cancelled when a new 
    /// search is triggered, preventing race conditions in the UI.
    /// </remarks>
    public abstract class ServiceSearchViewModelBase : ViewModelBase, IDisposable
    {
        #region Private fields

        private string _searchText;
        private string _searchButtonText = Strings.Button_Search;
        private bool _isBusy;
        private int _isDisposed = 0; // 0 = false, 1 = true

        #endregion

        #region Protected fields

        /// <summary>
        /// Manages the cancellation lifecycle for the current service search operation.
        /// </summary>
        protected CancellationTokenSource _serviceSearchCts;

        /// <summary>
        /// UI dispatcher for yielding control back to the UI thread during long-running operations.
        /// </summary>
        protected readonly IUiDispatcher _uiDispatcher;

        /// <summary>
        /// Cursor service for managing wait cursor state during long-running operations.
        /// </summary>
        protected readonly ICursorService _cursorService;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the collection of services retrieved during the last search.
        /// </summary>
        public BulkObservableCollection<ServiceItemBase> Services { get; } = new BulkObservableCollection<ServiceItemBase>();

        /// <summary>
        /// Gets or sets the text filter used for searching services.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => Set(ref _searchText, value);
        }

        /// <summary>
        /// Gets or sets the text displayed on the search button, dynamically toggling 
        /// between 'Search' and 'Searching...' states.
        /// </summary>
        public string SearchButtonText
        {
            get => _searchButtonText;
            set => Set(ref _searchButtonText, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether a search operation is currently in progress.
        /// Used to bind progress indicators in the UI.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets or sets the command engine for executing service-level operations.
        /// </summary>
        public IServiceCommands ServiceCommands { get; set; }

        /// <summary>
        /// Gets the command triggered by the UI to start a new search.
        /// </summary>
        public IAsyncCommand SearchCommand { get; protected set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceSearchViewModelBase"/> class.
        /// </summary>
        /// <param name="cursorService">Service to manage cursor state.</param>
        /// <param name="uiDispatcher">Dispatcher for UI thread operations.</param>
        /// <param name="serviceCommands">Commands for service operations.</param>
        protected ServiceSearchViewModelBase(ICursorService cursorService, IUiDispatcher uiDispatcher, IServiceCommands serviceCommands)
        {
            _cursorService = cursorService ?? throw new ArgumentNullException(nameof(cursorService));
            _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
            ServiceCommands = serviceCommands ?? throw new ArgumentNullException(nameof(serviceCommands));
            SearchCommand = new AsyncCommand(SearchServicesAsync, name: nameof(SearchCommand));
        }

        #endregion

        /// <summary>
        /// When implemented in a derived class, creates a view-specific service model 
        /// (e.g., ConsoleService, PerformanceService) from a raw Service entity.
        /// </summary>
        /// <param name="service">The raw service entity returned from the repository.</param>
        /// <returns>A specialized <see cref="ServiceItemBase"/> instance.</returns>
        protected abstract ServiceItemBase CreateServiceItem(Service service);

        /// <summary>
        /// Orchestrates the asynchronous search process.
        /// </summary>
        /// <param name="parameter">Unused command parameter.</param>
        /// <returns>A task representing the search operation.</returns>
        /// <remarks>
        /// This method handles:
        /// <list type="bullet">
        /// <item><description>Atomic cancellation of previous search tasks.</description></item>
        /// <item><description>Cursor and UI state transitions.</description></item>
        /// <item><description>Dispatcher yielding to allow UI repaints.</description></item>
        /// <item><description>Thread-safe population of the <see cref="Services"/> collection.</description></item>
        /// </list>
        /// </remarks>
        private async Task SearchServicesAsync(object parameter)
        {
            if (ServiceCommands == null)
            {
                Logger.Warn($"ServiceCommands is not set in {GetType().Name}. Search operation aborted.");
                return;
            }

            // LIFECYCLE GATE: Bail immediately if the ViewModel has already initiated teardown
            if (Volatile.Read(ref _isDisposed) != 0)
            {
                return;
            }

            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _serviceSearchCts, newCts);
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
                _cursorService.SetWaitCursor();
                IsBusy = true;
                SearchButtonText = Strings.Button_Searching;

                // Yield to the UI dispatcher to allow visual updates before the blocking call,
                // safely bypassing during xUnit test runs.
                await _uiDispatcher.YieldAsync();

                var results = await ServiceCommands.SearchServicesAsync(SearchText, false, token);

                // Bail out if a newer search has superseded us, even if the inner call
                // produced a result before noticing the cancellation.
                if (token.IsCancellationRequested) return;

                Services.Clear();
                Services.AddRange(results.Select(CreateServiceItem));
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled by a newer request; exit gracefully.
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to search services in {GetType().Name}.", ex);
            }
            finally
            {
                _cursorService.ResetCursor();
                IsBusy = false;
                SearchButtonText = Strings.Button_Search;
            }
        }

        /// <summary>
        /// Disposes the ViewModel, safely cancelling and releasing the search token.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            // Convert flag to atomic int and trip it FIRST 
            // before cleaning tokens. This forces any racing search threads to immediately self-terminate.
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            if (disposing)
            {
                var oldSearchCts = Interlocked.Exchange(ref _serviceSearchCts, null);
                if (oldSearchCts != null)
                {
                    // Defer directly to the shared safe-teardown infrastructure tool
                    Helpers.Helper.CancelAndDisposeSafely(oldSearchCts);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}