using System.Windows;
using System.Windows.Threading;

namespace Servy.UI.Services
{
    /// <summary>
    /// Provides a WPF-specific implementation of <see cref="IUiDispatcher"/> 
    /// using the <see cref="Dispatcher"/> to manage thread synchronization and yielding.
    /// </summary>
    public class WpfUiDispatcher : IUiDispatcher
    {
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="WpfUiDispatcher"/> class.
        /// </summary>
        public WpfUiDispatcher()
        {
            // Construct on the UI thread (typical at App startup) - capture that dispatcher.
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            await _dispatcher.InvokeAsync(action);
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(Action action, DispatcherPriority priority)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            // Check if the dispatcher is available (e.g., during shutdown)
            if (_dispatcher == null) return;

            await _dispatcher.InvokeAsync(action, priority);
        }

        /// <inheritdoc/>
        public async Task<T> InvokeAsync<T>(Func<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            return await _dispatcher.InvokeAsync(callback);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Uses <see cref="DispatcherPriority.Background"/> to ensure that the dispatcher 
        /// has finished processing layout and render passes before the calling task resumes.
        /// </remarks>
        public async Task YieldAsync()
        {
            // Dispatcher.Yield is the specialized WPF equivalent to Task.Yield
            // specifically designed to work with DispatcherPriority.
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }
}