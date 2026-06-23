using Servy.Core.Logging;
using Servy.Manager.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Provides a shared base implementation for search-capable user controls to unify event handler routines.
    /// </summary>
    public abstract class ServiceSearchUserControl : UserControl
    {
        /// <summary>
        /// Gets the distinct name of the view used to build explicit contextual log messages.
        /// </summary>
        protected abstract string ViewName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceSearchUserControl"/> class.
        /// </summary>
        protected ServiceSearchUserControl()
        {
            Loaded += UserControl_Loaded;
            Unloaded += (s, e) => (DataContext as ServiceSearchViewModelBase)?.Dispose();
        }

        /// <summary>
        /// Routes the synchronous Loaded event into an asynchronous initialization sequence.
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = UserControl_LoadedAsync(sender, e);
        }

        /// <summary>
        /// Performs asynchronous initialization for the UserControl.
        /// Automatically triggers an initial service search if the service list is currently empty.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task UserControl_LoadedAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                // Only trigger the search if the ViewModel is initialized and the list is empty
                // to avoid redundant API/DB calls on view switching.
                if (DataContext is ServiceSearchViewModelBase vm && !vm.Services.Any())
                {
                    await vm.SearchCommand.ExecuteAsync(null);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected - a newer tab navigation / window close superseded this initial load.
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to perform initial service search in {ViewName}.", ex);
            }
        }
    }
}