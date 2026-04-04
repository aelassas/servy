using System.Collections.Generic;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides an abstraction for accessing Windows services and querying their information.
    /// This allows unit testing without depending on actual Windows services.
    /// </summary>
    public interface IWindowsServiceProvider
    {
        /// <summary>
        /// Gets a list of all Windows services on the system.
        /// </summary>
        /// <returns>An enumerable of <see cref="WindowsServiceInfo"/> objects representing the installed services.</returns>
        IEnumerable<WindowsServiceInfo> GetServices();
    }

}
