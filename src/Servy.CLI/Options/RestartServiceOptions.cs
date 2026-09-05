using CommandLine;
using Servy.CLI.Resources;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>restart</c> command to restart a Windows service.
    /// </summary>
    [Verb("restart", HelpText = "Help_Verb_Restart", ResourceType = typeof(Strings))]
    public class RestartServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to restart.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Help_Restart_Name", ResourceType = typeof(Strings))]
        public string? ServiceName { get; set; }
    }
}
