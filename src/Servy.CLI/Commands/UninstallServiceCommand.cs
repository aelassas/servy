using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.Services;
using System;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to uninstall a Windows service.
    /// </summary>
    public class UninstallServiceCommand : BaseCommand
    {
        private readonly IServiceManager _serviceManager;
        private readonly IServiceRepository _serviceRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="UninstallServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceManager">Service manager to perform service operations.</param>
        /// <param name="serviceRepository">The repository for managing service data persistence.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceManager"/> or <paramref name="serviceRepository"/> is <c>null</c>.
        /// </exception>
        public UninstallServiceCommand(IServiceManager serviceManager, IServiceRepository serviceRepository)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
        }

        /// <summary>
        /// Executes the uninstall operation for the specified service.
        /// </summary>
        /// <param name="opts">Options containing the service name to uninstall.</param>
        /// <returns>A <see cref="Task{CommandResult}"/> indicating the success or failure of the operation.</returns>
        public async Task<CommandResult> Execute(UninstallServiceOptions opts)
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

                // Attempt to uninstall the service
                var success = await _serviceManager.UninstallService(opts.ServiceName);
                if (success)
                {
                    // Remove the service record from the repository
                    await _serviceRepository.DeleteAsync(opts.ServiceName);
                    return CommandResult.Ok("Service uninstalled successfully.");
                }
                else
                {
                    return CommandResult.Fail("Failed to uninstall service.");
                }
            });
        }
    }
}
