using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
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
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> ExecuteAsync(StartServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"start service '{opts.ServiceName}'";
            var suggestion = "Ensure the service is installed, the executable path is valid, and the service account has 'Log On As Service' rights.";

            return await ExecuteServiceOperationAsync(
                commandName: "start",
                action: action,
                suggestion: suggestion,
                serviceName: opts.ServiceName,
                serviceManager: _serviceManager,
                operation: (token) => _serviceManager.StartServiceAsync(opts.ServiceName, cancellationToken: token),
                successMessageFormatter: (name) => string.Format(Strings.Msg_StartSuccess, name),
                preCheck: NotDisabledPreCheck(_serviceManager, opts.ServiceName),
                cancellationToken: cancellationToken);
        }
    }
}