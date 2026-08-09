using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace Servy.UI.Views
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class SplashWindow : Window
    {
        /// <summary>
        /// Identifies the <see cref="SplashText"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SplashTextProperty =
            DependencyProperty.Register(
                nameof(SplashText),
                typeof(string),
                typeof(SplashWindow),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets the text displayed in the splash window.
        /// </summary>
        public string SplashText
        {
            get => (string)GetValue(SplashTextProperty);
            set => SetValue(SplashTextProperty, value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SplashWindow"/> class.
        /// </summary>
        public SplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SplashWindow"/> class with localized message text.
        /// </summary>
        /// <param name="splashText">The text displayed in the splash window.</param>
        public SplashWindow(string splashText) : this()
        {
            SplashText = splashText;
        }
    }
}
