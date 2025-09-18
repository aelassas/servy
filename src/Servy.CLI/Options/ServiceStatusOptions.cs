using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>status</c> command, which retrieves the current status of a Windows service.
    /// </summary>
    [Verb(
        "status",
        HelpText = "Get the current status of a Windows service. Possible results: Stopped, StartPending, StopPending, Running, ContinuePending, PausePending, Paused."
    )]
    public class ServiceStatusOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the Windows service to check.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "The name of the Windows service to check the status for.")]
        public string ServiceName { get; set; }
    }
}
