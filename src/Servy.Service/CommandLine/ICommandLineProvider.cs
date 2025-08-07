namespace Servy.Service.CommandLine
{
    /// <summary>
    /// Provides access to command-line arguments for the service.
    /// </summary>
    public interface ICommandLineProvider
    {
        /// <summary>
        /// Retrieves the command-line arguments passed to the process.
        /// </summary>
        /// <returns>An array of command-line arguments.</returns>
        string[] GetArgs();
    }
}
