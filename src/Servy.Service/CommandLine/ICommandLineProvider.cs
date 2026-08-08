namespace Servy.Service.CommandLine
{
    /// <summary>
    /// Provides access to command-line arguments for the service.
    /// </summary>
    public interface ICommandLineProvider
    {
        /// <summary>
        /// Retrieves the raw process command line, including the executable path.
        /// </summary>
        /// <returns>
        /// The command-line arguments as returned by <see cref="Environment.GetCommandLineArgs"/>:
        /// index 0 is the executable path and the caller-supplied arguments start at index 1.
        /// Implementations must preserve this layout - an array taken from <c>Main(string[] args)</c>
        /// is offset by one and will shift every consumer's indices.
        /// </returns>
        string[] GetArgs();
    }
}
