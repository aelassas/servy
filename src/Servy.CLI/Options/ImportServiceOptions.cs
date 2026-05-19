using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Defines command-line options for the <c>import</c> verb, enabling the ingestion 
    /// of Windows service configurations from JSON or XML files into the Servy 
    /// database and optional registration with the Windows Service Control Manager.
    /// </summary>
    /// <remarks>
    /// This command requires administrative privileges to perform service installation 
    /// and ensures that imported configuration files are validated against 
    /// path-security policies and schema constraints.
    /// </remarks>
    [Verb("import", HelpText = "Import a Windows service configuration into the Servy database and optionally install the service.")]
    public class ImportServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the configuration file type.
        /// Possible values: xml, json.
        /// </summary>
        [Option('c', "config", Required = true, HelpText = "Configuration file type (xml or json).")]
        public string ConfigFileType { get; set; } = null!;

        /// <summary>
        /// Gets or sets the path of the configuration file to import.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Path of the configuration file to import.")]
        public string Path { get; set; } = null!;

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
