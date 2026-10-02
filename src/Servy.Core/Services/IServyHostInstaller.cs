using Servy.Core.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Servy.Core.Helpers;

namespace Servy.Core.Services
{
    /// <summary>
    /// Installs, configures, starts and stops the Servy host service (<see cref="Config.AppConfig.ServyHostServiceName"/>),
    /// the Local System service that serves every Servy service its configuration over a named pipe.
    /// </summary>
    public interface IServyHostInstaller
    {
        /// <summary>
        /// Reads what is registered under the Servy host service name. Only a <see cref="ServyHostServiceState.ServyHost"/>
        /// service is ever stopped, started or reconfigured; a service of the same name that runs another program is
        /// left untouched.
        /// </summary>
        /// <returns>The state of the name.</returns>
        ServyHostServiceState GetState();

        /// <summary>
        /// Lists the running Servy services: the ones the wrappers of this build run, and every running service that
        /// depends on the Servy host service, which includes the services the other build (net10 or net48) installed.
        /// </summary>
        /// <param name="serviceHelper">Lists the services this build's wrappers run.</param>
        /// <returns>The service names, without duplicates.</returns>
        List<string> GetRunningServyServices(IServiceHelper serviceHelper);

        /// <summary>
        /// Stops the Servy host service when it is running and waits for it to stop. The Servy services depend on it,
        /// so the caller stops them first.
        /// </summary>
        /// <param name="cancellationToken">A token that stops the wait.</param>
        /// <returns>A task that completes when the service is stopped, or was not running.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service did not stop in time.</exception>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the Servy host service when it is not running and waits for it to run.
        /// </summary>
        /// <param name="cancellationToken">A token that stops the wait.</param>
        /// <returns>A task that completes when the service runs.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service did not start in time.</exception>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs the Servy host service when it is not installed, makes sure its startup type is Automatic and that it
        /// runs <paramref name="hostExePath"/> as Local System, and starts it when it is not running.
        /// </summary>
        /// <remarks>
        /// When the service is installed with another executable (for example after switching between the .NET 10 and the
        /// .NET Framework 4.8 build, or between a Debug and a Release build), every running Servy service and the host are
        /// stopped, the service is pointed at <paramref name="hostExePath"/>, and the host and the stopped services are
        /// started again.
        /// </remarks>
        /// <param name="hostExePath">The full path of the host executable this build extracted.</param>
        /// <param name="serviceHelper">Lists, stops and starts the Servy services when the host has to be moved.</param>
        /// <param name="cancellationToken">A token that stops the operation.</param>
        /// <returns>
        /// The outcome; apart from the exceptions listed below, a failure is returned rather than thrown. It is a failure,
        /// with nothing changed, when the name is taken by a service that does not run the Servy host or whose executable
        /// cannot be read.
        /// </returns>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="hostExePath"/> is <see langword="null"/>, empty or white space.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="serviceHelper"/> is <see langword="null"/>.</exception>
        /// <exception cref="System.OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        Task<OperationResult> EnsureInstalledAndRunningAsync(string hostExePath, IServiceHelper serviceHelper, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds the Servy host service to the dependencies of every given installed service that does not depend on it
        /// yet, keeping the dependencies it has. Services installed by an earlier version of Servy lack it.
        /// </summary>
        /// <param name="serviceNames">The names of the Servy services.</param>
        /// <param name="cancellationToken">A token checked between two services.</param>
        /// <returns>The number of services that were updated. A service that cannot be updated is logged and skipped.</returns>
        Task<int> EnsureServicesDependOnHostAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default);
    }
}
