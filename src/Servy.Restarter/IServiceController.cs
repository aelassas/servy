using System;
using System.ServiceProcess;

namespace Servy.Restarter
{
    /// <summary>
    /// Interface to abstract operations of a Windows Service Controller.
    /// Allows easier unit testing by wrapping <see cref="ServiceController"/>.
    /// </summary>
    public interface IServiceController : IDisposable
    {
        /// <summary>
        /// Gets the current status of the service.
        /// </summary>
        ServiceControllerStatus Status { get; }

        /// <summary>
        /// Waits for the service to reach the specified status within a timeout.
        /// </summary>
        /// <param name="desiredStatus">The status to wait for.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout);

        /// <summary>
        /// Starts the service.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the service.
        /// </summary>
        void Stop();
    }
}
