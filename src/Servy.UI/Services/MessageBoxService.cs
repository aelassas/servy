using System.Windows;

namespace Servy.UI.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IMessageBoxService"/> using InvokeAsync 
    /// to ensure callers wait for user dismissal.
    /// </summary>
    public class MessageBoxService : IMessageBoxService
    {
        /// <summary>
        /// A hook for integration tests to prevent blocking modal dialogs in headless CI environments.
        /// </summary>
        public static bool IsHeadlessMode { get; set; }

        ///<inheritdoc/>
        public async Task ShowInfoAsync(string? message, string caption)
        {
            if (IsHeadlessMode)
            {
                Console.WriteLine($"[HEADLESS INFO] {caption}: {message}");
                return;
            }

            // Use InvokeAsync to ensure the task doesn't complete until the dialog is closed.
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        ///<inheritdoc/>
        public async Task ShowWarningAsync(string? message, string caption)
        {
            if (IsHeadlessMode)
            {
                Console.WriteLine($"[HEADLESS WARNING] {caption}: {message}");
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        ///<inheritdoc/>
        public async Task ShowErrorAsync(string? message, string caption)
        {
            if (IsHeadlessMode)
            {
                Console.WriteLine($"[HEADLESS ERROR] {caption}: {message}");
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        ///<inheritdoc/>
        public async Task<bool> ShowConfirmAsync(string? message, string caption)
        {
            if (IsHeadlessMode)
            {
                Console.WriteLine($"[HEADLESS CONFIRM] {caption}: {message} -> Auto-answering 'Yes'.");
                return true; // Default to 'Yes' to allow happy-path CI flows
            }

            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                return MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
                       == MessageBoxResult.Yes;
            });
        }
    }
}