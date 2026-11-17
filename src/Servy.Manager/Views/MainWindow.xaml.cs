using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Manager.Config;
using Servy.Manager.Resources;
using Servy.Manager.Utils;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for <see cref="MainWindow"/>.
    /// Represents the main window of the Servy Manager application.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Specifies the active tab scope used to coordinate background activity teardown during tab transitions.
        /// </summary>
        private enum TabScope
        {
            /// <summary>The primary dashboard service management tab.</summary>
            Main,

            /// <summary>The real-time performance monitoring tab.</summary>
            Performance,

            /// <summary>The console output tab.</summary>
            Console,

            /// <summary>The service dependency tree visualization tab.</summary>
            Dependencies,

            /// <summary>The log search tab.</summary>
            Logs
        }

        private readonly IMessageBoxService _messageBoxService;
        private readonly IProcessKiller _processKiller;
        private readonly IAppConfiguration _appConfig;

        /// <summary>
        /// How long, in milliseconds, the window layout must stay unchanged before it is persisted.
        /// Dragging a window edge or a column edge raises a change for every pixel; the delay turns the
        /// whole gesture into a single write.
        /// </summary>
        private const int LayoutSaveDelayMs = 500;

        /// <summary>
        /// Persists the window layout <see cref="LayoutSaveDelayMs"/> after the last change to it.
        /// </summary>
        private readonly DispatcherTimer _layoutSaveTimer;

        /// <summary>
        /// The width the window is declared with, restored by the "Reset window layout" menu item.
        /// </summary>
        private readonly double _defaultWidth;

        /// <summary>
        /// The height the window is declared with, restored by the "Reset window layout" menu item.
        /// </summary>
        private readonly double _defaultHeight;

        /// <summary>
        /// The width each services column is declared with, recorded when the window is loaded and before
        /// the persisted widths are applied. <see langword="null"/> until then.
        /// </summary>
        private IReadOnlyDictionary<DataGridColumn, DataGridLength> _defaultServicesColumnWidths;

        /// <summary>
        /// Whether the persisted placement asks for a maximized window. Applied once the window handle
        /// exists, so the window maximizes on the monitor its restored bounds are on.
        /// </summary>
        private bool _restoreMaximized;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class using constructor injection.
        /// </summary>
        /// <param name="mainViewModel">The primary DataContext for the application.</param>
        /// <param name="messageBoxService">Service for displaying UI dialogs.</param>
        /// <param name="processKiller">Service for terminating child processes on application exit.</param>
        /// <param name="appConfig">Application configuration settings.</param>
        public MainWindow(
            MainViewModel mainViewModel,
            IMessageBoxService messageBoxService,
            IProcessKiller processKiller,
            IAppConfiguration appConfig
            )
        {
            InitializeComponent();

            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            DataContext = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));

            _defaultWidth = Width;
            _defaultHeight = Height;

            // Restore the size and position of the previous session before the window is shown
            RestoreWindowPlacement();

            _layoutSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LayoutSaveDelayMs) };
            _layoutSaveTimer.Tick += LayoutSaveTimer_Tick;
        }

        /// <summary>
        /// Starts persisting the window layout whenever the window is moved, resized, maximized or
        /// restored, or a column of the services or logs table is resized.
        /// </summary>
        /// <remarks>
        /// Called once the window is loaded and the persisted layout has been applied, so that showing
        /// the window and restoring its layout are not themselves persisted as changes.
        /// </remarks>
        private void TrackLayoutChanges()
        {
            SizeChanged += OnLayoutChanged;
            LocationChanged += OnLayoutChanged;
            StateChanged += OnLayoutChanged;

            DataGridColumnWidths.AddWidthChangedHandler(ServicesDataGrid, OnLayoutChanged);

            if (LogsTab.Content is LogsView logsView)
            {
                logsView.ColumnWidthsChanged += OnLayoutChanged;
            }
        }

        /// <summary>
        /// Stops the notifications started by <see cref="TrackLayoutChanges"/> and cancels a pending save.
        /// </summary>
        private void StopTrackingLayoutChanges()
        {
            _layoutSaveTimer.Stop();

            SizeChanged -= OnLayoutChanged;
            LocationChanged -= OnLayoutChanged;
            StateChanged -= OnLayoutChanged;

            DataGridColumnWidths.RemoveWidthChangedHandler(ServicesDataGrid, OnLayoutChanged);

            if (LogsTab.Content is LogsView logsView)
            {
                logsView.ColumnWidthsChanged -= OnLayoutChanged;
            }
        }

        /// <summary>
        /// Handles every change to the window layout by (re)starting the save delay, so the layout is
        /// persisted once the change has settled.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing event data.</param>
        private void OnLayoutChanged(object sender, EventArgs e)
        {
            _layoutSaveTimer.Stop();
            _layoutSaveTimer.Start();
        }

        /// <summary>
        /// Handles the <see cref="DispatcherTimer.Tick"/> event of the save delay: persists the window layout once.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing event data.</param>
        private void LayoutSaveTimer_Tick(object sender, EventArgs e)
        {
            _layoutSaveTimer.Stop();
            SaveWindowLayout();
        }

        /// <summary>
        /// Applies the persisted <see cref="IAppConfiguration.MainWindowPlacement"/> to the window, after
        /// fitting it to the current virtual screen. Leaves the default placement when nothing usable is persisted.
        /// </summary>
        private void RestoreWindowPlacement()
        {
            if (!WindowPlacement.TryParse(_appConfig.MainWindowPlacement, out var saved) || saved == null)
            {
                return;
            }

            var placement = saved.FitTo(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight,
                MinWidth,
                MinHeight);

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = placement.Left;
            Top = placement.Top;
            Width = placement.Width;
            Height = placement.Height;
            _restoreMaximized = placement.IsMaximized;
        }

        /// <summary>
        /// Raises the <see cref="Window.SourceInitialized"/> event and maximizes the window when the
        /// persisted placement asks for it.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> object that contains the event data.</param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (_restoreMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// Reads the current position, size and state of the window in its persisted form.
        /// </summary>
        /// <returns>
        /// The <c>Left,Top,Width,Height,State</c> string to persist, or <see langword="null"/> when the
        /// window is minimized or has no usable bounds, in which case the persisted placement is left as it is.
        /// </returns>
        private string CaptureWindowPlacement()
        {
            // A minimized window cannot tell whether it was maximized before, so it keeps what was persisted last
            if (WindowState == WindowState.Minimized)
            {
                return null;
            }

            // A maximized window reports its restored bounds through RestoreBounds
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (bounds.IsEmpty)
            {
                return null;
            }

            var text = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized).Serialize();

            return WindowPlacement.TryParse(text, out _) ? text : null;
        }

        /// <summary>
        /// Persists the window placement and the column widths of the services and logs tables.
        /// </summary>
        /// <remarks>
        /// Called shortly after every change to the layout and once more when the window closes, so the
        /// layout survives a killed process and is the one a second Manager instance starts with. The
        /// widths of a table that was never shown in this session are left as they are. Failures are
        /// logged and never thrown, so they cannot prevent the window from closing.
        /// </remarks>
        private void SaveWindowLayout()
        {
            try
            {
                if (Application.Current is App app)
                {
                    var servicesColumnWidths = _defaultServicesColumnWidths == null
                        ? null
                        : DataGridColumnWidths.Capture(ServicesDataGrid, app.ServicesColumnWidths);
                    var logsColumnWidths = (LogsTab.Content as LogsView)?.CaptureColumnWidths();

                    app.SaveWindowLayout(CaptureWindowPlacement(), servicesColumnWidths, logsColumnWidths);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to save the window layout: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the <see cref="MenuItem.Click"/> event of the "Reset window layout" menu item.
        /// Restores the default size of the window, centers it on the primary work area, gives the columns
        /// of the services and logs tables their default widths back, and replaces the persisted layout
        /// with that default one.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing event data.</param>
        private void ResetWindowLayout_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
            Width = _defaultWidth;
            Height = _defaultHeight;

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + Math.Max(0, (workArea.Width - _defaultWidth) / 2);
            Top = workArea.Top + Math.Max(0, (workArea.Height - _defaultHeight) / 2);

            if (_defaultServicesColumnWidths != null)
            {
                DataGridColumnWidths.Reset(ServicesDataGrid, _defaultServicesColumnWidths);
            }

            (LogsTab.Content as LogsView)?.ResetColumnWidths();

            if (Application.Current is App app)
            {
                // Forget the persisted layout first, so no width of a hidden column survives the reset
                app.SaveWindowLayout(string.Empty, string.Empty, string.Empty);
            }

            // Then persist the default layout once it has been laid out
            OnLayoutChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the <see cref="Window.Loaded"/> event.
        /// Acts as a synchronous wrapper that fire-and-forgets the asynchronous window initialization.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
            => _ = RunAsync(() => Window_LoadedAsync(sender, e));

        /// <summary>
        /// Performs asynchronous initialization when the window is loaded.
        /// Executes the initial service search via the <see cref="MainViewModel.SearchCommand"/>.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            // Restore column visibilities based on the configured ServicesHiddenColumns
            RestoreColumnVisibilities();

            // Record the declared column widths, then restore the widths of the previous session
            _defaultServicesColumnWidths = DataGridColumnWidths.CaptureDefaults(ServicesDataGrid);
            DataGridColumnWidths.Apply(ServicesDataGrid, _appConfig.ServicesColumnWidths);

            // From here on, persist the layout every time the user changes it
            TrackLayoutChanges();

            // Trigger the global search command to populate the dashboard
            // the moment the shell is ready for interaction.
            if (DataContext is MainViewModel vm)
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Restores column visibilities based on the configured ServicesHiddenColumns string.
        /// </summary>
        private void RestoreColumnVisibilities()
        {
            string displayNameHeader = Strings.Column_DisplayName;
            string rawHidden = _appConfig.ServicesHiddenColumns ?? string.Empty;

            var hiddenSet = new HashSet<string>(
                rawHidden.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);

            bool needsMigration = !_appConfig.IsDisplayNameColumnMigrated;

            // 1. Initial run / v10.5 migration check:
            // Ensure Display Name starts hidden by adding it to hiddenSet before applying column visibilities.
            if (needsMigration)
            {
                hiddenSet.Add(displayNameHeader);
            }

            // 2. Apply visibility across all columns in the DataGrid using the updated hiddenSet
            foreach (var column in ServicesDataGrid.Columns)
            {
                string id = column.Header as string;

                if (!string.IsNullOrEmpty(id))
                {
                    column.Visibility = hiddenSet.Contains(id) ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            // 3. Persist the complete grid state and set migration flag only after all column visibilities have been applied
            if (needsMigration)
            {
                SaveColumnVisibilities();

                if (Application.Current is App app)
                {
                    app.SaveIsDisplayNameColumnMigrated(true);
                }
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
                var parameter = btn.CommandParameter ?? btn.DataContext;
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
            => _ = RunAsync(() => Window_PreviewKeyDownAsync(sender, e));

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
                    await dvm.RefreshCommand.ExecuteAsync(null);
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
        /// Handles the <see cref="Selector.SelectionChanged"/> event of the main <see cref="TabControl"/>.
        /// Acts as a synchronous wrapper that fire-and-forgets the asynchronous tab transition logic.
        /// </summary>
        /// <param name="sender">The <see cref="TabControl"/> that raised the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing event data.</param>
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _ = RunAsync(() => MainTabControl_SelectionChangedAsync(sender, e));

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
                    if (MainTab.IsSelected)
                        await HandleMainTabSelected(vm);
                    else if (PerformanceTab.IsSelected)
                        await HandleMonitoringTabSelected(vm, GetPerformanceVm(), TabScope.Performance);
                    else if (ConsoleTab.IsSelected)
                        await HandleMonitoringTabSelected(vm, GetConsoleVm(), TabScope.Console);
                    else if (DependenciesTab.IsSelected)
                        await HandleMonitoringTabSelected(vm, GetDependenciesVm(), TabScope.Dependencies);
                    else if (LogsTab.IsSelected)
                        await HandleLogsTabSelected(vm, GetLogsVm());
                }
            }
            catch (OperationCanceledException)
            {
                // Expected - a newer tab navigation or search has superseded this one.
            }
            catch (Exception ex)
            {
                Logger.Error("Tab selection failed.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_MainTabControl_SelectionChangedError, UiAppConfig.Caption);
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
            => PerformanceTab.Content is PerformanceView perfView ? perfView.DataContext as PerformanceViewModel : null;

        /// <summary>
        /// Retrieves the current console view model associated with the console tab, if available.
        /// </summary>
        /// <returns>A <see cref="ConsoleViewModel"/> instance representing the data context of the console view; or <see
        /// langword="null"/> if the console view is not present or its data context is not a <see
        /// cref="ConsoleViewModel"/>.</returns>
        private ConsoleViewModel GetConsoleVm()
            => ConsoleTab.Content is ConsoleView consoleView ? consoleView.DataContext as ConsoleViewModel : null;

        /// <summary>
        /// Retrieves the <see cref="DependenciesViewModel"/> instance bound to the <see cref="DependenciesTab"/> content, if available.
        /// </summary>
        /// <returns>
        /// The <see cref="DependenciesViewModel"/> instance if the <see cref="DependenciesTab"/> has content and its DataContext
        /// is a <see cref="DependenciesViewModel"/>; otherwise, <c>null</c>.
        /// </returns>
        private DependenciesViewModel GetDependenciesVm()
            => DependenciesTab.Content is DependenciesView depsView ? depsView.DataContext as DependenciesViewModel : null;

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
        /// Centralized teardown utility that deactivates background monitoring, timers, and searches
        /// across all tabs except for the explicitly specified active scope.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="activeTab">The tab scope identifier that should skip deactivation teardown.</param>
        private void DeactivateAllExcept(MainViewModel vm, TabScope activeTab)
        {
            if (activeTab != TabScope.Main) vm.StopRefreshTimer();
            if (activeTab != TabScope.Performance) GetPerformanceVm()?.StopMonitoring();
            if (activeTab != TabScope.Console) GetConsoleVm()?.StopMonitoring();
            if (activeTab != TabScope.Dependencies) GetDependenciesVm()?.StopMonitoring();
            if (activeTab != TabScope.Logs) GetLogsVm()?.CancelSearch();
        }

        /// <summary>
        /// Handles tasks when the Main tab is selected: stops background monitoring and searches in
        /// every other tab, triggers a service search if none has completed yet, and starts the periodic
        /// refresh timer.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        private async Task HandleMainTabSelected(MainViewModel vm)
        {
            // Stop background workers on all other interfaces, bypassing the Main refresh timer
            DeactivateAllExcept(vm, TabScope.Main);

            if (!vm.HasSearched)
                await vm.SearchCommand.ExecuteAsync(null);

            vm.CreateAndStartTimer();
        }

        /// <summary>
        /// Handles state transitions and resource management when a monitoring-based tab (Performance, Console, or Dependencies) is selected.
        /// </summary>
        /// <remarks>
        /// This method ensures that only the specified monitoring tab is active by stopping or cleaning up background
        /// activities across all other tabs.
        /// </remarks>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="monitoringVm">The target <see cref="MonitoringViewModelBase"/> instance, or <c>null</c> if unavailable.</param>
        /// <param name="scope">The active <see cref="TabScope"/> corresponding to the selected monitoring tab.</param>
        private Task HandleMonitoringTabSelected(MainViewModel vm, MonitoringViewModelBase monitoringVm, TabScope scope)
        {
            DeactivateAllExcept(vm, scope);

            monitoringVm?.StartMonitoring();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles tasks when the Logs tab is selected: stops background monitoring and timers in
        /// every other tab, and triggers a search for logs if none has completed yet.
        /// </summary>
        /// <param name="vm">The main <see cref="MainViewModel"/> instance.</param>
        /// <param name="logsVm">
        /// The <see cref="LogsViewModel"/> instance for the logs tab, or <c>null</c> if unavailable.
        /// </param>
        private async Task HandleLogsTabSelected(MainViewModel vm, LogsViewModel logsVm)
        {
            DeactivateAllExcept(vm, TabScope.Logs);

            if (logsVm != null && !logsVm.HasSearched)
            {
                logsVm.ResetDateWindowToNow();
                await logsVm.SearchCommand.ExecuteAsync(null);
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
        /// Handles the PreviewMouseRightButtonDown event for DataGrid rows.
        /// Clears any existing row selection and selects the right-clicked row exclusively so context menu actions target the intended item.
        /// </summary>
        /// <param name="sender">The source of the event, expected to be a <see cref="DataGridRow"/>.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing event data.</param>
        private void DataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is ServiceRowViewModel)
            {
                ServicesDataGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }
        }

        /// <summary>
        /// Handles the <see cref="DataGridRow.MouseDoubleClick"/> event.
        /// Executes the <see cref="ServiceRowViewModel.ConfigureCommand"/> when a row is double-clicked to open the service configuration.
        /// </summary>
        /// <param name="sender">The source of the event, expected to be a <see cref="DataGridRow"/>.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing event data.</param>
        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is ServiceRowViewModel vm)
            {
                if (vm.ConfigureCommand.CanExecute(null))
                {
                    vm.ConfigureCommand.Execute(null);
                }

                // consume the double-click so it does not reach the row's default handling
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

                source = (source is Visual || source is Visual3D)
                    ? VisualTreeHelper.GetParent(source)
                    : LogicalTreeHelper.GetParent(source);
            }
            return false;
        }

        /// <summary>
        /// Handles the <see cref="ContextMenu.Opened"/> event for the column header context menu.
        /// Synchronizes the checked state of each menu item with the current visibility of its target column.
        /// </summary>
        /// <param name="sender">The source of the event, expected to be a <see cref="ContextMenu"/>.</param>
        /// <param name="e">Event arguments containing event data.</param>
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
        /// Handles the <see cref="MenuItem.Click"/> event for column visibility toggle items.
        /// Shows or hides the associated <see cref="DataGridColumn"/> based on the menu item's checked state.
        /// </summary>
        /// <param name="sender">The source of the event, expected to be a <see cref="MenuItem"/> whose <see cref="FrameworkElement.Tag"/> references a <see cref="DataGridColumn"/>.</param>
        /// <param name="e">Event arguments containing event data.</param>
        private void ToggleColumnVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is DataGridColumn column)
            {
                column.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

                // Save the updated column visibility state to configuration
                SaveColumnVisibilities();
            }
        }

        /// <summary>
        /// Serializes collapsed column identifiers to a comma-separated string and saves them.
        /// </summary>
        private void SaveColumnVisibilities()
        {
            var hiddenColumns = ServicesDataGrid.Columns
                .Where(c => c.Visibility == Visibility.Collapsed)
                .Select(c => c.Header as string)
                .Where(id => !string.IsNullOrEmpty(id));

            string csv = string.Join(",", hiddenColumns);

            if (Application.Current is App app)
            {
                app.SaveServicesHiddenColumns(csv);
            }
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
        /// Secure data and the global logger are disposed later, in <see cref="App.OnExit"/> -
        /// see #2027; shutting the logger down here suppresses disposal warnings.
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
                    vm.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during ViewModel cleanup in OnClosed.", ex);
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
        /// This explicitly calls <see cref="Application.Shutdown()"/> to guarantee
        /// that no background threads, timers, or hidden windows keep the process alive.
        /// The window layout is persisted first, while the window still has its bounds.
        /// </remarks>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // No handler currently sets e.Cancel, so the guard is a placeholder for a future "confirm before closing" feature rather than an active check today.
            if (!e.Cancel)
            {
                // Persist the layout a last time; a save still pending is no longer needed
                StopTrackingLayoutChanges();
                SaveWindowLayout();

                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Executes an asynchronous task in a fire-and-forget manner, typically for UI event handlers.
        /// </summary>
        /// <param name="action">The asynchronous work to execute.</param>
        /// <param name="callerName">The automatically captured member name of the calling UI event handler, used for context-aware error logging.</param>
        /// <remarks>
        /// <para>
        /// This method returns <see cref="Task"/> but is invoked as fire-and-forget via
        /// <c>_ = RunAsync(...)</c> from synchronous UI event handlers. Because the
        /// returned task is discarded, exceptions would otherwise be observed only on
        /// finalizer/GC. The top-level try-catch inside <see cref="UiTaskRunner"/> ensures all failures are recorded by
        /// the <see cref="Logger"/> instead.
        /// </para>
        /// <para>
        /// <b>Note:</b> Use this only for top-level event handlers where the caller does not
        /// need to know when the operation completes.
        /// </para>
        /// </remarks>
        private Task RunAsync(Func<Task> action, [CallerMemberName] string callerName = "")
        {
            return UiTaskRunner.RunAsync(action, $"MainWindow.{callerName}");
        }
    }
}
