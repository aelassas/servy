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
        public RestartResult RestartService(string serviceName, TimeSpan timeout)
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
                    catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                    {
                        // ROBUSTNESS: Service was uninstalled, marked for deletion, or native SCM handle was dropped.
                        return RestartResult.ServiceNotFound;
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
                    catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                    {
                        // ROBUSTNESS: Handle disappearance or native SCM teardown during the refresh cycle.
                        return RestartResult.ServiceNotFound;
                    }
                }

                // 2. Stop phase
                // ROBUSTNESS: Secure the stop-phase entry check against mid-flight uninstalls
                // to prevent unhandled top-level crashes before entering the main execution frame.
                ServiceControllerStatus stopEntryStatus;
                try
                {
                    stopEntryStatus = controller.Status;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                {
                    // Clean exit if the service vanished or SCM handle dropped between the settle phase and this query
                    return RestartResult.ServiceNotFound;
                }

                if (stopEntryStatus != ServiceControllerStatus.Stopped)
                {
                    try
                    {
                        controller.Stop();
                        var stopRemaining = timeout - stopwatch.Elapsed;
                        if (stopRemaining <= TimeSpan.Zero)
                            throw new System.TimeoutException(
                                $"Timeout expired while waiting for service '{serviceName}' to reach Stopped. " +
                                "The Stop command was issued; the service is stopping and will not be restarted by this run.");

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
                    catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                    {
                        // Fallback: If it transitioned to Pending or experienced SCM access blocks between our check and the call
                        var transitionalResult = HandleTransitionalError(serviceName, controller, ServiceControllerStatus.Stopped, timeout - stopwatch.Elapsed);
                        if (transitionalResult.HasValue) return transitionalResult.Value;
                    }
                }

                // 3. Start phase
                try
                {
                    controller.Refresh();
                    if (controller.Status == ServiceControllerStatus.Running)
                        return RestartResult.Restarted; // already running, nothing left to do
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                {
                    return RestartResult.ServiceNotFound;
                }

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

                    return RestartResult.Restarted;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                {
                    // Fallback: If it transitioned to Pending or experienced SCM access blocks between our check and the call
                    var transitionalResult = HandleTransitionalError(serviceName, controller, ServiceControllerStatus.Running, timeout - stopwatch.Elapsed);
                    if (transitionalResult.HasValue) return transitionalResult.Value;
                    return RestartResult.Restarted;
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
        /// <returns>
        /// A <see cref="RestartResult"/> if the operation detected that the service was uninstalled or lost (<see cref="RestartResult.ServiceNotFound"/>);
        /// otherwise, <c>null</c> when the target status is successfully reached.
        /// </returns>
        /// <exception cref="System.TimeoutException">
        /// Thrown if the service fails to reach the <paramref name="targetStatus"/>
        /// before the <paramref name="timeout"/> expires.
        /// </exception>
        /// <remarks>
        /// This method uses an interrogation loop with <see cref="IServiceController.Refresh"/>
        /// to wait out the <see cref="InvalidOperationException"/>, <see cref="Win32Exception"/>
        /// and <see cref="System.ServiceProcess.TimeoutException"/> errors raised while the Windows SCM
        /// holds the service in a state transition. A re-probe after each failure distinguishes a service
        /// that is still transitioning from one that has been uninstalled.
        /// </remarks>
        private RestartResult? HandleTransitionalError(string serviceName, IServiceController controller, ServiceControllerStatus targetStatus, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    controller.Refresh();
                    if (controller.Status == targetStatus) return null;

                    // Not in the target state yet, whatever that state is: re-issue the command.
                    // There is no pending-state test here, and no wait before the retry. While the
                    // service is still transitioning the SCM refuses the command with
                    // ERROR_SERVICE_CANNOT_ACCEPT_CTRL, and it is the catch block below that
                    // re-probes and then sleeps before the next attempt.
                    if (targetStatus == ServiceControllerStatus.Stopped)
                        controller.Stop();
                    else if (targetStatus == ServiceControllerStatus.Running)
                        controller.Start();

                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        throw new System.TimeoutException($"Service '{serviceName}' failed to reach {targetStatus} within the timeout period.");

                    controller.WaitForStatus(targetStatus, remaining);
                    return null;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception || ex is System.ServiceProcess.TimeoutException)
                {
                    // ROBUSTNESS: Re-probe status to detect mid-flight uninstalls or dropped SCM handles
                    try
                    {
                        controller.Refresh();
                        _ = controller.Status;
                    }
                    catch (Exception probeEx) when (probeEx is InvalidOperationException || probeEx is Win32Exception)
                    {
                        return RestartResult.ServiceNotFound;
                    }

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
