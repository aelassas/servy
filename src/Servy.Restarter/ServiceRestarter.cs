using System;
using System.ServiceProcess;

namespace Servy.Restarter
{
    /// <summary>
    /// Implements service restart functionality using <see cref="IServiceController"/> abstraction.
    /// </summary>
    public class ServiceRestarter : IServiceRestarter
    {
        private const int StopTimeoutSeconds = 120;
        private const int StartTimeoutSeconds = 120;

        private readonly Func<string, IServiceController> _controllerFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="ServiceRestarter"/>.
        /// </summary>
        /// <param name="controllerFactory">
        /// Optional factory method to create <see cref="IServiceController"/> instances.
        /// Defaults to <see cref="ServiceController"/>.
        /// </param>
        public ServiceRestarter(Func<string, IServiceController> controllerFactory = null)
        {
            _controllerFactory = controllerFactory ?? (name => new ServiceController(name));
        }

        /// <inheritdoc />
        public void RestartService(string serviceName)
        {
            using (var controller = _controllerFactory(serviceName))
            {
                if (controller.Status != ServiceControllerStatus.Stopped)
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(StopTimeoutSeconds));
                }

                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(StartTimeoutSeconds));
            }
        }
    }

}
