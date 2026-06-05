namespace Servy.Restarter
{
    /// <summary>
    /// Interface for service restart operations.
    /// </summary>
    public interface IServiceRestarter
    {
        /// <summary>
        /// Restarts the specified Windows service by stopping and starting it.
        /// </summary>
        /// <param name="serviceName">The name of the service to restart.</param>
        /// <param name="timeout">The maximum total time allowed for the entire restart (settle + stop + start). A <see cref="System.TimeoutException"/> is thrown if the service does not reach the target state within this budget.</param>
        void RestartService(string serviceName, TimeSpan timeout);
    }
}
