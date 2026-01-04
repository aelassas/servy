using Servy.Manager.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for PerformanceView.xaml.
    /// Provides the UI for monitoring service performance metrics and searching available services.
    /// </summary>
    public partial class PerformanceView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceView"/> class.
        /// </summary>
        public PerformanceView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the Loaded event of the UserControl.
        /// Automatically triggers an initial service search if the service list is currently empty.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is PerformanceViewModel vm && !vm.Services.Any())
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Handles the KeyDown event of the SearchTextBox.
        /// Executes the <see cref="PerformanceViewModel.SearchCommand"/> when the Enter key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                DataContext is PerformanceViewModel vm &&
                vm.SearchCommand.CanExecute(null))
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        }
    }
}