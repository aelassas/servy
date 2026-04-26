#nullable enable

using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Manager.Config;
using Servy.Manager.Resources;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System.ComponentModel;
using System.Diagnostics;
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
        private readonly IMessageBoxService? _messageBoxService;
        private readonly IProcessKiller _processKiller;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class using constructor injection.
        /// </summary>
        /// <param name="mainViewModel">The primary DataContext for the application.</param>
        /// <param name="logsViewModel">The ViewModel mapped to the Logs view.</param>
        /// <param name="messageBoxService">Service for displaying UI dialogs.</param>
        /// <param name="processKiller">Service for terminating child processes on application exit.</param>
        public MainWindow(
            MainViewModel mainViewModel,
            LogsViewModel logsViewModel, 
            IMessageBoxService messageBoxService,
            IProcessKiller processKiller
            )
        {
            InitializeComponent();

            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            DataContext = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));

            // Map standalone ViewModels to their respective Views
            var logsView = new LogsView { DataContext = logsViewModel };
            LogsTab.Content = logsView;
        }

        /// <summary>
        /// Handles the <see cref="Window.Loaded"/> event.
        /// Acts as a synchronous wrapper that fire-and-forgets the asynchronous window initialization.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
            => _ = Window_LoadedAsync(sender, e);

        /// <summary>
        /// Performs asynchronous initialization when the window is loaded.
        /// Executes the initial service search via the <see cref="MainViewModel.SearchCommand"/>.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            // Trigger the global search command to populate the dashboard 
            // the moment the shell is ready for interaction.
            if (DataContext is MainViewModel vm)
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Handles the KeyDown event of the search text box.
        /// Acts as a synchronous wrapper that fire-and-forgets the asynchronous key processing.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
            => _ = SearchTextBox_KeyDownAsync(sender, e);

        /// <summary>
        /// Asynchronously processes key down events for the search text box.
        /// Executes the <see cref="MainViewModel.SearchCommand"/> specifically when the Enter key is pressed 
        /// and the command is in a valid state to execute.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task SearchTextBox_KeyDownAsync(object sender, KeyEventArgs e)
        {
            // Verify Enter key was pressed and the ViewModel is in a state where 
            // a search can be safely initiated.
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
        /// Handles the PreviewKeyDown event on the window to provide global keyboard shortcuts.
        /// Acts as a synchronous entry point that fire-and-forgets the asynchronous shortcut logic.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key and modifiers.</param>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
            => _ = Window_PreviewKeyDownAsync(sender, e);

        /// <summary>
        /// Asynchronously processes global keyboard shortcuts.
        /// Supports F5 for contextual refreshing across different tabs and Ctrl+A for service selection.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key and modifiers.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task Window_PreviewKeyDownAsync(object sender, KeyEventArgs e)
        {
            // F5: Context-aware refresh logic based on the active tab
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
                else if (DependenciesTab.IsSelected && DependenciesTab.Content is DependenciesView dependenciesView && dependenciesView.DataContext is DependenciesViewModel dvm)
                {
                    await dvm.LoadDependencyTreeAsync(null);
                }
            }

            // Ctrl + A: Select all services in the DataGrid (Dashboard only)
            if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && MainTab.IsSelected)
            {
                ServicesDataGrid.Focus();
                ServicesDataGrid.SelectAll();

                // Mark event as handled to prevent standard text-box 'Select All' if a search field has focus
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the click event for the Configuration menu item.
        /// Acts as a synchronous wrapper that fire-and-forgets the asynchronous configuration sequence.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Menu_ConfigClick(object sender, RoutedEventArgs e)
            => _ = Menu_ConfigClickAsync(sender, e);

        /// <summary>
        /// Asynchronously initiates the application configuration sequence.
        /// Executes the <see cref="MainViewModel.ConfigureCommand"/> to open the settings interface.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task Menu_ConfigClickAsync(object sender, RoutedEventArgs e)
        {
            // Verify the ViewModel is correctly bound before attempting to 
            // launch the configuration modal or view.
            if (DataContext is MainViewModel vm)
            {
                await vm.ConfigureCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Handles the <see cref="SelectionChanged"/> event of the main <see cref="TabControl"/>.
        /// Acts as a synchronous wrapper that fire-and-forgets the asynchronous tab transition logic.
        /// </summary>
        /// <param name="sender">The <see cref="TabControl"/> that raised the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing event data.</param>
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _ = MainTabControl_SelectionChangedAsync(sender, e);

        /// <summary>
        /// Asynchronously manages the lifecycle and state transitions when switching between application tabs.
        /// Cancels background tasks or timers for inactive tabs and triggers context-specific searches or 
        /// data loading for the newly selected tab.
        /// </summary>
        /// <param name="sender">The <see cref="TabControl"/> that raised the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method ensures that high-resource operations (like real-time performance monitoring or 
        /// log tailing) are throttled or stopped when the user navigates away from the respective tab. 
        /// It uses the <see cref="SelectionChangedEventArgs.OriginalSource"/> check to ensure it only 
        /// reacts to the main TabControl and not nested elements.
        /// </remarks>
        private async Task MainTabControl_SelectionChangedAsync(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Only react if the TabControl itself fired the event (prevents bubbling issues)
                if (!ReferenceEquals(sender, e.OriginalSource))
                    return;

                if (DataContext is MainViewModel vm)
                {
                    // Resolve nested ViewModels for coordinated state management
                    var perfVm = GetPerformanceVm();
                    var consoleVm = GetConsoleVm();
                    var dependenciesVM = GetDependenciesVm();
                    var logsVm = GetLogsVm();

                    // Route to the appropriate handler based on the selected tab
                    if (MainTab.IsSelected)
                        await HandleMainTabSelected(vm, perfVm, consoleVm, dependenciesVM, logsVm);
                    else if (PerformanceTab.IsSelected)
                        await HandlePerfTabSelected(vm, perfVm, consoleVm, dependenciesVM, logsVm);
                    else if (ConsoleTab.IsSelected)
                        await HandleConsoleTabSelected(vm, perfVm, consoleVm, dependenciesVM, logsVm);
                    else if (DependenciesTab.IsSelected)
                        await HandleDependenciesTabSelected(vm, perfVm, consoleVm, dependenciesVM, logsVm);
                    else if (LogsTab.IsSelected)
                        await HandleLogsTabSelected(vm, perfVm, consoleVm, dependenciesVM, logsVm);
                }
            }
            catch (Exception ex)
            {
                // Prioritize the UI notification service, falling back to static logging if initialization failed
                if (_messageBoxService != null)
                {
                    await _messageBoxService.ShowErrorAsync(
                        Strings.Msg_MainTabControl_SelectionChangedError, AppConfig.Caption);
                }
                else
                {
                    Logger.Error("Tab selection failed and message box service unavailable", ex);
                }
            }
        }

        /// <summary>
        /// Retrieves the <see cref="PerformanceViewModel"/> instance bound to the <see cref="PerformanceTab"/> content, if available.
        /// </summary>
        /// <returns>
        /// The <see cref="PerformanceViewModel"/> instance if the <see cref="PerformanceTab"/> has content and its DataContext 
        /// is a <see cref="PerformanceViewModel"/>; otherwise, <c>null</c>.
        /// </returns>
        private PerformanceViewModel? GetPerformanceVm()
            => PerformanceTab.Content is PerformanceView perfView ? perfView.DataContext as PerformanceViewModel : null;

        /// <summary>
        /// Retrieves the current console view model associated with the console tab, if available.
        /// </summary>
        /// <returns>A <see cref="ConsoleViewModel"/> instance representing the data context of the console view; or <see
        /// langword="null"/> if the console view is not present or its data context is not a <see
        /// cref="ConsoleViewModel"/>.</returns>
        private ConsoleViewModel? GetConsoleVm()
            => ConsoleTab.Content is ConsoleView consoleView ? consoleView.DataContext as ConsoleViewModel : null;

        /// <summary>
        /// Retrieves the <see cref="DependenciesViewModel"/> instance bound to the <see cref="DependenciesTab"/> content, if available.
        /// </summary>
        /// <returns>
        /// The <see cref="DependenciesViewModel"/> instance if the <see cref="DependenciesTab"/> has content and its DataContext 
        /// is a <see cref="DependenciesViewModel"/>; otherwise, <c>null</c>.
        /// </returns>
        private DependenciesViewModel? GetDependenciesVm()
            => DependenciesTab.Content is DependenciesView depsView ? depsView.DataContext as DependenciesViewModel : null;

        /// <summary>
        /// Retrieves the <see cref="LogsViewModel"/> instance bound to the <see cref="LogsTab"/> content, if available.
        /// </summary>
        /// <returns>
        /// The <see cref="LogsViewModel"/> instance if the <see cref="LogsTab"/> has content and its DataContext 
        /// is a <see cref="LogsViewModel"/>; otherwise, <c>null</c>.
        /// </returns>
        private LogsViewModel? GetLogsVm()
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
        /// <param name="dependenciesVM">
        /// The <see cref="DependenciesViewModel"/> instance for the dependencies tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandleMainTabSelected(MainViewModel vm, PerformanceViewModel? perfVm, ConsoleViewModel? consoleVM, DependenciesViewModel? dependenciesVM, LogsViewModel? logsVm)
        {
            // Stop timers in performance tab
            perfVm?.StopMonitoring(false);

            // Stop timers in console tab
            consoleVM?.StopMonitoring(false);

            // Stop timers in dependencies tab
            dependenciesVM?.StopMonitoring();

            // Stop ongoing search in Logs tab
            logsVm?.Cleanup();

            // Run search for main tab if applicable
            if (vm.ServicesView.IsEmpty)
                await vm.SearchCommand.ExecuteAsync(null);

            // Start periodic timer updates in main tab
            vm.CreateAndStartTimer();
        }

        /// <summary>
        /// Handles tasks when the Performance tab is selected:
        /// cleans up main tab resources and stops search in logs tab.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="perfVm">
        /// The <see cref="PerformanceViewModel"/> instance for the performance tab, or <c>null</c> if unavailable.
        /// </param> 
        /// <param name="consoleVM">
        /// The <see cref="ConsoleViewModel"/> instance for the console tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="dependenciesVM">
        /// The <see cref="DependenciesViewModel"/> instance for the dependencies tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandlePerfTabSelected(MainViewModel vm, PerformanceViewModel? perfVm, ConsoleViewModel? consoleVM, DependenciesViewModel? dependenciesVM, LogsViewModel? logsVm)
        {
            // Cleanup all background tasks and stop timers in main tab
            vm.Cleanup();

            // Stop timers in console tab
            consoleVM?.StopMonitoring(false);

            // Stop timers in dependencies tab
            dependenciesVM?.StopMonitoring();

            // Start timers in performance tab
            perfVm?.StartMonitoring();

            // Stop ongoing search in Logs tab
            logsVm?.Cleanup();
        }

        /// <summary>
        /// Handles the necessary state transitions and resource management when the Console tab is selected in the user
        /// interface.
        /// </summary>
        /// <remarks>This method ensures that only the Console tab's monitoring is active by stopping or
        /// cleaning up background activities in other tabs. It should be called whenever the Console tab becomes active
        /// to maintain correct application state.</remarks>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="perfVm">
        /// The <see cref="PerformanceViewModel"/> instance for the performance tab, or <c>null</c> if unavailable.
        /// </param> 
        /// <param name="consoleVM">
        /// The <see cref="ConsoleViewModel"/> instance for the console tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="dependenciesVM">
        /// The <see cref="DependenciesViewModel"/> instance for the dependencies tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandleConsoleTabSelected(MainViewModel vm, PerformanceViewModel? perfVm, ConsoleViewModel? consoleVM, DependenciesViewModel? dependenciesVM, LogsViewModel? logsVm)
        {
            // Cleanup all background tasks and stop timers in main tab
            vm.Cleanup();

            // Stop timers in performance tab
            perfVm?.StopMonitoring(false);

            // Stop timers in dependencies tab
            dependenciesVM?.StopMonitoring();

            // Start timers in console tab
            consoleVM?.StartMonitoring();

            // Stop ongoing search in Logs tab
            logsVm?.Cleanup();
        }

        /// <summary>
        /// Handles the necessary state transitions and resource management when the Dependencies tab is selected in the user
        /// interface.
        /// </summary>
        /// <remarks>This method ensures that only the Dependencies tab's monitoring is active by stopping or
        /// cleaning up background activities in other tabs. It should be called whenever the Dependencies tab becomes active
        /// to maintain correct application state.</remarks>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="perfVm">
        /// The <see cref="PerformanceViewModel"/> instance for the performance tab, or <c>null</c> if unavailable.
        /// </param> 
        /// <param name="consoleVM">
        /// The <see cref="ConsoleViewModel"/> instance for the console tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="dependenciesVM">
        /// The <see cref="DependenciesViewModel"/> instance for the dependencies tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandleDependenciesTabSelected(MainViewModel vm, PerformanceViewModel? perfVm, ConsoleViewModel? consoleVM, DependenciesViewModel? dependenciesVM, LogsViewModel? logsVm)
        {
            // Cleanup all background tasks and stop timers in main tab
            vm.Cleanup();

            // Stop timers in performance tab
            perfVm?.StopMonitoring(false);

            // Stop timers in console tab
            consoleVM?.StopMonitoring(false);

            // Start timers in dependencies tab
            dependenciesVM?.StartMonitoring();

            // Stop ongoing search in Logs tab
            logsVm?.Cleanup();
        }

        /// <summary>
        /// Handles tasks when the Logs tab is selected:
        /// cleans up main tab resources and triggers a search for logs if the logs collection is empty.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="perfVm">
        /// The <see cref="PerformanceViewModel"/> instance for the performance tab, or <c>null</c> if unavailable.
        /// </param> 
        /// <param name="consoleVM">
        /// The <see cref="ConsoleViewModel"/> instance for the console tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="dependenciesVM">
        /// The <see cref="DependenciesViewModel"/> instance for the dependencies tab, or <c>null</c> if unavailable.
        /// </param>
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandleLogsTabSelected(MainViewModel vm, PerformanceViewModel? perfVm, ConsoleViewModel? consoleVM, DependenciesViewModel? dependenciesVM, LogsViewModel? logsVm)
        {
            // Cleanup all background tasks and stop timers in main tab
            vm.Cleanup();

            // Stop timers in performance tab
            perfVm?.StopMonitoring(false);

            // Stop timers in console tab
            consoleVM?.StopMonitoring(false);

            // Stop timers in dependencies tab
            dependenciesVM?.StopMonitoring();

            // Run search for logs tab if applicable
            if (logsVm != null && logsVm.LogsView.IsEmpty)
                await logsVm.SearchCommand.ExecuteAsync(null);
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

        /// <summary>
        /// Handles the <see cref="Window.Closed"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> object that contains the event data.</param>
        /// <remarks>
        /// This override ensures that when the main window is closed:
        /// 1. All child processes spawned by Servy are terminated to prevent orphans.
        /// 2. All ViewModels are explicitly cleaned up to stop DispatcherTimers and cancel 
        ///    async background tasks.
        /// 3. Secure data and the global logger are safely disposed.
        /// </remarks>
        protected override void OnClosed(EventArgs e)
        {
            // 1. Terminate orphaned child processes
            try
            {
                int currentPID;
                using (var current = Process.GetCurrentProcess())
                {
                    currentPID = current.Id;
                }
                _processKiller.KillChildren(currentPID);
            }
            catch (Exception ex)
            {
                Logger.Error("Error killing child processes.", ex);
            }

            // 2. Explicitly stop timers and cancel background work
            // This is CRITICAL to prevent memory leaks and "zombie" ticks
            try
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.Cleanup();
                }

                GetPerformanceVm()?.Dispose();
                GetConsoleVm()?.Dispose();
                GetDependenciesVm()?.Dispose();
                GetLogsVm()?.Cleanup();
            }
            catch (Exception ex)
            {
                Logger.Error("Error during ViewModel cleanup in OnClosed.", ex);
            }

            // 3. Shutdown logging subsystem
            try
            {
                Logger.Shutdown();
            }
            catch
            {
                // Fail-silent on logger shutdown
            }

            base.OnClosed(e);
        }

        /// <summary>
        /// Ensures the entire application process is terminated when the main window is closed.
        /// </summary>
        /// <param name="e">
        /// Provides data for the closing event.
        /// </param>
        /// <remarks>
        /// This explicitly calls <see cref="Application.Current.Shutdown"/> to guarantee
        /// that no background threads, timers, or hidden windows keep the process alive.
        /// </remarks>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Application.Current.Shutdown();
        }

    }
}