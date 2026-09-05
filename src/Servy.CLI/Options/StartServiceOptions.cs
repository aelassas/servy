using CommandLine;
using Servy.CLI.Resources;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>start</c> command to start a Windows service.
    /// </summary>
    [Verb("start", HelpText = "Help_Verb_Start", ResourceType = typeof(Strings))]
    public class StartServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to start.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Help_Start_Name", ResourceType = typeof(Strings))]
        public string? ServiceName { get; set; }
    }
}
