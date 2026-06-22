using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Logging;
using Servy.Core.Services;

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
                    return CommandResult.Fail(Strings.Msg_ServiceNameRequired);

                // 2. Direct execution
                var status = _serviceManager.GetServiceStatus(opts.ServiceName, cancellationToken: cancellationToken);

                // 3. Log the detailed technical status and return the localized result to the console
                var statusMsg = string.Format(Strings.Msg_ServiceStatusResult, opts.ServiceName, status);
                Logger.Info(statusMsg);
                return CommandResult.Ok(statusMsg);
            });
        }

    }
}
