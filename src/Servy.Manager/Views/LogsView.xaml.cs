using Servy.Manager.ViewModels;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for <see cref="LogsView"/>.
    /// Represents the Logs tab UI in Servy Manager.
    /// Subscribes to the <see cref="LogsViewModel.ScrollLogsToTopRequested"/> event
    /// to scroll the logs DataGrid to the top when requested, and cancels any in-flight
    /// search when the view is unloaded.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class LogsView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogsView"/> class, subscribing to
        /// <see cref="FrameworkElement.DataContextChanged"/> to re-wire the view model and to
        /// <see cref="FrameworkElement.Unloaded"/> to cancel any search still running.
        /// </summary>
        public LogsView()
        {
            InitializeComponent();
            DataContextChanged += LogsView_DataContextChanged;
            Unloaded += (s, e) => (DataContext as LogsViewModel)?.CancelSearch();
        }

        /// <summary>
        /// Handles the <see cref="FrameworkElement.DataContextChanged"/> event.
        /// Unsubscribes from the old <see cref="LogsViewModel"/> events and
        /// subscribes to the new one to ensure the view responds to
        /// <see cref="LogsViewModel.ScrollLogsToTopRequested"/>.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data that contains old and new <see cref="DataContext"/> values.</param>
        private void LogsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is LogsViewModel oldVm)
            {
                oldVm.ScrollLogsToTopRequested -= OnScrollLogsToTopRequested;
            }

            if (e.NewValue is LogsViewModel newVm)
            {
                newVm.ScrollLogsToTopRequested += OnScrollLogsToTopRequested;
            }
        }

        /// <summary>
        /// Scrolls the <see cref="LogsDataGrid"/> to the first item.
        /// Called when <see cref="LogsViewModel.ScrollLogsToTopRequested"/> is raised.
        /// </summary>
        private void OnScrollLogsToTopRequested()
        {
            if (LogsDataGrid.Items.Count == 0)
                return;

            // Realize the rows first: ScrollIntoView cannot reach an item the
            // virtualizing panel has not generated yet.
            LogsDataGrid.UpdateLayout();

            LogsDataGrid.ScrollIntoView(LogsDataGrid.Items[0]);
        }
    }
}
