using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;

namespace Servy.Core.Services
{
    /// <summary>
    /// Default implementation of <see cref="IWindowsServiceProvider"/> that interacts with
    /// the actual Windows system to retrieve service information via <see cref="ServiceController"/>
    /// and advapi32.dll.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class WindowsServiceProvider : IWindowsServiceProvider
    {
        /// <inheritdoc/>
        public IEnumerable<WindowsServiceInfo> GetServices()
        {
            return ServiceController.GetServices()
                .Select(s => new WindowsServiceInfo
                {
                    ServiceName = s.ServiceName,
                    DisplayName = s.DisplayName
                });
        }

    }

}
