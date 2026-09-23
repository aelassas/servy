using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Services;

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
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceManager"/> is <c>null</c>.
        /// </exception>
        public UninstallServiceCommand(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        }

        /// <summary>
        /// Executes the uninstall of the service with the specified options.
        /// </summary>
        /// <param name="opts">Options containing the service name to uninstall.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="Task{CommandResult}"/> indicating the success or failure of the operation.</returns>
        public async Task<CommandResult> ExecuteAsync(UninstallServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = string.Format(Strings.Msg_UninstallServiceAction, opts.ServiceName);
            var suggestion = Strings.Msg_UninstallServiceSuggestion;

            return await ExecuteServiceOperationAsync(
                commandName: "uninstall",
                action: action,
                suggestion: suggestion,
                serviceName: opts.ServiceName,
                serviceManager: _serviceManager,
                operation: (token) => _serviceManager.UninstallServiceAsync(opts.ServiceName, cancellationToken: token),
                successMessageFormatter: (name) => string.Format(Strings.Msg_UninstallSuccess, name),
                skipInstalledCheck: true,
                cancellationToken: cancellationToken);
        }
    }
}
