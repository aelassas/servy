using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Enums;
using Servy.Core.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

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
            var action = string.Format(Strings.Msg_RestartServiceAction, opts.ServiceName);
            var suggestion = Strings.Msg_RestartServiceSuggestion;

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