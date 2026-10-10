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
        /// Handles the <see cref="FrameworkElement.Loaded"/> event for the LogsView.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data that contains information about the event.</param>
        private void LogsView_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreColumnVisibilities();
        }

        /// <summary>
        /// Restores column visibilities based on the configured LogsHiddenColumns string.
        /// </summary>
        private void RestoreColumnVisibilities()
        {
            var app = Application.Current as App;
            if (app == null || string.IsNullOrWhiteSpace(app.LogsHiddenColumns)) return;

            var hiddenSet = new HashSet<string>(
                app.LogsHiddenColumns.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var column in LogsDataGrid.Columns)
            {
                string? id = null;
                if (column.Header is string headerText)
                {
                    id = headerText;
                }

                if (!string.IsNullOrEmpty(id) && hiddenSet.Contains(id))
                {
                    column.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Handles context menu opening to sync checkbox state with actual column visibility.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data that contains information about the event.</param>
        private void ColumnHeaderContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu)
            {
                foreach (var item in contextMenu.Items)
                {
                    if (item is MenuItem menuItem && menuItem.Tag is DataGridColumn column)
                    {
                        menuItem.IsChecked = column.Visibility == Visibility.Visible;
                    }
                }
            }
        }

        /// <summary>
        /// Handles menu item click to toggle column visibility and persist the change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data that contains information about the event.</param>
        private void ToggleColumnVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is DataGridColumn column)
            {
                column.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

                SaveColumnVisibilities();
            }
        }

        /// <summary>
        /// Serializes collapsed column identifiers to a comma-separated string and saves them.
        /// </summary>
        private void SaveColumnVisibilities()
        {
            var hiddenColumns = LogsDataGrid.Columns
                .Where(c => c.Visibility == Visibility.Collapsed)
                .Select(c => c.Header as string)
                .Where(id => !string.IsNullOrEmpty(id));

            string csv = string.Join(",", hiddenColumns);

            if (Application.Current is App app)
            {
                app.SaveLogsHiddenColumns(csv);
            }
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
