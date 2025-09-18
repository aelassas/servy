using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>restart</c> command to restart a Windows service.
    /// </summary>
    [Verb("restart", HelpText = "Restart a Windows service.")]
    public class RestartServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to restart.
        /// This option is required.
        /// </summary>
        [Option('n', "name", Required = true, HelpText = "Name of the service to restart.")]
        public string ServiceName { get; set; }
    }
}
