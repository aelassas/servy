using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management;
using System.ServiceProcess;

namespace Servy.Core.Services
{
    /// <summary>
    /// Default implementation of <see cref="IWindowsServiceProvider"/> that interacts with
    /// the actual Windows system to retrieve service information via <see cref="ServiceController"/>
    /// and WMI queries.
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

        /// <inheritdoc/>
        public IEnumerable<ManagementBaseObject> QueryService(string wmiQuery)
        {
            using (var searcher = new ManagementObjectSearcher(wmiQuery))
            {
                foreach (var obj in searcher.Get())
                {
                    yield return obj;
                }
            }
        }
    }

}
