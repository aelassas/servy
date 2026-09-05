using CommandLine;
using Servy.CLI.Resources;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>export</c> command to export a Servy Windows service configuration to a file.
    /// </summary>
    [Verb("export", HelpText = "Help_Verb_Export", ResourceType = typeof(Strings))]
    public class ExportServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to export.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Help_Export_Name", ResourceType = typeof(Strings))]
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the configuration file type.
        /// Possible values: xml, json.
        /// </summary>
        [Option('c', "config", Required = true, HelpText = "Help_Export_Config", ResourceType = typeof(Strings))]
        public string? ConfigFileType { get; set; }

        /// <summary>
        /// Gets or sets the path of the configuration file to export.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Help_Export_Path", ResourceType = typeof(Strings))]
        public string? Path { get; set; }
    }
}
