using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
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
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        }

        /// <summary>
        /// Executes the restart of the service with the specified options.
        /// </summary>
        /// <param name="opts">Restart service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> Execute(RestartServiceOptions opts)
        {
            var action = $"restart service '{opts.ServiceName}'";
            var suggestion = "Ensure the service is currently installed and that your account has sufficient permissions to stop and start services.";

            return await ExecuteWithHandlingAsync("restart", action, suggestion, async () =>
            {
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail(Strings.Msg_ServiceNameRequired);

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

                var res = await _serviceManager.RestartServiceAsync(opts.ServiceName);
                if (res.IsSuccess)
                {
                    // Localize and include the service name for better feedback
                    var successMsg = string.Format(Strings.Msg_RestartSuccess, opts.ServiceName);

                    Logger.Info(successMsg);
                    return CommandResult.Ok(successMsg);
                }
                else
                {
                    Logger.Error(res.ErrorMessage);
                    return res.ToFailure();
                }
            });
        }
    }
}
