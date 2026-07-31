using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;

namespace Servy.Core.Services
{
    /// <summary>
    /// Concrete implementation of the service controller provider that fetches 
    /// real services from the Windows Service Control Manager.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ServiceControllerProvider : IServiceControllerProvider
    {
        private readonly Func<string, IServiceControllerWrapper> _factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceControllerProvider"/> class.
        /// </summary>
        /// <param name="factory">A factory function used to create wrappers for native service controllers.</param>
        public ServiceControllerProvider(Func<string, IServiceControllerWrapper> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Retrieves all Windows services from the local machine and wraps them in testable abstractions.
        /// </summary>
        /// <returns>An array of <see cref="IServiceControllerWrapper"/> instances.</returns>
        public IServiceControllerWrapper[] GetServices()
        {
            // Retrieve native ServiceController instances from the OS,
            // then map them to the custom wrapper using the provided factory.
            return MapAndDisposeServices(sc => _factory(sc.ServiceName));
        }

        /// <summary>
        /// Enumerates services via <see cref="ServiceController.GetServices()"/>, maps each with
        /// <paramref name="projector"/>, and disposes every controller afterwards.
        /// </summary>
        /// <typeparam name="T">The type of the projected result elements.</typeparam>
        /// <param name="projector">A transform function to apply to each <see cref="ServiceController"/>.</param>
        /// <returns>An array of projected elements containing data extracted from the service controllers.</returns>
        internal static T[] MapAndDisposeServices<T>(Func<ServiceController, T> projector)
        {
            var controllers = ServiceController.GetServices();
            try
            {
                // Execute eager transformation to copy system metadata out before handle teardown
                return controllers.Select(projector).ToArray();
            }
            finally
            {
                foreach (var sc in controllers)
                    sc.Dispose();
            }
        }
    }
}