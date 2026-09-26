using CommandLine;

namespace Servy.CLI.Options
{
    /// <summary>
    /// Options for the <c>show</c> command, which prints a human-readable view of the Servy
    /// service configuration stored in the database.
    /// </summary>
    /// <remarks>
    /// The command has two modes, selected by whether <see cref="ServiceName"/> is supplied:
    /// <list type="bullet">
    /// <item><description>with <c>--name</c>, the full configuration of a single service, grouped by category;</description></item>
    /// <item><description>without it, a one-line-per-service table of every service in the database,
    /// optionally narrowed by <see cref="SearchKeyword"/>.</description></item>
    /// </list>
    /// The two options are mutually exclusive: <c>--search</c> narrows the list, and there is nothing
    /// for it to narrow once a single service has been named.
    /// </remarks>
    [Verb("show", HelpText = "Show a Servy Windows service configuration in a human-readable form, or list all services when no name is given.")]
    public class ShowServiceOptions : GlobalOptionsBase
    {
        /// <summary>
        /// Gets or sets the name of the service to show.
        /// When omitted, the command lists every service in the database instead.
        /// </summary>
        [Option('n', "name", Required = false, HelpText = "Name of the service to show. When omitted, all services are listed.")]
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the keyword used to narrow the service list by name or description.
        /// Only meaningful in list mode; it cannot be combined with <see cref="ServiceName"/>.
        /// </summary>
        [Option('s', "search", Required = false, HelpText = "Filter the service list by a keyword matched against the service name or description. Cannot be combined with --name.")]
        public string SearchKeyword { get; set; }
    }
}
