using Microsoft.Extensions.DependencyInjection;

namespace Servy.Manager.UnitTests
{
    /// <summary>
    /// An isolation scope helper that captures the ambient state of the global static App.Services provider,
    /// configures a test-specific ServiceCollection instance, and guarantees an unconditional restore
    /// along with inner container disposal when discarded. Resolves issues #3762 and #3697.
    /// </summary>
    public sealed class AmbientAppServicesScope : IDisposable
    {
        /// <summary>
        /// Stores the original ambient <see cref="IServiceProvider"/> discovered at scope instantiation.
        /// </summary>
        private readonly IServiceProvider? _originalProvider;

        /// <summary>
        /// The test-specific <see cref="ServiceProvider"/> instance generated for this discrete execution slice.
        /// </summary>
        private readonly ServiceProvider _builtProvider;

        /// <summary>
        /// Captures the ambient <see cref="App.Services"/> provider, installs a test-specific container built by
        /// <paramref name="configure"/>, and unconditionally restores the original provider (disposing the inner
        /// container) when the scope is discarded.
        /// </summary>
        /// <param name="configure">An encapsulation action utilized to seed dependencies and mocks directly into the test container.</param>
        public AmbientAppServicesScope(Action<IServiceCollection> configure)
        {
            _originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            configure(serviceCollection);
            _builtProvider = serviceCollection.BuildServiceProvider();
            App.Services = _builtProvider;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Unconditionally restores the historical ambient service provider snapshot context to prevent leakage.
        /// </summary>
        public void Dispose()
        {
            // Restore before disposing: a consumer that resolves during teardown must not see the disposed container.
            App.Services = _originalProvider;
            _builtProvider.Dispose();
        }
    }
}