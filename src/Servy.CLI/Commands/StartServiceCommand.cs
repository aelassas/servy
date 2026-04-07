using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;

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

                var res = await _serviceManager.StartServiceAsync(opts.ServiceName);
                if (res.IsSuccess)
                {
                    Logger.Info($"Successfully started the service '{opts.ServiceName}'.");
                    return CommandResult.Ok("Service started successfully.");
                }
                else
                {
                    Logger.Info(res.ErrorMessage);
                    return CommandResult.Fail(res.ErrorMessage!);
                }

            });
        }
    }
}
