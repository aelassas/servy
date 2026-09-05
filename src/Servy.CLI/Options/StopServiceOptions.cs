using CommandLine;
using Servy.CLI.Resources;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>stop</c> command to stop a Windows service.
    /// </summary>
    [Verb("stop", HelpText = "Help_Verb_Stop", ResourceType = typeof(Strings))]
    public class StopServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to stop.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Help_Stop_Name", ResourceType = typeof(Strings))]
        public string? ServiceName { get; set; }
    }
}
