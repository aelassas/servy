using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.UI;
using Servy.UI.Commands;
using Servy.UI.Services;

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
    public abstract class ServiceSearchViewModelBase : SearchableViewModelBase
    {
        #region Private fields

        private string? _searchText;

        #endregion

        #region Protected fields

        /// <summary>
        /// UI dispatcher for yielding control back to the UI thread during long-running operations.
        /// </summary>
        protected readonly IUiDispatcher _uiDispatcher;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the collection of services retrieved during the last search.
        /// </summary>
        public BulkObservableCollection<ServiceItemBase> Services { get; } = new BulkObservableCollection<ServiceItemBase>();

        /// <summary>
        /// Gets or sets the text filter used for searching services.
        /// </summary>
        public string? SearchText
        {
            get => _searchText;
            set => Set(ref _searchText, value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets or sets the command engine for executing service-level operations.
        /// </summary>
        public IServiceCommands ServiceCommands { get; set; }

        /// <summary>
        /// Gets or sets the command triggered by the UI to start a new search.
        /// The set is used in unit tests.
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
            : base(cursorService)
        {
            _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
            ServiceCommands = serviceCommands ?? throw new ArgumentNullException(nameof(serviceCommands));
            SearchCommand = new AsyncCommand(SearchServicesAsync, name: nameof(SearchCommand));
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// When implemented in a derived class, creates a view-specific service model
        /// (e.g., ConsoleService, PerformanceService) from a raw Service entity.
        /// </summary>
        /// <param name="service">The raw service entity returned from the repository.</param>
        /// <returns>A specialized <see cref="ServiceItemBase"/> instance.</returns>
        protected abstract ServiceItemBase CreateServiceItem(Service? service);

        #endregion

        #region Private Methods

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
        private async Task SearchServicesAsync(object? parameter)
        {
            if (ServiceCommands == null)
            {
                Logger.Warn($"ServiceCommands is not set in {GetType().Name}. Search operation aborted.");
                return;
            }

            await ExecuteSearchPipelineAsync(
                async token =>
                {
                    var results = await ServiceCommands.SearchServicesAsync(SearchText, false, token);

                    if (token.IsCancellationRequested) return 0;

                    Services.Clear();
                    Services.AddRange(results.Select(CreateServiceItem));
                    return Services.Count;
                },
                onPreFetchYieldAsync: () => _uiDispatcher.YieldAsync());
        }

        #endregion

        #region IDisposable Implementation

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        #endregion
    }
}
