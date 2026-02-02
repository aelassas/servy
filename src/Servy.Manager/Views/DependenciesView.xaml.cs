using Servy.Manager.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for DependenciesView.xaml.
    /// Provides the UI for live-monitoring stdout/stderr and searching available services.
    /// </summary>
    public partial class DependenciesView : UserControl
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesView"/> class.
        /// Sets up the data context change listener to wire up ViewModel events and manages selection changes.
        /// </summary>
        public DependenciesView()
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
            if (DataContext is DependenciesViewModel vm && !vm.Services.Any())
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        }

        /// <summary>
        /// Handles the KeyDown event of the SearchTextBox.
        /// Executes the <see cref="DependenciesViewModel.SearchCommand"/> when the Enter key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the key that was pressed.</param>
        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                DataContext is DependenciesViewModel vm &&
                vm.SearchCommand.CanExecute(null))
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        }
    }
}