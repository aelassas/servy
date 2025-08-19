using System;
using System.Threading.Tasks;
using System.Windows;

namespace Servy.UI.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IMessageBoxService"/>.
    /// </summary>
    public class MessageBoxService : IMessageBoxService
    {
        ///<inheritdoc/>
        public async Task ShowInfoAsync(string message, string caption)
        {
            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            }));
        }

        ///<inheritdoc/>
        public async Task ShowWarningAsync(string message, string caption)
        {
            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }

        ///<inheritdoc/>
        public async Task ShowErrorAsync(string message, string caption)
        {
            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }

        ///<inheritdoc/>
        public async Task<bool> ShowConfirmAsync(string message, string caption)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                return MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
                       == MessageBoxResult.Yes;
            });
        }
    }
}
