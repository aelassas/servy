using Servy.CLI.Models;
using Servy.CLI.Options;
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
            _serviceManager = serviceManager;
        }

        /// <summary>
        /// Executes the start of the service with the specified options.
        /// </summary>
        /// <param name="opts">Start service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public CommandResult Execute(StartServiceOptions opts)
        {
            return ExecuteWithHandling(() =>
            {
                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail("Service name is required.");

                var success = _serviceManager.StartService(opts.ServiceName);
                return success
                    ? CommandResult.Ok("Service started successfully.")
                    : CommandResult.Fail("Failed to start service.");
            });
        }
    }
}
