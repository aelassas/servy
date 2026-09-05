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
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        /// <exception cref="System.ServiceProcess.TimeoutException">
        /// The service did not reach <paramref name="desiredStatus"/> within <paramref name="timeout"/>.
        /// Note the type: this is <c>System.ServiceProcess.TimeoutException</c>, not
        /// <see cref="System.TimeoutException"/>, so a caller that means to surface a timeout as the
        /// latter - as <see cref="IServiceRestarter.RestartService"/> documents - must catch this type
        /// and translate it.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service could not be found.</exception>
        void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout);

        /// <summary>
        /// Starts the service.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        /// <exception cref="InvalidOperationException">The service could not be found, or the SCM rejected the request.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception">The underlying SCM call failed.</exception>
        void Start();

        /// <summary>
        /// Stops the service.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        /// <exception cref="InvalidOperationException">The service could not be found, or the SCM rejected the request.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception">The underlying SCM call failed.</exception>
        void Stop();

        /// <summary>
        /// Refreshes property values by resetting the properties to their current values from the Windows Service Control Manager (SCM).
        /// </summary>
        /// <remarks>
        /// Call this method before checking the <see cref="Status"/> property to ensure you are reading the most up-to-date state rather than a cached snapshot.
        /// Refreshing only discards the cached values; the SCM is contacted by the next property
        /// read, so an SCM failure surfaces there rather than here.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        void Refresh();
    }
}
