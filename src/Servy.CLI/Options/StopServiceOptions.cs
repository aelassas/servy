using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>stop</c> command to stop a Windows service.
    /// </summary>
    [Verb("stop", HelpText = "Stop a Windows service.")]
    public class StopServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to stop.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Name of the service to stop.")]
        public string ServiceName { get; set; }
    }
}
