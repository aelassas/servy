using System;
using System.ServiceProcess;

namespace Servy.Core.Interfaces
{
    /// <summary>
    /// Defines an abstraction for controlling and monitoring the status of a Windows service.
    /// </summary>
    public interface IServiceControllerWrapper
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
        /// Stops the Windows service.
        /// </summary>
        void Stop();

        /// <summary>
        /// Waits for the Windows service to reach the specified status within the given timeout period.
        /// </summary>
        /// <param name="desiredStatus">The status to wait for.</param>
        /// <param name="timeout">The maximum time to wait for the service to reach the desired status.</param>
        void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout);
    }
}
