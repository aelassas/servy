using System;

namespace Servy.Restarter
{
    /// <summary>
    /// Interface for service restart operations.
    /// </summary>
    public interface IServiceRestarter
    {
        /// <summary>
        /// Restarts the specified Windows service by stopping and starting it.
        /// </summary>
        /// <param name="serviceName">The name of the service to restart.</param>
        /// <param name="timeout">The maximum total time allowed for the entire restart (settle + stop + start).</param>
        /// <exception cref="System.TimeoutException">
        /// Thrown if the service does not reach the target state within <paramref name="timeout"/>.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown, unwrapped, when the SCM refuses a status read or a Stop/Start command with an error that
        /// is neither transitional nor "service gone" (for example, a disabled service, a logon failure or
        /// access denied); only a missing or deleted service is reported as <see cref="RestartResult.ServiceNotFound"/>.
        /// </exception>
        /// <exception cref="System.ComponentModel.Win32Exception">
        /// Thrown, unwrapped, for the same permanent SCM refusals when the underlying call surfaces the native
        /// error directly rather than wrapped in an <see cref="System.InvalidOperationException"/>.
        /// </exception>
        /// <returns>A <see cref="RestartResult"/> indicating the outcome of the operation.</returns>
        RestartResult RestartService(string serviceName, TimeSpan timeout);
    }
}
