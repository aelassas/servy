using Servy.Manager.ViewModels;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for DependenciesView.xaml.
    /// Provides the UI for viewing a service's dependency tree and searching available services.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class DependenciesView : ServiceSearchUserControl
    {
        /// <summary>
        /// Gets the distinct name of the view used to build explicit contextual log messages.
        /// </summary>
        protected override string ViewName => nameof(DependenciesView);

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesView"/> class.
        /// Sets up the data context change listener to wire up ViewModel events and manages selection changes.
        /// </summary>
        public DependenciesView()
        {
            InitializeComponent();
        }
    }
}