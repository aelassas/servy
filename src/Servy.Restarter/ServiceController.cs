using System.ServiceProcess;

namespace Servy.Restarter
{
    /// <summary>
    /// Concrete implementation of <see cref="IServiceController"/> that wraps <see cref="System.ServiceProcess.ServiceController"/>.
    /// </summary>
    public class ServiceController : IServiceController
    {
        private readonly ISystemServiceController _controller;

        public ServiceController(ISystemServiceController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <inheritdoc />
        public ServiceControllerStatus Status => _controller.Status;

        /// <inheritdoc />
        public void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout) =>
            _controller.WaitForStatus(desiredStatus, timeout);

        /// <inheritdoc />
        public void Start() => _controller.Start();

        /// <inheritdoc />
        public void Dispose() => _controller.Dispose();
    }
}
