using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
            _factory = factory;
        }

        /// <summary>
        /// Retrieves all Windows services from the local machine and wraps them in testable abstractions.
        /// </summary>
        /// <returns>An array of <see cref="IServiceControllerWrapper"/> instances.</returns>
        public IServiceControllerWrapper[] GetServices()
        {
            // Retrieve native ServiceController instances from the OS,
            // then map them to your custom wrapper using the provided factory.
            var controllers = ServiceController.GetServices();
            try
            {
                return controllers
                    .Select(sc => _factory(sc.ServiceName))
                    .ToArray();
            }
            finally
            {
                foreach (var sc in controllers)
                    sc.Dispose();
            }
        }
    }
}