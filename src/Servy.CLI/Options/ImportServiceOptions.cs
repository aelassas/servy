using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>import</c> command to import a Windows service configuration into Servy's database.
    /// </summary>
    [Verb("import", HelpText = "Import a Windows service configuration into Servy's database.")]
    public class ImportServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the configuration file type.
        /// Possible values: xml, json.
        /// </summary>
        [Option('c', "config", Required = true, HelpText = "Configuration file type (xml or json).")]
        public string ConfigFileType { get; set; } = null;

        /// <summary>
        /// Gets or sets the path of the configuration file to import.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Path of the configuration file to import.")]
        public string Path { get; set; } = null;

        /// <summary>
        /// Gets or sets a value indicating whether to install the service after import.
        /// If the service is already installed, restarting it is required for changes to take effect.
        /// </summary>
        [Option('i', "install",
            Required = false,
            HelpText = "Install the service after import. If the service is already installed, restarting it is required for changes to take effect.")]
        public bool InstallService { get; set; }
    }
}
