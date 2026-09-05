using CommandLine;
using Servy.CLI.Resources;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Represents the 'help' verb to display help information for the CLI.
    /// </summary>
    [Verb("help", HelpText = "Help_Verb_Help", ResourceType = typeof(Strings))]
    public class HelpOptions : GlobalOptionsBase
    {
    }
}
