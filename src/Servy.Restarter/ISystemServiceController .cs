using System;
using System.ServiceProcess;

namespace Servy.Restarter
{
    /// <summary>
    /// Defines methods for controlling and monitoring a Windows service.
    /// </summary>
    public interface ISystemServiceController : IDisposable
    {
        /// <summary>
        /// Gets the current status of the Windows service.
        /// </summary>
        ServiceControllerStatus Status { get; }

        /// <summary>
        /// Starts the Windows service.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the Windows service.
        /// </summary>
        void Stop();

        /// <summary>
        /// Waits for the service to reach the specified status within the given timeout.
        /// </summary>
        /// <param name="desiredStatus">The desired final status to wait for.</param>
        /// <param name="timeout">The maximum time to wait for the status change.</param>
        void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout);
    }
}
