using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Helpers;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for <see cref="MainWindow"/>.
    /// Represents the main window of the Servy Manager application.
    /// </summary>
    public partial class MainWindow : Window
    {
        private ILogger _logger;
        private IMessageBoxService _messageBoxService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class,
        /// sets up the UI components and initializes the DataContext with the main ViewModel.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = CreateMainViewModel();
        }

        /// <summary>
        /// Creates and configures the <see cref="MainViewModel"/> with all required dependencies.
        /// </summary>
        /// <returns>A fully initialized <see cref="MainViewModel"/> instance.</returns>
        private MainViewModel CreateMainViewModel()
        {
            var app = (App)Application.Current;

            // Initialize Logs view
            var logsView = new LogsView();
            var logsVm = new LogsViewModel(new EventLogLogger(AppConfig.EventSource), new EventLogService(new EventLogReader()));
            logsView.DataContext = logsVm;
            LogsTab.Content = logsView;

            // Initialize database and helpers
            var dbContext = new AppDbContext(app.ConnectionString);
            DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

            var dapperExecutor = new DapperExecutor(dbContext);
            var protectedKeyProvider = new ProtectedKeyProvider(app.AESKeyFilePath, app.AESIVFilePath);
            var securePassword = new SecurePassword(protectedKeyProvider);
            var xmlSerializer = new XmlServiceSerializer();

            var serviceRepository = new ServiceRepository(dapperExecutor, securePassword, xmlSerializer);

            // Initialize logger
            _logger = new EventLogLogger(AppConfig.EventSource);

            // Initialize service manager
            var serviceManager = new ServiceManager(
                name => new ServiceControllerWrapper(name),
                new WindowsServiceApi(),
                new Win32ErrorProvider(),
                serviceRepository,
                new WmiSearcher()
            );

            // Initialize service commands and helpers
            var fileDialogService = new FileDialogService();
            _messageBoxService = new MessageBoxService();
            var helpService = new HelpService(_messageBoxService);

            // Create main ViewModel
            var viewModel = new MainViewModel(
                _logger,
                serviceManager,
                serviceRepository,
                null,                   // We'll set ServiceCommands next
                helpService,
                _messageBoxService
            );

            var serviceConfigurationValidator = new ServiceConfigurationValidator(_messageBoxService);

            var serviceCommands = new ServiceCommands(
                serviceManager,
                serviceRepository,
                _messageBoxService,
                _logger,
                fileDialogService,
                viewModel.RemoveService,
                viewModel.Resfresh,
                serviceConfigurationValidator
            );

            viewModel.ServiceCommands = serviceCommands;

            return viewModel;
        }

        /// <summary>
        /// Handles the <see cref="Window.Loaded"/> event.
        /// Executes the initial search when the window is loaded.
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.SearchCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// Handles the KeyDown event of the search text box.
        /// Executes the search when the Enter key is pressed.
        /// </summary>
        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel vm && vm.SearchCommand.CanExecute(null))
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Handles PreviewMouseLeftButtonDown for row action buttons (Start, Stop, Restart).
        /// Executes the associated command manually.
        /// </summary>
        private void ActionButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is Button btn && btn.Command != null)
            {
                var parameter = btn.CommandParameter ?? DataContext;
                if (btn.Command.CanExecute(parameter))
                    btn.Command.Execute(parameter);
            }
        }

        /// <summary>
        /// Handles PreviewMouseLeftButtonDown for menu buttons (⋮).
        /// Opens the associated ContextMenu and sets its DataContext.
        /// </summary>
        private void MenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.DataContext = btn.DataContext;

                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Handles KeyDown on the window for global shortcuts.
        /// Executes search when F5 is pressed.
        /// </summary>
        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                if (MainTab.IsSelected && DataContext is MainViewModel vm && vm.SearchCommand.CanExecute(null))
                {
                    await vm.SearchCommand.ExecuteAsync(null);
                }
                else if (LogsTab.IsSelected && LogsTab.Content is LogsView logsView && logsView.DataContext is LogsViewModel lvm && lvm.SearchCommand.CanExecute(null))
                {
                    await lvm.SearchCommand.ExecuteAsync(null);
                }
            }

            if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && MainTab.IsSelected)
            {
                ServicesDataGrid.Focus();
                ServicesDataGrid.SelectAll();

                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the Config menu click.
        /// Executes the configure command for the main ViewModel.
        /// </summary>
        private async void Menu_ConfigClik(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.ConfigureCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// Handles the <see cref="SelectionChanged"/> event of the main <see cref="TabControl"/>.
        /// Cancels background tasks or timers when switching tabs and triggers searches
        /// for logs or services depending on the selected tab.
        /// </summary>
        /// <param name="sender">The <see cref="TabControl"/> that raised the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing event data.</param>
        /// <remarks>
        /// The tab-specific logic is split into separate methods (<see cref="HandleLogsTabSelected"/> and
        /// <see cref="HandleMainTabSelected"/>) to improve readability, maintainability, and to clearly
        /// separate concerns. This allows the main event handler to remain concise and ensures that
        /// each tab’s behavior (cleanup, searches, timers) is encapsulated in a dedicated method.
        /// </remarks>
        private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Only react if the TabControl itself fired the event
                if (!ReferenceEquals(sender, e.OriginalSource))
                    return;

                if (DataContext is MainViewModel vm)
                {
                    var perfVm = GetPerformanceVm();

                    var logsVm = GetLogsVm();

                    if (MainTab.IsSelected)
                        await HandleMainTabSelected(vm, perfVm, logsVm);
                    else if (PerformanceTab.IsSelected)
                        await HandlePerfTabSelected(vm, perfVm, logsVm);
                    else if (LogsTab.IsSelected)
                        await HandleLogsTabSelected(vm, perfVm, logsVm);

                    if (PerformanceTab.IsSelected)
                        perfVm?.StartMonitoring();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error in MainTabControl_SelectionChanged", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_MainTabControl_SelectionChangedError, AppConfig.Caption);
            }
        }

        /// <summary>
        /// Retrieves the <see cref="PerformanceViewModel"/> instance bound to the <see cref="PerformanceTab"/> content, if available.
        /// </summary>
        /// <returns>
        /// The <see cref="PerformanceViewModel"/> instance if the <see cref="PerformanceTab"/> has content and its DataContext 
        /// is a <see cref="PerformanceViewModel"/>; otherwise, <c>null</c>.
        /// </returns>
        private PerformanceViewModel GetPerformanceVm()
            => PerformanceTab.Content is PerformanceView PerformanceView ? PerformanceView.DataContext as PerformanceViewModel : null;

        /// <summary>
        /// Retrieves the <see cref="LogsViewModel"/> instance bound to the <see cref="LogsTab"/> content, if available.
        /// </summary>
        /// <returns>
        /// The <see cref="LogsViewModel"/> instance if the <see cref="LogsTab"/> has content and its DataContext 
        /// is a <see cref="LogsViewModel"/>; otherwise, <c>null</c>.
        /// </returns>
        private LogsViewModel GetLogsVm()
            => LogsTab.Content is LogsView logsView ? logsView.DataContext as LogsViewModel : null;

        /// <summary>
        /// Handles tasks when the Main tab is selected:
        /// cleans up logs tab resources, triggers a search for services if needed,
        /// and starts periodic timer updates in the main tab.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="perfVm">
        /// The <see cref="PerformanceViewModel"/> instance for the performance tab, or <c>null</c> if unavailable.
        /// </param> 
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandleMainTabSelected(MainViewModel vm, PerformanceViewModel perfVm, LogsViewModel logsVm)
        {
            // Stop timers in performance tab
            perfVm?.StopMonitoring(false);

            // Stop ongoing search in Logs tab
            logsVm?.Cleanup();

            // Run search for main tab if applicable
            if (vm.ServicesView.IsEmpty)
                await vm.SearchCommand.ExecuteAsync(null);

            // Start periodic timer updates in main tab
            vm.CreateAndStartTimer();
        }

        /// <summary>
        /// Handles tasks when the Logs tab is selected:
        /// cleans up main tab resources and triggers a search for logs if the logs collection is empty.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="perfVm">
        /// The <see cref="PerformanceViewModel"/> instance for the performance tab, or <c>null</c> if unavailable.
        /// </param> 
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandleLogsTabSelected(MainViewModel vm, PerformanceViewModel perfVm, LogsViewModel logsVm)
        {
            // Cleanup all background tasks and stop timers in main tab
            vm.Cleanup();

            // Stop timers in performance tab
            perfVm?.StopMonitoring(false);

            // Run search for logs tab if applicable
            if (logsVm != null && logsVm.LogsView.IsEmpty)
                await logsVm.SearchCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// Handles tasks when the Performance tab is selected:
        /// cleans up main tab resources and stops search in logs tab.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="perfVm">
        /// The <see cref="PerformanceViewModel"/> instance for the performance tab, or <c>null</c> if unavailable.
        /// </param> 
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandlePerfTabSelected(MainViewModel vm, PerformanceViewModel perfVm, LogsViewModel logsVm)
        {
            // Cleanup all background tasks and stop timers in main tab
            vm.Cleanup();

            // Start timers in performance tab
            perfVm?.StartMonitoring();

            // Stop ongoing search in Logs tab
            logsVm?.Cleanup();
        }

        /// <summary>
        /// Handles the <see cref="Window.Closed"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> object that contains the event data.</param>
        /// <remarks>
        /// This override ensures that when the main window is closed, all child processes
        /// spawned by the current process are terminated. This prevents orphaned processes
        /// from remaining in the system after the application exits.
        /// 
        /// The method retrieves the current process ID and passes it to
        /// <see cref="ProcessKiller.KillChildren(int)"/> to terminate all descendants.
        /// Any exceptions thrown during this cleanup are caught and logged for debugging.
        /// </remarks>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                var currentPID = Process.GetCurrentProcess().Id;
                ProcessKiller.KillChildren(currentPID);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error killing child processes: {ex}");
            }

            base.OnClosed(e);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the DataGrid "Check All" header checkbox.
        /// Toggles the SelectAll property in the MainViewModel when the user clicks the header checkbox.
        /// </summary>
        /// <param name="sender">The DataGridColumnHeader that raised the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridColumnHeader header &&
                header.Content is CheckBox cb &&
                ServicesDataGrid.DataContext is MainViewModel vm)
            {
                // Toggle the SelectAll value manually (true if unchecked or indeterminate, false if checked)
                bool newValue = cb.IsChecked != true;
                vm.SelectAll = newValue;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for cells in the checkbox column.
        /// Toggles the IsChecked property of the corresponding ServiceRowViewModel when the user clicks the checkbox cell.
        /// Also ensures that IsSelected is cleared if the checkbox is checked.
        /// </summary>
        /// <param name="sender">The DataGridCell that raised the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void CheckBoxCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only toggle for the first column, which contains the checkbox
            if (sender is DataGridCell cell && cell.DataContext is ServiceRowViewModel vm && cell.Column.DisplayIndex == 0)
            {
                vm.IsChecked = !vm.IsChecked;
                if (vm.IsChecked) vm.IsSelected = false;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the <see cref="DataGridRow.MouseDoubleClick"/> event.
        /// Toggles the <see cref="ServiceRowViewModel.IsChecked"/> property when a row is double-clicked,
        /// which visually changes the row background through a style trigger.
        /// </summary>
        /// <param name="sender">The source of the event, expected to be a <see cref="DataGridRow"/>.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing event data.</param>
        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is ServiceRowViewModel vm)
            {
                vm.IsChecked = !vm.IsChecked;

                // Optional: prevent DataGrid from entering edit mode
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles mouse clicks on the window and clears the DataGrid selection
        /// if the click occurred outside the DataGrid.
        /// </summary>
        /// <param name="sender">The source of the event (typically the Window).</param>
        /// <param name="e">Mouse button event arguments containing click information.</param>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the click was outside the DataGrid
            if (e.OriginalSource is DependencyObject source && !IsDescendantOf(source, ServicesDataGrid))
            {
                ServicesDataGrid.SelectedItems.Clear();
            }
        }

        /// <summary>
        /// Helper to check if a visual element is a child of a parent
        /// </summary>
        private bool IsDescendantOf(DependencyObject source, DependencyObject parent)
        {
            while (source != null)
            {
                if (source == parent)
                    return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }

    }
}
