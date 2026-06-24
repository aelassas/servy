using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace Servy.UI.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IMessageBoxService"/> using InvokeAsync 
    /// to ensure callers wait for user dismissal.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class MessageBoxService : IMessageBoxService
    {
        private readonly IUiDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBoxService"/> class.
        /// </summary>
        /// <param name="dispatcher">
        /// The <see cref="IUiDispatcher"/> abstraction used to marshal message box calls 
        /// onto the UI thread, ensuring thread-safe interaction with WPF visual components.
        /// </param>
        public MessageBoxService(IUiDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <inheritdoc/>
        public async Task ShowInfoAsync(string? message, string caption)
        {
            if (UiHeadless.IsEnabled)
            {
                Console.WriteLine($"[HEADLESS INFO] {caption}: {message}");
                return;
            }

            // Use InvokeAsync to ensure the task doesn't complete until the dialog is closed.
            await _dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        /// <inheritdoc/>
        public async Task ShowWarningAsync(string? message, string caption)
        {
            if (UiHeadless.IsEnabled)
            {
                Console.WriteLine($"[HEADLESS WARNING] {caption}: {message}");
                return;
            }

            await _dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        /// <inheritdoc/>
        public async Task ShowErrorAsync(string? message, string caption)
        {
            if (UiHeadless.IsEnabled)
            {
                Console.WriteLine($"[HEADLESS ERROR] {caption}: {message}");
                return;
            }

            await _dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        /// <inheritdoc/>
        public async Task<bool> ShowConfirmAsync(string? message, string caption)
        {
            if (UiHeadless.IsEnabled)
            {
                Console.WriteLine($"[HEADLESS CONFIRM] {caption}: {message} -> Auto-answering 'Yes'.");
                return true; // Default to 'Yes' to allow happy-path CI flows
            }

            return await _dispatcher.InvokeAsync(() =>
            {
                return MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
                       == MessageBoxResult.Yes;
            });
        }
    }
}