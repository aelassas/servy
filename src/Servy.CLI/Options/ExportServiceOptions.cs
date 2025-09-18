using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>export</c> command to export a Servy Windows service configuration to a file.
    /// </summary>
    [Verb("export", HelpText = "Export a Servy Windows service configuration to a configuration file.")]
    public class ExportServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to export.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Name of the service to export.")]
        public string ServiceName { get; set; } = null;

        /// <summary>
        /// Gets or sets the configuration file type.
        /// Possible values: xml, json.
        /// </summary>
        [Option('c', "config", Required = true, HelpText = "Configuration export file type (xml or json).")]
        public string ConfigFileType { get; set; } = null;

        /// <summary>
        /// Gets or sets the path of the configuration file to export.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Path of the configuration file to export.")]
        public string Path { get; set; } = null;
    }
}
