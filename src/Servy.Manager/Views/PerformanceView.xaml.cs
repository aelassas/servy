using Servy.Manager.ViewModels;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Manager.Views
{
    /// <summary>
    /// Interaction logic for PerformanceView.xaml.
    /// Provides the UI for monitoring service performance metrics and searching available services.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class PerformanceView : ServiceSearchUserControl
    {
        /// <summary>
        /// Gets the distinct name of the view used to build explicit contextual log messages.
        /// </summary>
        protected override string ViewName => nameof(PerformanceView);

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceView"/> class.
        /// </summary>
        public PerformanceView()
        {
            InitializeComponent();

            Unloaded += (s, e) => (DataContext as PerformanceViewModel)?.Dispose();
        }
    }
}