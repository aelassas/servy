using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

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
                while (IsPendingState(controller.Status))
                {
                    if (stopwatch.Elapsed > timeout)
                        throw new System.TimeoutException($"Service '{serviceName}' stuck in {controller.Status} state.");

                    Thread.Sleep(500);
                    controller.Refresh();
                }

                // 2. Stop phase
                if (controller.Status != ServiceControllerStatus.Stopped)
                {
                    try
                    {
                        controller.Stop();
                        var startRemaining = timeout - stopwatch.Elapsed;
                        if (startRemaining <= TimeSpan.Zero)
                            throw new System.TimeoutException($"No time remaining to start service '{serviceName}'.");

                        controller.WaitForStatus(ServiceControllerStatus.Stopped, startRemaining);
                    }
                    catch (InvalidOperationException)
                    {
                        // Fallback: If it transitioned to Pending between our check and the call
                        HandleTransitionalError(controller, ServiceControllerStatus.Stopped, timeout - stopwatch.Elapsed);
                    }
                }

                // 3. Start phase
                controller.Refresh();
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, timeout - stopwatch.Elapsed);
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
        private void HandleTransitionalError(IServiceController controller, ServiceControllerStatus targetStatus, TimeSpan timeout)
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

                    controller.WaitForStatus(targetStatus, timeout - stopwatch.Elapsed);
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Still transitional, wait a bit before the next poll
                    Thread.Sleep(500);
                }
            }

            throw new System.TimeoutException($"Failed to reach {targetStatus} within the timeout period.");
        }

    }

}
