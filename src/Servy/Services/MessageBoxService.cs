using System.Windows;

namespace Servy.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IMessageBoxService"/>.
    /// </summary>
    public class MessageBoxService : IMessageBoxService
    {
        /// <inheritdoc />
        public void ShowInfo(string message, string caption)
            => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);

        /// <inheritdoc />
        public void ShowWarning(string message, string caption)
            => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);

        /// <inheritdoc />
        public void ShowError(string message, string caption)
            => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
