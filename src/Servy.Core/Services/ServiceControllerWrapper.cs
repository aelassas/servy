using Servy.Core.Interfaces;
using System.ServiceProcess;

namespace Servy.Core.Services
{
    /// <inheritdoc cref="IServiceControllerWrapper"/>
    public class ServiceControllerWrapper : IServiceControllerWrapper
    {
        private readonly ServiceController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceControllerWrapper"/> class with the specified service name.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service to control.</param>
        public ServiceControllerWrapper(string serviceName)
        {
            _controller = new ServiceController(serviceName);
        }

        /// <inheritdoc/>
        public ServiceControllerStatus Status => _controller.Status;

        /// <inheritdoc/>
        public void Start() => _controller.Start();

        /// <inheritdoc/>
        public void Stop() => _controller.Stop();

        /// <inheritdoc/>
        public void Refresh() => _controller.Refresh();

        /// <inheritdoc/>
        public void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout) =>
            _controller.WaitForStatus(desiredStatus, timeout);

        /// <inheritdoc/>
        public void Dispose() => _controller.Dispose();
    }
}
