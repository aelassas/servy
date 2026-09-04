using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;

namespace Servy.Manager.Views.Controls
{
    /// <summary>
    /// Reusable badge that displays the process id of the running service
    /// together with a button that copies it to the clipboard.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class PidBadgeControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PidBadgeControl"/> class.
        /// </summary>
        public PidBadgeControl()
        {
            InitializeComponent();
        }
    }
}
