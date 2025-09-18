using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>uninstall</c> command to uninstall a Windows service.
    /// </summary>
    [Verb("uninstall", HelpText = "Uninstall a service.")]
    public class UninstallServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to uninstall.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Name of the service to uninstall.")]
        public string ServiceName { get; set; }
    }
}
