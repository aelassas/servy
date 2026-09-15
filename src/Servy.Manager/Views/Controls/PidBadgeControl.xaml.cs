using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        /// Identifies the <see cref="Pid"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty PidProperty =
            DependencyProperty.Register(
                nameof(Pid),
                typeof(string),
                typeof(PidBadgeControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Identifies the <see cref="CopyPidCommand"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty CopyPidCommandProperty =
            DependencyProperty.Register(
                nameof(CopyPidCommand),
                typeof(ICommand),
                typeof(PidBadgeControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the Process ID text displayed within the badge.
        /// </summary>
        public string Pid
        {
            get => (string)GetValue(PidProperty);
            set => SetValue(PidProperty, value);
        }

        /// <summary>
        /// Gets or sets the command executed when the copy PID button is clicked.
        /// </summary>
        public ICommand CopyPidCommand
        {
            get => (ICommand)GetValue(CopyPidCommandProperty);
            set => SetValue(CopyPidCommandProperty, value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PidBadgeControl"/> class.
        /// </summary>
        public PidBadgeControl()
        {
            InitializeComponent();
        }
    }
}
