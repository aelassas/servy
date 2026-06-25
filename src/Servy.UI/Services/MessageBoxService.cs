using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
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

        /// <summary>
        /// Centralized unmanaged UI helper to abstract headless checks and handle cross-thread marshaling boundaries cleanly.
        /// </summary>
        private Task ShowAsync(string message, string caption, MessageBoxImage image, string headlessTag)
        {
            if (UiHeadless.IsEnabled)
            {
                Console.WriteLine($"[HEADLESS {headlessTag}] {caption}: {message}");
                return Task.CompletedTask;
            }

            // Use InvokeAsync to ensure the task doesn't complete until the dialog is closed.
            return _dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, image);
            });
        }

        /// <inheritdoc/>
        public Task ShowInfoAsync(string message, string caption)
        {
            return ShowAsync(message, caption, MessageBoxImage.Information, "INFO");
        }

        /// <inheritdoc/>
        public Task ShowWarningAsync(string message, string caption)
        {
            return ShowAsync(message, caption, MessageBoxImage.Warning, "WARNING");
        }

        /// <inheritdoc/>
        public Task ShowErrorAsync(string message, string caption)
        {
            return ShowAsync(message, caption, MessageBoxImage.Error, "ERROR");
        }

        /// <inheritdoc/>
        public async Task<bool> ShowConfirmAsync(string message, string caption)
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