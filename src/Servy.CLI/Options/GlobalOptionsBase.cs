using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Represents global options that can be shared across all Servy CLI verbs.
    /// Inherit this class in verb-specific option classes to automatically
    /// include common options such as <see cref="Quiet"/>.
    /// </summary>
    public class GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets a value indicating whether to suppress interactive output
        /// such as spinners or loading animations.
        /// This is useful in automated or non-interactive environments (e.g., CI/CD, Ansible, WinRM),
        /// where console handles may not be available.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable quiet mode; otherwise, <c>false</c>.
        /// </value>
        [Option('q', "quiet", Required = false, HelpText = "Suppress spinner and run in non-interactive mode.")]
        public bool Quiet { get; set; }
    }
}
