using System.Windows.Threading;

namespace Servy.UI.Services
{
    /// <summary>
    /// Provides a WPF-specific implementation of <see cref="IUiDispatcher"/> 
    /// using the <see cref="Dispatcher"/> to manage background-priority yielding.
    /// </summary>
    public class WpfUiDispatcher : IUiDispatcher
    {
        /// <inheritdoc/>
        /// <remarks>
        /// Invokes an empty action at <see cref="DispatcherPriority.Background"/> 
        /// to ensure the UI has finished its current render cycle.
        /// </remarks>
        public async Task YieldAsync()
        {
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }
}