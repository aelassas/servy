using Servy.Core.Logging;
using Servy.Manager.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for DependenciesView.xaml.
    /// Provides the UI for live-monitoring stdout/stderr and searching available services.
    /// </summary>
    public partial class DependenciesView : UserControl
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesView"/> class.
        /// Sets up the data context change listener to wire up ViewModel events and manages selection changes.
        /// </summary>
        public DependenciesView()
        {
            InitializeComponent();

            Unloaded += (s, e) => (DataContext as DependenciesViewModel)?.Dispose();
        }

        /// <summary>
        /// Handles the Loaded event of the UserControl.
        /// This method acts as a synchronous entry point that fire-and-forgets the asynchronous initialization.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
            => _ = UserControl_LoadedAsync(sender, e);

        /// <summary>
        /// Performs asynchronous initialization for the UserControl.
        /// Automatically triggers an initial service search if the service list in the <see cref="DependenciesViewModel"/> is currently empty.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task UserControl_LoadedAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ensure the DataContext is a DependenciesViewModel and check if 
                // we need to populate the service list to avoid redundant database calls.
                if (DataContext is DependenciesViewModel vm && !vm.Services.Any())
                {
                    await vm.SearchCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to perform initial service search in DependenciesView.", ex);
            }
        }

        /// <summary>
        /// Handles the KeyDown event of the SearchTextBox.
        /// Acts as a synchronous wrapper that fire-and-forgets the asynchronous key processing logic.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
            => _ = SearchTextBox_KeyDownAsync(sender, e);

        /// <summary>
        /// Asynchronously processes key down events for the SearchTextBox.
        /// Executes the <see cref="DependenciesViewModel.SearchCommand"/> specifically when the Enter key is pressed
        /// and the command is in a valid state to execute.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task SearchTextBox_KeyDownAsync(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                DataContext is DependenciesViewModel vm &&
                vm.SearchCommand.CanExecute(null))
            {
                try
                {
                    // Trigger the search command to update the dependency graph
                    // based on the current filter criteria.
                    await vm.SearchCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    Logger.Error("Search failed in DependenciesView via Enter key.", ex);
                }
            }
        }

    }
}