namespace Servy.Core.Services
{
    /// <summary>
    /// Provides an abstraction for retrieving a collection of Windows services.
    /// </summary>
    public interface IServiceControllerProvider
    {
        /// <summary>
        /// Retrieves a specific Windows service controller wrapper by service name.
        /// </summary>
        /// <param name="serviceName">The name that identifies the service to the system.</param>
        /// <returns>An <see cref="IServiceControllerWrapper"/> instance for the target service.</returns>
        IServiceControllerWrapper GetService(string serviceName);

        /// <summary>
        /// Retrieves all Windows services currently registered on the local computer.
        /// </summary>
        /// <returns>
        /// An array of <see cref="IServiceControllerWrapper"/> instances representing the services.
        /// <b>The caller owns the returned wrappers and must dispose every element</b>, including
        /// any it does not go on to use; each wrapper holds a live Service Control Manager handle.
        /// </returns>
        /// <remarks>
        /// This is typically a wrapper around the static <see cref="System.ServiceProcess.ServiceController.GetServices()"/>
        /// method to enable unit testing and dependency injection.
        /// </remarks>
        IServiceControllerWrapper[] GetServices();
    }
}
