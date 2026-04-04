namespace Servy.Core.Services
{
    /// <summary>
    /// Provides an abstraction for retrieving a collection of Windows services.
    /// </summary>
    public interface IServiceControllerProvider
    {
        /// <summary>
        /// Retrieves all Windows services currently registered on the local computer.
        /// </summary>
        /// <returns>
        /// An array of <see cref="IServiceControllerWrapper"/> instances representing the services.
        /// </returns>
        /// <remarks>
        /// This is typically a wrapper around the static <see cref="System.ServiceProcess.ServiceController.GetServices()"/> 
        /// method to enable unit testing and dependency injection.
        /// </remarks>
        IServiceControllerWrapper[] GetServices();
    }
}