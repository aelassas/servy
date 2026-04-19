using Servy.Core.Logging;
using Servy.Manager.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for PerformanceView.xaml.
    /// Provides the UI for monitoring service performance metrics and searching available services.
    /// </summary>
    public partial class PerformanceView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceView"/> class.
        /// </summary>
        public PerformanceView()
        {
            InitializeComponent();

            Unloaded += (s, e) => (DataContext as PerformanceViewModel)?.Dispose();
        }

        /// <summary>
        /// Handles the Loaded event of the UserControl.
        /// Acts as a synchronous entry point that fire-and-forgets the asynchronous initialization logic.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
            => _ = UserControl_LoadedAsync(sender, e);

        /// <summary>
        /// Performs asynchronous initialization for the PerformanceView.
        /// Automatically triggers an initial service search if the service list is currently empty, 
        /// ensuring the performance graphs have a data source to monitor.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task UserControl_LoadedAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                // Only trigger the search if the ViewModel is initialized and the list is empty.
                // This ensures real-time performance monitoring has target services to track
                // immediately upon the view becoming visible.
                if (DataContext is PerformanceViewModel vm && !vm.Services.Any())
                {
                    await vm.SearchCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to perform initial service search in PerformanceView.", ex);
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
        /// Executes the <see cref="PerformanceViewModel.SearchCommand"/> specifically when the Enter key is pressed
        /// and the command is in a valid state to execute.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task SearchTextBox_KeyDownAsync(object sender, KeyEventArgs e)
        {
            // Verify the Enter key was pressed and the ViewModel is correctly bound
            // before attempting to trigger the performance data search.
            if (e.Key == Key.Enter &&
                DataContext is PerformanceViewModel vm &&
                vm.SearchCommand.CanExecute(null))
            {
                try
                {
                    await vm.SearchCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    Logger.Error("Search failed in PerformanceView via Enter key.", ex);
                }
            }
        }

    }
}