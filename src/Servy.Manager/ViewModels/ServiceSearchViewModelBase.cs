using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.UI.Commands;
using Servy.UI.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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
    public abstract class ServiceSearchViewModelBase : ViewModelBase
    {
        /// <summary>
        /// Manages the cancellation lifecycle for the current service search operation.
        /// </summary>
        protected CancellationTokenSource _serviceSearchCts;

        /// <summary>
        /// Gets the collection of services retrieved during the last search.
        /// </summary>
        public ObservableCollection<ServiceItemBase> Services { get; } = new ObservableCollection<ServiceItemBase>();

        private string _searchText;
        /// <summary>
        /// Gets or sets the text filter used for searching services.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => Set(ref _searchText, value);
        }

        private string _searchButtonText = Strings.Button_Search;
        /// <summary>
        /// Gets or sets the text displayed on the search button, dynamically toggling 
        /// between 'Search' and 'Searching...' states.
        /// </summary>
        public string SearchButtonText
        {
            get => _searchButtonText;
            set => Set(ref _searchButtonText, value);
        }

        private bool _isBusy;
        /// <summary>
        /// Gets or sets a value indicating whether a search operation is currently in progress.
        /// Used to bind progress indicators in the UI.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        /// <summary>
        /// Gets or sets the command engine for executing service-level operations.
        /// </summary>
        public IServiceCommands ServiceCommands { get; set; }

        /// <summary>
        /// Gets the command triggered by the UI to start a new search.
        /// </summary>
        public IAsyncCommand SearchCommand { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceSearchViewModelBase"/> class.
        /// </summary>
        protected ServiceSearchViewModelBase()
        {
            SearchCommand = new AsyncCommand(SearchServicesAsync);
        }

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
            // Thread-safe atomic swap for cancellation
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _serviceSearchCts, newCts);
            if (oldCts != null)
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var token = newCts.Token;

            try
            {
                // Show "Searching..." immediately
                Mouse.OverrideCursor = Cursors.Wait;
                IsBusy = true;
                SearchButtonText = Strings.Button_Searching;

                // Allow WPF to repaint the button and show progress bar
                // Only execute if we are in a real UI context with a running dispatcher frame
                if (Application.Current?.Dispatcher != null && !Helper.IsRunningInUnitTest())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }

                // Execute Search
                var results = await ServiceCommands.SearchServicesAsync(SearchText, false, token);

                // Mapping is cheap; update the collection on the UI thread
                Services.Clear();
                foreach (var s in results)
                {
                    Services.Add(CreateServiceItem(s));
                }
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
                // Restore UI state
                Mouse.OverrideCursor = null;
                IsBusy = false;
                SearchButtonText = Strings.Button_Search;
            }
        }

        /// <summary>
        /// Safely cancels and disposes of the internal search cancellation token source.
        /// </summary>
        /// <remarks>
        /// Derived classes should call this method within their <c>Cleanup()</c> or <c>Dispose()</c> 
        /// implementation to prevent memory leaks and ensure background tasks are terminated.
        /// </remarks>
        protected void CleanupSearchCts()
        {
            var oldSearchCts = Interlocked.Exchange(ref _serviceSearchCts, null);
            if (oldSearchCts != null)
            {
                oldSearchCts.Cancel();
                oldSearchCts.Dispose();
            }
        }
    }
}