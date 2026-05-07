namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides abstractions to query, start, and stop Servy services.
    /// </summary>
    public interface IServiceHelper
    {
        /// <summary>
        /// Gets the names of all currently running Servy UI services.
        /// </summary>
        /// <returns>A list of service names.</returns>
        List<string> GetRunningServyUIServices();

        /// <summary>
        /// Gets the names of all currently running Servy CLI services.
        /// </summary>
        /// <returns>A list of service names.</returns>
        List<string> GetRunningServyCLIServices();

        /// <summary>
        /// Gets the names of all currently running Servy services (GUI and CLI).
        /// </summary>
        /// <returns>A list of service names.</returns>
        List<string> GetRunningServyServices();

        /// <summary>
        /// Starts the specified services if they are not already running or pending start,
        /// and waits until each service is fully running.
        /// </summary>
        /// <param name="services">A collection of service names to start.</param>
        Task StartServices(IEnumerable<string> services);

        /// <summary>
        /// Stops the specified services if they are running or pending stop,
        /// and waits until each service is fully stopped.
        /// </summary>
        /// <param name="services">A collection of service names to stop.</param>
        Task StopServices(IEnumerable<string> services);
    }
}