using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Logging;
using Servy.Core.Security;
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
            var action = $"stop service '{opts.ServiceName}'";
            var suggestion = "Ensure you have Administrator privileges. If the service is unresponsive, you may need to terminate the process manually via Task Manager.";

            return await ExecuteWithHandlingAsync("stop", action, suggestion, async () =>
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

                var res = await _serviceManager.StopServiceAsync(opts.ServiceName);
                if (res.IsSuccess)
                {
                    // Localize the message and include the service name for clarity
                    var successMsg = string.Format(Strings.Msg_StopSuccess, opts.ServiceName);

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
