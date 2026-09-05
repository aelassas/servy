using CommandLine;
using Servy.CLI.Resources;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>uninstall</c> command to uninstall a Windows service.
    /// </summary>
    [Verb("uninstall", HelpText = "Help_Verb_Uninstall", ResourceType = typeof(Strings))]
    public class UninstallServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to uninstall.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Help_Uninstall_Name", ResourceType = typeof(Strings))]
        public string? ServiceName { get; set; }
    }
}
