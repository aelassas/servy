using Servy.Manager.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for <see cref="LogsView"/>.
    /// Represents the Logs tab UI in Servy Manager.
    /// Subscribes to the <see cref="LogsViewModel.ScrollLogsToTopRequested"/> event
    /// to scroll the logs DataGrid to the top when requested.
    /// </summary>
    public partial class LogsView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogsView"/> class
        /// and subscribes to <see cref="FrameworkElement.DataContextChanged"/> 
        /// to handle changes in the view model.
        /// </summary>
        public LogsView()
        {
            InitializeComponent();
            DataContextChanged += LogsView_DataContextChanged;
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

            LogsDataGrid.UpdateLayout();

            // Scroll first item into view
            LogsDataGrid.ScrollIntoView(LogsDataGrid.Items[0]);
        }

    }

}