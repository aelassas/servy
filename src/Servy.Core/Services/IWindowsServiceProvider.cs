using System.Collections.Generic;
using System.Management;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides an abstraction for accessing Windows services and querying their WMI information.
    /// This allows unit testing without depending on actual Windows services.
    /// </summary>
    public interface IWindowsServiceProvider
    {
        /// <summary>
        /// Gets a list of all Windows services on the system.
        /// </summary>
        /// <returns>An enumerable of <see cref="WindowsServiceInfo"/> objects representing the installed services.</returns>
        IEnumerable<WindowsServiceInfo> GetServices();

        /// <summary>
        /// Executes a WMI query to retrieve information about Windows services.
        /// </summary>
        /// <param name="wmiQuery">The WMI query string to execute.</param>
        /// <returns>An enumerable of <see cref="ManagementBaseObject"/> representing the results of the query.</returns>
        IEnumerable<ManagementBaseObject> QueryService(string wmiQuery);
    }

}
