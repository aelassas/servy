using CommandLine;
using Servy.CLI.Resources;

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
    [Verb("import", HelpText = "Help_Verb_Import", ResourceType = typeof(Strings))]
    public class ImportServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the configuration file type.
        /// Possible values: xml, json.
        /// </summary>
        [Option('c', "config", Required = true, HelpText = "Help_Import_Config", ResourceType = typeof(Strings))]
        public string? ConfigFileType { get; set; }

        /// <summary>
        /// Gets or sets the path of the configuration file to import.
        /// </summary>
        [Option('p', "path", Required = true, HelpText = "Help_Import_Path", ResourceType = typeof(Strings))]
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to install the service after import.
        /// If the service is already installed, restarting it is required for changes to take effect.
        /// </summary>
        [Option('i', "install",
            Required = false,
            HelpText = "Help_Import_Install",
            ResourceType = typeof(Strings))]
        public bool InstallService { get; set; }
    }
}
