using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
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
        public async Task<CommandResult> ExecuteAsync(UninstallServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"uninstall service '{opts.ServiceName}'";
            var suggestion = "Ensure the service is stopped before uninstalling and that you are running this command as an Administrator.";

            return await ExecuteServiceOperationAsync(
                commandName: "uninstall",
                action: action,
                suggestion: suggestion,
                serviceName: opts.ServiceName,
                serviceManager: _serviceManager,
                operation: (token) => _serviceManager.UninstallServiceAsync(opts.ServiceName, cancellationToken: token),
                successMessageFormatter: (name) => string.Format(Strings.Msg_UninstallSuccess, name),
                onSuccess: (token) => _serviceRepository.DeleteAsync(opts.ServiceName, cancellationToken: token),
                cancellationToken: cancellationToken);
        }
    }
}