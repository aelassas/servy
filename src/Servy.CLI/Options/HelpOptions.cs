using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Represents the 'help' verb to display help information for the CLI.
    /// </summary>
    [Verb("help", HelpText = "Display help information")]
    public class HelpOptions : GlobalOptionsBase
    {
    }
}
