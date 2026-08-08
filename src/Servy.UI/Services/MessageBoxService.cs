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

        /// <summary>
        /// Shows a message box on the UI thread, or writes to the console when headless mode is enabled.
        /// </summary>
        /// <param name="message">The body text to display.</param>
        /// <param name="caption">The title header for the dialog.</param>
        /// <param name="image">The dialog icon classification.</param>
        /// <param name="buttons">The button set presented on the dialog.</param>
        /// <param name="headlessTag">The text label prefix printed in console output during headless execution.</param>
        /// <param name="headlessResult">The default boolean return value provided during headless execution.</param>
        /// <returns>A task returning <c>true</c> if the user confirmed (or auto-answered 'Yes' in headless mode); otherwise, <c>false</c>.</returns>
        private Task<bool> ShowCoreAsync(
            string? message,
            string caption,
            MessageBoxImage image,
            MessageBoxButton buttons,
            string headlessTag,
            bool headlessResult)
        {
            if (UiHeadless.IsEnabled)
            {
                string suffix = buttons == MessageBoxButton.YesNo ? " -> Auto-answering 'Yes'." : string.Empty;
                Console.WriteLine($"[HEADLESS {headlessTag}] {caption}: {message}{suffix}");
                return Task.FromResult(headlessResult);
            }

            // Use InvokeAsync to ensure the task doesn't complete until the dialog is closed.
            return _dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(message, caption, buttons, image);
                return buttons != MessageBoxButton.YesNo || result == MessageBoxResult.Yes;
            });
        }

        /// <inheritdoc />
        public Task ShowInfoAsync(string? message, string caption)
        {
            return ShowCoreAsync(message, caption, MessageBoxImage.Information, MessageBoxButton.OK, "INFO", true);
        }

        /// <inheritdoc />
        public Task ShowWarningAsync(string? message, string caption)
        {
            return ShowCoreAsync(message, caption, MessageBoxImage.Warning, MessageBoxButton.OK, "WARNING", true);
        }

        /// <inheritdoc />
        public Task ShowErrorAsync(string? message, string caption)
        {
            return ShowCoreAsync(message, caption, MessageBoxImage.Error, MessageBoxButton.OK, "ERROR", true);
        }

        /// <inheritdoc />
        public Task<bool> ShowConfirmAsync(string? message, string caption)
        {
            return ShowCoreAsync(message, caption, MessageBoxImage.Question, MessageBoxButton.YesNo, "CONFIRM", true);
        }
    }
}
