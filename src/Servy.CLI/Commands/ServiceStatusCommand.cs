using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using System;
using System.Threading;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to get status of an existing Windows service.
    /// </summary>
    public class ServiceStatusCommand : BaseCommand
    {
        private readonly IServiceManager _serviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceStatusCommand"/> class.
        /// </summary>
        /// <param name="serviceManager">Service manager to perform service operations.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceManager"/> is <c>null</c>.
        /// </exception>
        public ServiceStatusCommand(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        }

        /// <summary>
        /// Executes the retrieval of the status for the specified service.
        /// </summary>
        /// <param name="opts">Options for the service status command.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public CommandResult Execute(ServiceStatusOptions opts, CancellationToken cancellationToken = default)
        {
            var action = string.Format(Strings.Msg_ServiceStatusAction, opts.ServiceName);
            var suggestion = Strings.Msg_ServiceStatusSuggestion;

            return ExecuteWithHandling("status", action, suggestion, () =>
            {
                // 1. Validation using localized resource
                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail(Core.Resources.Strings.Msg_ServiceNameRequired);

                // 2. Direct execution
                var status = _serviceManager.GetServiceStatus(opts.ServiceName, cancellationToken: cancellationToken);

                // 3. Log the status and return it to the console. The status token is deliberately
                //    invariant (ServiceControllerStatus member name, or "NotInstalled" when absent):
                //    it is the machine-readable vocabulary published by the status verb's HelpText and
                //    parsed by callers' scripts. Only the surrounding Msg_ServiceStatusResult template
                //    is localized - do not localize the token itself.
                var statusText = status?.ToString() ?? nameof(ServiceStatus.NotInstalled);
                var statusMsg = string.Format(Strings.Msg_ServiceStatusResult, opts.ServiceName, statusText);
                return CommandResult.Ok(statusMsg);
            });
        }

    }
}
