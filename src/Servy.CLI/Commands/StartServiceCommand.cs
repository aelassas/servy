using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using System;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to start an existing Windows service.
    /// </summary>
    public class StartServiceCommand : BaseCommand
    {
        private readonly IServiceManager _serviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceManager">Service manager to perform service operations.</param>
        public StartServiceCommand(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        }

        /// <summary>
        /// Executes the start of the service with the specified options.
        /// </summary>
        /// <param name="opts">Start service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> Execute(StartServiceOptions opts)
        {
            var action = $"start service '{opts.ServiceName}'";
            var suggestion = "Ensure the service is installed, the executable path is valid, and the service account has 'Log On As Service' rights.";

            return await ExecuteWithHandlingAsync(action, suggestion, async () =>
            {
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

                var res = await _serviceManager.StartServiceAsync(opts.ServiceName);
                if (res.IsSuccess)
                {
                    // Use the localized resource and include the service name
                    var successMsg = string.Format(Strings.Msg_StartSuccess, opts.ServiceName);

                    Logger.Info(successMsg);
                    return CommandResult.Ok(successMsg);
                }
                else
                {
                    Logger.Info(res.ErrorMessage);
                    return CommandResult.Fail(res.ErrorMessage);
                }

            });
        }
    }
}
