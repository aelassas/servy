using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Logging;
using Servy.Core.Services;
using System;

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
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public CommandResult Execute(ServiceStatusOptions opts)
        {
            var action = $"query status for service '{opts.ServiceName}'";
            var suggestion = "Verify the service name is spelled correctly and that it is currently installed on this system.";

            return ExecuteWithHandling("status", action, suggestion, () =>
            {
                // 1. Validation using localized resource
                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail(Strings.Msg_ServiceNameRequired);

                // 2. Direct execution
                var status = _serviceManager.GetServiceStatus(opts.ServiceName);

                // 1. Log the detailed technical status
                Logger.Info(string.Format(Strings.Msg_ServiceStatusResult, opts.ServiceName, status));

                // 2. Return the localized result to the console
                return CommandResult.Ok(string.Format(Strings.Msg_ServiceStatusResult, opts.ServiceName, status));
            });
        }

    }
}
