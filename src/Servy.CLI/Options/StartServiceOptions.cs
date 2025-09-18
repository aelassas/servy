using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>start</c> command to start a Windows service.
    /// </summary>
    [Verb("start", HelpText = "Start a Windows service.")]
    public class StartServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to start.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Name of the service to start.")]
        public string ServiceName { get; set; }
    }
}
