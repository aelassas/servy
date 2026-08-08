using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>status</c> command, which retrieves the current status of a Windows service.
    /// </summary>
    [Verb(
        "status",
        HelpText = "Get the current status of a Windows service. Possible results: NotInstalled, Stopped, StartPending, StopPending, Running, ContinuePending, PausePending, Paused."
    )]
    public class ServiceStatusOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the Windows service to check.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Name of the service to check.")]
        public string? ServiceName { get; set; }
    }
}
