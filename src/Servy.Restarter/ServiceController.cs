using System.ServiceProcess;

namespace Servy.Restarter
{
    /// <summary>
    /// Concrete implementation of <see cref="IServiceController"/> that wraps <see cref="System.ServiceProcess.ServiceController"/>.
    /// </summary>
    public class ServiceController : IServiceController
    {
        private readonly System.ServiceProcess.ServiceController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemServiceControllerAdapter"/> class with the specified service name.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service to control.</param>
        public ServiceController(string serviceName)
        {
            _controller = new System.ServiceProcess.ServiceController(serviceName);
        }

        /// <inheritdoc />
        public ServiceControllerStatus Status => _controller.Status;

        /// <inheritdoc />
        public void Start() => _controller.Start();

        /// <inheritdoc />
        public void Stop() => _controller.Stop();

        /// <inheritdoc />
        public void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout) => _controller.WaitForStatus(desiredStatus, timeout);

        /// <inheritdoc />
        public void Dispose() => _controller.Dispose();
    }
}
