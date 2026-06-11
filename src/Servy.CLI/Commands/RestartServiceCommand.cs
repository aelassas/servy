using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Services;

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
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> ExecuteAsync(RestartServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"restart service '{opts.ServiceName}'";
            var suggestion = "Ensure the service is currently installed and that your account has sufficient permissions to stop and start services.";

            return await ExecuteServiceOperationAsync(
                commandName: "restart",
                action: action,
                suggestion: suggestion,
                serviceName: opts.ServiceName,
                serviceManager: _serviceManager,
                operation: (token) => _serviceManager.RestartServiceAsync(opts.ServiceName, cancellationToken: token),
                successMessageFormatter: (name) => string.Format(Strings.Msg_RestartSuccess, name),
                preCheck: NotDisabledPreCheck(_serviceManager, opts.ServiceName),
                cancellationToken: cancellationToken);
        }
    }
}