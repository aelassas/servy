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
    public partial class ConsoleView : ServiceSearchUserControl
    {
        /// <summary>
        /// Gets the distinct name of the view used to build explicit contextual log messages.
        /// </summary>
        protected override string ViewName => nameof(ConsoleView);

        /// <summary>
        /// Flag to track if the console history is being loaded for the first time.
        /// </summary>
        private bool _isFirstLoad = true;

        /// <summary>Defines the tolerance below which a vertical scroll change is treated as zero.</summary>
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

            DataContextChanged += ConsoleView_DataContextChanged;

            LogList.SelectionChanged += (_, __) =>
            {
                if (DataContext is ConsoleViewModel vm)
                {
                    vm.SetPaused(LogList.SelectedItems.Count > 0);
                }
            };
        }

        /// <summary>
        /// Handles changes to the <see cref="FrameworkElement.DataContext"/>, detaching event handlers
        /// from the previous <see cref="ConsoleViewModel"/> and attaching them to the new instance.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing <see cref="DependencyPropertyChangedEventArgs.OldValue"/>
        /// and <see cref="DependencyPropertyChangedEventArgs.NewValue"/> used for clean event unsubscription and resubscription.</param>
        private void ConsoleView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ConsoleViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnVmPropertyChanged;
                oldVm.RequestScroll -= OnRequestScroll;
            }

            if (e.NewValue is ConsoleViewModel newVm)
            {
                newVm.RequestScroll += OnRequestScroll;
                newVm.PropertyChanged += OnVmPropertyChanged;
            }
        }

        /// <summary>
        /// Reacts to the view model leaving the paused state: clears the list selection and
        /// snaps the log list to the bottom once the reloaded history has been rendered.
        /// </summary>
        /// <param name="sender">The source of the property change event, expected to be a <see cref="ConsoleViewModel"/> instance.</param>
        /// <param name="e">A <see cref="PropertyChangedEventArgs"/> containing event data such as the property name.</param>
        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs? e)
        {
            if (e?.PropertyName == nameof(ConsoleViewModel.IsPaused) && sender is ConsoleViewModel vm && !vm.IsPaused)
            {
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
            if (Math.Abs(e.VerticalChange) < ScrollTolerance)
                return;

            if (DataContext is ConsoleViewModel vm)
            {
                var sv = e.OriginalSource as ScrollViewer;
                if (sv == null) return;

                bool isAtBottom = sv.VerticalOffset >= (sv.ScrollableHeight - ResumeAtBottomThresholdPx);

                // UI/UX Logic: Resume only if at the bottom AND nothing is selected.
                // Otherwise, stay in "Paused" mode to protect the user's focus.
                if (isAtBottom && LogList.SelectedItems.Count == 0)
                {
                    if (vm.IsPaused)
                    {
                        vm.SetPaused(false);
                    }
                }
                else
                {
                    if (!vm.IsPaused)
                    {
                        vm.SetPaused(true);
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
        /// Performs the asynchronous copy operation of currently selected log lines in the ListBox to the system clipboard.
        /// Retries on transient clipboard COM locks and notifies the user upon failure after exhausted retries.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous copy operation.</returns>
        private async Task CopySelectedLinesAsync()
        {
            var selected = LogList.SelectedItems
                                  .OfType<LogLine>()
                                  .Select(l => l.Text)
                                  .ToList();

            if (!selected.Any())
                return;

            var text = string.Join(Environment.NewLine, selected);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            for (int i = 0; i < Core.Config.AppConfig.ClipboardComMaxRetries; i++)
            {
                try
                {
                    // Since this async execution targets the UI thread context,
                    // this direct execution safely targets the required STA clipboard context.
                    Clipboard.SetText(text);
                    return;
                }
                catch (ExternalException)
                {
                    // COMException (clipboard locked by another process) or any other Win32 clipboard
                    // failure: non-fatal, retry after the configured delay.
                }

                if (i < Core.Config.AppConfig.ClipboardComMaxRetries - 1)
                {
                    // Yield control back to the WPF dispatcher queue thread pump.
                    // This allows UI paint commands and input requests to flow normally while waiting to retry.
                    await Task.Delay(Core.Config.AppConfig.ClipboardComRetryDelayMs);
                }
            }

            Logger.Warn($"Failed to copy {selected.Count} log line(s) to clipboard after {Core.Config.AppConfig.ClipboardComMaxRetries} attempts.");

            if (DataContext is ConsoleViewModel vm)
            {
                await vm.ShowClipboardErrorAsync();
            }
        }

        /// <summary>
        /// Copies the currently selected log lines in the ListBox to the system clipboard.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await CopySelectedLinesAsync();
        }

        /// <summary>
        /// Handles keyboard shortcuts for the log list, specifically Ctrl+C for copying.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private async void LogList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 1. Handle ESC to clear selection
            if (e.Key == Key.Escape)
            {
                LogList.SelectedItems.Clear();
                e.Handled = true;
            }
            // 2. Handle Ctrl+C to copy the selected lines
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await CopySelectedLinesAsync();
            }
        }
    }
}
