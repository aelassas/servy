using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.ViewModels;
using Servy.UI.Helpers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for ConsoleView.xaml.
    /// Provides the UI for live-monitoring stdout/stderr and searching available services.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class ConsoleView : UserControl
    {
        /// <summary>
        /// Flag to track if the console history is being loaded for the first time.
        /// </summary>
        private bool _isFirstLoad = true;

        /// <summary>
        /// The view model currently bound to the console view, tracked so its events can be unhooked on DataContext change.
        /// </summary>
        private ConsoleViewModel? _currentViewModel;

        /// <summary>
        /// Define a small tolerance for floating point comparisons 
        /// </summary>
        private const double ScrollTolerance = 0.001;

        /// <summary>Defines the pixel threshold from the bottom that forces an immediate resumption of tailing.</summary>
        private const double ResumeAtBottomThresholdPx = 10;

        /// <summary>Defines the pixel window from the bottom where auto-scrolling remains active.</summary>
        private const double AutoFollowBandPx = 50;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleView"/> class.
        /// Sets up the data context change listener to wire up ViewModel events and manages selection changes.
        /// </summary>
        public ConsoleView()
        {
            InitializeComponent();

            DataContextChanged += (s, e) =>
            {
                if (_currentViewModel != null)
                {
                    _currentViewModel.PropertyChanged -= OnVmPropertyChanged;
                    _currentViewModel.RequestScroll -= OnRequestScroll;
                }

                if (DataContext is ConsoleViewModel vm)
                {
                    _currentViewModel = vm;
                    vm.RequestScroll += OnRequestScroll;
                    vm.PropertyChanged += OnVmPropertyChanged;
                }
            };

            LogList.SelectionChanged += (_, __) =>
            {
                if (DataContext is ConsoleViewModel vm)
                {
                    vm.SetSelectionActive(LogList.SelectedItems.Count > 0);
                }
            };

            Unloaded += (s, e) => (DataContext as ConsoleViewModel)?.Dispose();
        }

        /// <summary>
        /// Since we now use "Option A" (no background updates while paused),
        /// we only need to handle the scroll snap when resuming.
        /// </summary>
        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs? e)
        {
            if (e?.PropertyName == nameof(ConsoleViewModel.IsPaused) && sender is ConsoleViewModel vm && !vm.IsPaused)
            {
                // Clear UI selection
                LogList.SelectedItems.Clear();

                // Snap to the bottom of the fresh history loaded by LoadLogsAsync
                // We use a slight delay to ensure the ListView has rendered the new items
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    OnRequestScroll(true);
                }), DispatcherPriority.Render);
            }
        }

        /// <summary>
        /// Monitors user-initiated scrolling within the log list.
        /// Pauses the console if the user scrolls up or has an active selection.
        /// Automatically resumes tailing only if the user scrolls to the bottom with no items selected.
        /// </summary>
        private void LogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Check if the change is effectively zero using the tolerance range
            if (Math.Abs(e.VerticalChange) < ScrollTolerance)
                return;

            if (DataContext is ConsoleViewModel vm)
            {
                var sv = e.OriginalSource as ScrollViewer;
                if (sv == null) return;

                // Check if we are at the bottom
                bool isAtBottom = sv.VerticalOffset >= (sv.ScrollableHeight - ResumeAtBottomThresholdPx);

                // UI/UX Logic: Resume only if at the bottom AND nothing is selected.
                // Otherwise, stay in "Paused" mode to protect the user's focus.
                if (isAtBottom && LogList.SelectedItems.Count == 0)
                {
                    if (vm.IsPaused)
                    {
                        vm.SetSelectionActive(false);
                    }
                }
                else
                {
                    if (!vm.IsPaused)
                    {
                        vm.SetSelectionActive(true);
                    }
                }
            }
        }

        /// <summary>
        /// Handles requests from the ViewModel to adjust the scroll position of the log list.
        /// </summary>
        /// <param name="scrollToEnd">If true, forces a scroll to the bottom regardless of current position.</param>
        private void OnRequestScroll(bool scrollToEnd = false)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var sv = Helper.GetVisualChild<ScrollViewer>(LogList);
                if (sv == null) return;

                if (_isFirstLoad || scrollToEnd)
                {
                    sv.ScrollToEnd();
                    _isFirstLoad = false;
                }
                else if (DataContext is ConsoleViewModel vm && sv.VerticalOffset >= (sv.ScrollableHeight - AutoFollowBandPx) && !vm.IsPaused)
                {
                    // Only auto-scroll if the user hasn't paused (by selecting or scrolling up)
                    sv.ScrollToEnd();
                }
            }), DispatcherPriority.DataBind);
        }

        /// <summary>
        /// Copies the currently selected log lines in the ListBox to the system clipboard.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private async void CopyMenuItem_Click(object? sender, RoutedEventArgs? e)
        {
            var selected = LogList.SelectedItems
                                  .OfType<LogLine>()
                                  .Select(l => l.Text)
                                  .ToList();

            if (!selected.Any())
                return;

            var text = string.Join(Environment.NewLine, selected);

            for (int i = 0; i < Core.Config.AppConfig.ClipboardComMaxRetries; i++)
            {
                try
                {
                    // Since this async void event handler initiates on the UI thread,
                    // this direct execution safely targets the required STA clipboard context.
                    Clipboard.SetText(text);
                    return;
                }
                catch (COMException) { /* clipboard locked */ }
                catch (ExternalException) { /* generic Win32 failure */ }

                if (i < Core.Config.AppConfig.ClipboardComMaxRetries - 1)
                {
                    // Yield control back to the WPF dispatcher queue thread pump. 
                    // This allows UI paint commands and input requests to flow normally while waiting to retry.
                    await Task.Delay(Core.Config.AppConfig.ClipboardComRetryDelayMs);
                }
            }

            Logger.Warn($"Failed to copy {selected.Count} log line(s) to clipboard after {Core.Config.AppConfig.ClipboardComMaxRetries} attempts.");
        }

        /// <summary>
        /// Handles keyboard shortcuts for the log list, specifically Ctrl+C for copying.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void LogList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 1. Handle ESC to clear selection
            if (e.Key == Key.Escape)
            {
                LogList.SelectedItems.Clear();
                e.Handled = true;
            }
            // 2. Existing Ctrl+C handler
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopyMenuItem_Click(null, null);
                e.Handled = true;
            }
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
                if (DataContext is ConsoleViewModel vm && !vm.Services.Any())
                {
                    await vm.SearchCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to perform initial service search in ConsoleView.", ex);
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
        /// Executes the <see cref="ConsoleViewModel.SearchCommand"/> specifically when the Enter key is pressed
        /// and the command is in a valid state to execute.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task SearchTextBox_KeyDownAsync(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                DataContext is ConsoleViewModel vm &&
                vm.SearchCommand.CanExecute(null))
            {
                try
                {
                    // Trigger the search command asynchronously to keep the UI responsive
                    // while querying the service database.
                    await vm.SearchCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    Logger.Error("Search failed in ConsoleView via Enter key.", ex);
                }
            }
        }

    }
}