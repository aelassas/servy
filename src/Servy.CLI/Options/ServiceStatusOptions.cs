using CommandLine;
using Servy.CLI.Resources;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>status</c> command, which retrieves the current status of a Windows service.
    /// </summary>
    [Verb(
        "status",
        HelpText = "Help_Verb_Status",
        ResourceType = typeof(Strings)
    )]
    public class ServiceStatusOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the Windows service to check.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Help_Status_Name", ResourceType = typeof(Strings))]
        public string? ServiceName { get; set; }
    }
}
