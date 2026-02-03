using Servy.Manager.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Servy.Manager.Views.Controls
{
    /// <summary>
    /// Reusable control that displays a searchable list of Windows services.
    /// Designed to be shared across multiple views such as dependencies,
    /// performance monitoring, and console output.
    /// </summary>
    public partial class ServiceListControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceListControl"/> class.
        /// </summary>
        public ServiceListControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the collection of services displayed in the list.
        /// </summary>
        public ObservableCollection<ServiceItemBase> Services
        {
            get => (ObservableCollection<ServiceItemBase>)GetValue(ServicesProperty);
            set => SetValue(ServicesProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="Services"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ServicesProperty =
            DependencyProperty.Register(
                nameof(Services),
                typeof(ObservableCollection<ServiceItemBase>),
                typeof(ServiceListControl));

        /// <summary>
        /// Gets or sets the currently selected service in the list.
        /// This property supports two-way binding by default.
        /// </summary>
        public ServiceItemBase SelectedService
        {
            get => (ServiceItemBase)GetValue(SelectedServiceProperty);
            set => SetValue(SelectedServiceProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="SelectedService"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedServiceProperty =
            DependencyProperty.Register(
                nameof(SelectedService),
                typeof(ServiceItemBase),
                typeof(ServiceListControl),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Gets or sets the search text used to filter the services list.
        /// </summary>
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="SearchText"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(
                nameof(SearchText),
                typeof(string),
                typeof(ServiceListControl),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Gets or sets the text displayed on the search button.
        /// This typically reflects the current search state (for example, "Search" or "Searching...").
        /// </summary>
        public string SearchButtonText
        {
            get => (string)GetValue(SearchButtonTextProperty);
            set => SetValue(SearchButtonTextProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="SearchButtonText"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SearchButtonTextProperty =
            DependencyProperty.Register(
                nameof(SearchButtonText),
                typeof(string),
                typeof(ServiceListControl));

        /// <summary>
        /// Gets or sets a value indicating whether the control is currently busy.
        /// Used to disable user interactions while background operations are running.
        /// </summary>
        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="IsBusy"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(
                nameof(IsBusy),
                typeof(bool),
                typeof(ServiceListControl));

        /// <summary>
        /// Gets or sets the command executed when the user initiates a service search.
        /// </summary>
        public ICommand SearchCommand
        {
            get => (ICommand)GetValue(SearchCommandProperty);
            set => SetValue(SearchCommandProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="SearchCommand"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SearchCommandProperty =
            DependencyProperty.Register(
                nameof(SearchCommand),
                typeof(ICommand),
                typeof(ServiceListControl));

        /// <summary>
        /// Handles key presses on the search input box.
        /// Triggers the search command when the Enter key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Key event data.</param>
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            // Prevent the TextBox from beeping or propagating the key event
            e.Handled = true;

            // Execute the bound search command if available
            if (SearchCommand?.CanExecute(null) == true)
            {
                SearchCommand.Execute(null);
            }
        }
    }
}
