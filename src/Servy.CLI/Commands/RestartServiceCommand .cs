using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Services;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to restart an existing Windows service.
    /// </summary>
    public class RestartServiceCommand : BaseCommand
    {
        private readonly IServiceManager _serviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestartServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceManager">Service manager to perform service operations.</param>
        public RestartServiceCommand(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager;
        }

        /// <summary>
        /// Executes the restart of the service with the specified options.
        /// </summary>
        /// <param name="opts">Restart service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> Execute(RestartServiceOptions opts)
        {
            return await ExecuteWithHandlingAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail("Service name is required.");

                var exists = _serviceManager.IsServiceInstalled(opts.ServiceName);
                if (!exists)
                {
                    return CommandResult.Fail(Strings.Msg_ServiceNotFound);
                }

                var startupType = _serviceManager.GetServiceStartupType(opts.ServiceName);
                if (startupType == ServiceStartType.Disabled)
                {
                    return CommandResult.Fail(Strings.Msg_ServiceDisabledError);
                }

                var success = await _serviceManager.RestartService(opts.ServiceName);
                return success
                    ? CommandResult.Ok("Service restarted successfully.")
                    : CommandResult.Fail("Failed to restart service.");
            });
        }
    }
}
