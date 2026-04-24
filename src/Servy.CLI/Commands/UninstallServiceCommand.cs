using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Threading;
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
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="Task{CommandResult}"/> indicating the success or failure of the operation.</returns>
        public async Task<CommandResult> Execute(UninstallServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"uninstall service '{opts.ServiceName}'";
            var suggestion = "Ensure the service is stopped before uninstalling and that you are running this command as an Administrator.";

            return await ExecuteWithHandlingAsync("uninstall", action, suggestion, async () =>
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

                // Attempt to uninstall the service
                var res = await _serviceManager.UninstallServiceAsync(opts.ServiceName, cancellationToken);
                if (res.IsSuccess)
                {
                    // 1. Data Persistence: Remove the service record from the repository
                    await _serviceRepository.DeleteAsync(opts.ServiceName);

                    // 2. Localized Success Output
                    var successMsg = string.Format(Strings.Msg_UninstallSuccess, opts.ServiceName);

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
