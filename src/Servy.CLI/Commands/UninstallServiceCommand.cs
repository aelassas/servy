using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.Core.Interfaces;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to uninstall a Windows service.
    /// </summary>
    public class UninstallServiceCommand : BaseCommand
    {
        private readonly IServiceManager _serviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="UninstallServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceManager">Service manager to perform service operations.</param>
        public UninstallServiceCommand(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager;
        }

        /// <summary>
        /// Executes the uninstall operation for the specified service.
        /// </summary>
        /// <param name="opts">Options containing the service name to uninstall.</param>
        /// <returns>A <see cref="CommandResult"/> indicating the success or failure of the operation.</returns>
        public CommandResult Execute(UninstallServiceOptions opts)
        {
            return ExecuteWithHandling(() =>
            {
                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail("Service name is required.");

                var success = _serviceManager.UninstallService(opts.ServiceName);
                return success
                    ? CommandResult.Ok("Service uninstalled successfully.")
                    : CommandResult.Fail("Failed to uninstall service.");
            });
        }
    }
}
