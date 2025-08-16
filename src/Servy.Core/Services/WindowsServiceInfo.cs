using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servy.Core.Services
{
    /// <summary>
    /// Represents information about a Windows service, including its service name
    /// and display name. This class is used by <see cref="IWindowsServiceProvider"/>
    /// to abstract service details for testing and querying purposes.
    /// </summary>
    public class WindowsServiceInfo
    {
        /// <summary>
        /// Gets or sets the system name of the Windows service.
        /// This is the name used by the system and APIs to identify the service.
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the Windows service.
        /// This is the human-readable name shown in the Services MMC.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
    }

}
