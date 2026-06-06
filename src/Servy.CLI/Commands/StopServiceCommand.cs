using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Services;

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
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating the result of the stop operation.</returns>
        public async Task<CommandResult> ExecuteAsync(StopServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"stop service '{opts.ServiceName}'";
            var suggestion = "Ensure you have Administrator privileges. If the service is unresponsive, you may need to terminate the process manually via Task Manager.";

            return await ExecuteServiceOperationAsync(
                commandName: "stop",
                action: action,
                suggestion: suggestion,
                serviceName: opts.ServiceName,
                serviceManager: _serviceManager,
                operation: (token) => _serviceManager.StopServiceAsync(opts.ServiceName, cancellationToken: token),
                successMessageFormatter: (name) => string.Format(Strings.Msg_StopSuccess, name),
                cancellationToken: cancellationToken);
        }
    }
}