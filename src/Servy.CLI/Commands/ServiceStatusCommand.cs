using Servy.CLI.Models;
using Servy.CLI.Options;
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
            _serviceManager = serviceManager;
        }

        /// <summary>
        /// Executes the retrieval of the status for the specified service.
        /// </summary>
        /// <param name="opts">Options for the service status command.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public CommandResult Execute(ServiceStatusOptions opts)
        {
            return ExecuteWithHandling(() =>
            {
                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail("Service name is required.");

                try
                {
                    var status = _serviceManager.GetServiceStatus(opts.ServiceName);
                    return CommandResult.Ok($"Service status: {status}");
                }
                catch (Exception)
                {
                    return CommandResult.Fail("Failed to get service status.");
                }
            });
        }

    }
}
