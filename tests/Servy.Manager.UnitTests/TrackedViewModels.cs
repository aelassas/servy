using System;
using System.Collections.Concurrent;

namespace Servy.Manager.UnitTests
{
    /// <summary>
    /// Collects the disposable view models a test class creates and disposes them all at fixture teardown.
    /// Mirrors the <see cref="AmbientAppServicesScope"/> precedent: one shared helper in this assembly instead
    /// of the same tracking field and teardown loop copy-pasted into every suite. Resolves issue #5563.
    /// </summary>
    public sealed class TrackedViewModels : IDisposable
    {
        /// <summary>
        /// The view models registered by <see cref="Track{T}"/>, in no particular order.
        /// </summary>
        private readonly ConcurrentBag<IDisposable> _allocated = new ConcurrentBag<IDisposable>();

        /// <summary>
        /// Registers <paramref name="viewModel"/> for teardown and returns it unchanged, so a factory method can
        /// wrap its result in a single expression.
        /// </summary>
        /// <typeparam name="T">The disposable view model type.</typeparam>
        /// <param name="viewModel">The view model to dispose at fixture teardown.</param>
        /// <returns><paramref name="viewModel"/>, unchanged.</returns>
        public T Track<T>(T viewModel) where T : IDisposable
        {
            _allocated.Add(viewModel);
            return viewModel;
        }

        /// <summary>
        /// Explicit test fixture teardown sequence to purge in-flight background CTS contexts safely.
        /// </summary>
        public void Dispose()
        {
            foreach (var viewModel in _allocated)
            {
                try
                {
                    viewModel.Dispose();
                }
                catch
                {
                    // Catch-all block to guarantee adjacent cleanup executions complete safely
                }
            }
        }
    }
}
