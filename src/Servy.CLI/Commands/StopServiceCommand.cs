using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Logging;
using Servy.Core.Services;
using System;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to stop a running Windows service.
    /// </summary>
    public class StopServiceCommand : BaseCommand
    {
        private readonly IServiceManager _serviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="StopServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceManager">Service manager to perform service operations.</param>
        public StopServiceCommand(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        }

        /// <summary>
        /// Executes the stop operation for the specified service.
        /// </summary>
        /// <param name="opts">Options containing the service name to stop.</param>
        /// <returns>A <see cref="CommandResult"/> indicating the result of the stop operation.</returns>
        public async Task<CommandResult> Execute(StopServiceOptions opts)
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

                var res = await _serviceManager.StopServiceAsync(opts.ServiceName);
                if (res.IsSuccess)
                {
                    Logger.Info($"Successfully stopped the service '{opts.ServiceName}'.");
                    return CommandResult.Ok("Service stopped successfully.");
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
