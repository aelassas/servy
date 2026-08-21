using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Navigation;

namespace Servy.Views
{
    /// <summary>
    /// Interaction logic for <see cref="MainWindow"/>.
    /// Represents the main window of the Servy application.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class MainWindow : Window
    {
        /// <summary>The minimum vertical screen resolution threshold in pixels to trigger expanded window height constraints.</summary>
        private const double FullHdScreenHeightThreshold = 1080;

        /// <summary>The minimum window height enforced for displays meeting or exceeding Full HD vertical resolution.</summary>
        private const double FullHdMinWindowHeight = 790;

        private readonly MainViewModel _mainViewModel;
        private readonly IProcessKiller _processKiller;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class using constructor injection.
        /// </summary>
        /// <param name="mainViewModel">The primary DataContext for the application.</param>
        /// <param name="processKiller">Service responsible for terminating child processes.</param>
        public MainWindow(MainViewModel mainViewModel, IProcessKiller processKiller)
        {
            InitializeComponent();

            AdjustWindowDimensionsForScreenResolution();

            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
            DataContext = _mainViewModel;
        }

        /// <summary>
        /// Adjusts the minimum height of the main window based on the primary screen's vertical resolution.
        /// </summary>
        private void AdjustWindowDimensionsForScreenResolution()
        {
            if (SystemParameters.PrimaryScreenHeight >= FullHdScreenHeightThreshold)
            {
                MinHeight = FullHdMinWindowHeight;
            }
        }

        /// <summary>
        /// Load current service configuration based on windows service name.
        /// </summary>
        /// <param name="serviceName">Service Name.</param>
        /// <returns>A task representing the asynchronous load operation.</returns>
        public async Task LoadServiceConfigurationAsync(string serviceName)
        {
            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                await _mainViewModel.LoadServiceConfigurationAsync(serviceName);
            }
        }

        /// <summary>
        /// Handles navigation requests for hyperlinks by opening the target URI in the user's default web browser.
        /// </summary>
        /// <param name="sender">The source of the event, typically a <see cref="System.Windows.Documents.Hyperlink"/> instance.</param>
        /// <param name="e">A <see cref="RequestNavigateEventArgs"/> containing event data, including the target URI.</param>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
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
        /// </remarks>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
            {
                Application.Current.Shutdown();
            }
        }

    }
}
