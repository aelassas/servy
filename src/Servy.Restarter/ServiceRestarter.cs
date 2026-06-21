using Servy.Core.Config;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;

namespace Servy.Restarter
{
    /// <summary>
    /// Implements service restart functionality using <see cref="IServiceController"/> abstraction.
    /// </summary>
    public class ServiceRestarter : IServiceRestarter
    {
        private readonly Func<string, IServiceController> _controllerFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="ServiceRestarter"/>.
        /// </summary>
        public ServiceRestarter(Func<string, IServiceController>? controllerFactory = null)
        {
            _controllerFactory = controllerFactory ?? (name => new ServiceController(name));
        }

        /// <inheritdoc />
        public void RestartService(string serviceName, TimeSpan timeout)
        {
            using (var controller = _controllerFactory(serviceName))
            {
                var stopwatch = Stopwatch.StartNew();

                // 1. Settle: If Pending, wait for it to reach a stable state first
                while (true)
                {
                    ServiceControllerStatus current;
                    try
                    {
                        current = controller.Status;
                        if (!IsPendingState(current)) break;
                    }
                    catch (InvalidOperationException)
                    {
                        // ROBUSTNESS: Service was uninstalled or marked for deletion mid-flight. 
                        // There is no longer an active handle to manage or restart; exit cleanly.
                        return;
                    }

                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        throw new System.TimeoutException($"Service '{serviceName}' stuck in {current} state.");

                    var sleepFor = (int)Math.Min(AppConfig.ServiceRestarterPollIntervalMs, remaining.TotalMilliseconds);
                    if (sleepFor > 0) Thread.Sleep(sleepFor);

                    try
                    {
                        controller.Refresh();
                    }
                    catch (InvalidOperationException)
                    {
                        // ROBUSTNESS: Handle disappearance during the refresh cycle.
                        return;
                    }
                    catch (Win32Exception)
                    {
                        // ROBUSTNESS: Handle native SCM teardowns (e.g. ERROR_SERVICE_DOES_NOT_EXIST).
                        return;
                    }
                }

                // 2. Stop phase
                if (controller.Status != ServiceControllerStatus.Stopped)
                {
                    try
                    {
                        controller.Stop();
                        var stopRemaining = timeout - stopwatch.Elapsed;
                        if (stopRemaining <= TimeSpan.Zero)
                            throw new System.TimeoutException($"No time remaining to stop service '{serviceName}'.");

                        try
                        {
                            controller.WaitForStatus(ServiceControllerStatus.Stopped, stopRemaining);
                        }
                        catch (System.ServiceProcess.TimeoutException ex)
                        {
                            throw new System.TimeoutException(
                                $"Service '{serviceName}' did not reach Stopped within {stopRemaining}.", ex);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Fallback: If it transitioned to Pending between our check and the call
                        HandleTransitionalError(serviceName, controller, ServiceControllerStatus.Stopped, timeout - stopwatch.Elapsed);
                    }
                }

                // 3. Start phase
                try
                {
                    controller.Refresh();
                    if (controller.Status == ServiceControllerStatus.Running)
                        return; // already running, nothing to do
                }
                catch (InvalidOperationException) { return; }
                catch (Win32Exception) { return; }

                try
                {
                    controller.Start();
                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        throw new System.TimeoutException(
                            $"Timeout expired while waiting for service '{serviceName}' to reach Running. " +
                            "The Start command was issued; the service may still complete the transition.");

                    try
                    {
                        controller.WaitForStatus(ServiceControllerStatus.Running, remaining);
                    }
                    catch (System.ServiceProcess.TimeoutException ex)
                    {
                        throw new System.TimeoutException(
                            $"Service '{serviceName}' did not reach Running within {remaining}.", ex);
                    }

                }
                catch (InvalidOperationException)
                {
                    // Fallback: If it transitioned to Pending between our check and the call
                    HandleTransitionalError(serviceName, controller, ServiceControllerStatus.Running, timeout - stopwatch.Elapsed);
                }
            }
        }

        /// <summary>
        /// Determines whether the specified service status represents a transitional (pending) state.
        /// </summary>
        /// <param name="status">The <see cref="ServiceControllerStatus"/> to evaluate.</param>
        /// <returns>
        /// <c>true</c> if the service is currently in a "Pending" state (Start, Stop, Continue, or Pause); 
        /// otherwise, <c>false</c>.
        /// </returns>
        private bool IsPendingState(ServiceControllerStatus status)
        {
            return status == ServiceControllerStatus.StartPending ||
                   status == ServiceControllerStatus.StopPending ||
                   status == ServiceControllerStatus.ContinuePending ||
                   status == ServiceControllerStatus.PausePending;
        }

        /// <summary>
        /// Handles race conditions where a service enters a transitional state between 
        /// a status check and a command execution.
        /// </summary>
        /// <param name="serviceName">Windows Service name.</param>
        /// <param name="controller">The <see cref="IServiceController"/> instance to manage.</param>
        /// <param name="targetStatus">The desired <see cref="ServiceControllerStatus"/> (typically Running or Stopped).</param>
        /// <param name="timeout">The maximum <see cref="TimeSpan"/> allowed for the entire recovery operation.</param>
        /// <exception cref="System.TimeoutException">
        /// Thrown if the service fails to reach the <paramref name="targetStatus"/> 
        /// before the <paramref name="timeout"/> expires.
        /// </exception>
        /// <remarks>
        /// This method uses an interrogation loop with <see cref="ServiceController.Refresh"/> 
        /// to wait out <see cref="InvalidOperationException"/> errors caused by the Windows SCM 
        /// locking the service during state transitions.
        /// </remarks>
        private void HandleTransitionalError(string serviceName, IServiceController controller, ServiceControllerStatus targetStatus, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    controller.Refresh();
                    if (controller.Status == targetStatus) return;

                    // If it's still in a pending state, wait and retry the command
                    if (targetStatus == ServiceControllerStatus.Stopped)
                        controller.Stop();
                    else if (targetStatus == ServiceControllerStatus.Running)
                        controller.Start();

                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        throw new System.TimeoutException($"Service '{serviceName}' failed to reach {targetStatus} within the timeout period.");

                    controller.WaitForStatus(targetStatus, remaining);
                    return;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                {
                    // Still transitional or experiencing transient SCM access blocks; wait before the next poll
                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero) break;
                    Thread.Sleep((int)Math.Min(AppConfig.ServiceRestarterPollIntervalMs, remaining.TotalMilliseconds));
                }
            }

            throw new System.TimeoutException($"Service '{serviceName}' failed to reach {targetStatus} within the timeout period.");
        }
    }
}