using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace Servy.Views
{
    /// <summary>
    /// Interaction logic for <see cref="MainWindow"/>.
    /// Represents the main window of the Servy application.
    /// </summary>
    public partial class MainWindow : Window
    {
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

            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
            DataContext = _mainViewModel;
        }

        /// <summary>
        /// Load current service configuration based on windows service name.
        /// </summary>
        /// <param name="serviceName">Service Name.</param>
        /// <returns>A task representing the asynchronous load operation.</returns>
        public async Task LoadServiceConfiguration(string serviceName)
        {
            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                await _mainViewModel.LoadServiceConfiguration(serviceName);
            }
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

            Logger.Shutdown();

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
