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
        void RestartService(string serviceName);
    }
}
