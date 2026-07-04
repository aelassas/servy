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
        /// Initializes a new instance of the <see cref="AmbientAppServicesScope"/> class, snapshotting the active provider 
        /// state and applying the provided configuration delegate rules against a fresh inner collection container.
        /// </summary>
        /// <param name="configure">An encapsulation action utilized to seed dependencies and mocks directly into the test container.</param>
        public AmbientAppServicesScope(Action<IServiceCollection> configure)
        {
            // Arrange
            _originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();

            configure(serviceCollection);

            _builtProvider = serviceCollection.BuildServiceProvider();

            // Act
            App.Services = _builtProvider;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Unconditionally restores the historical ambient service provider snapshot context to prevent leakage.
        /// </summary>
        public void Dispose()
        {
            // Assert & Restore - Unconditional reversion guarantees no static leaks
            App.Services = _originalProvider;
            _builtProvider.Dispose();
        }
    }
}