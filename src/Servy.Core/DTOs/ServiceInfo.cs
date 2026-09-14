using Servy.Core.Config;
using Servy.Core.Enums;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Represents detailed information about a Windows service.
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the service.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the current state of the service.
        /// Uses <see cref="ServiceStatus"/> enum.
        /// </summary>
        public ServiceStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the startup type of the service.
        /// Uses <see cref="ServiceStartType"/> enum.
        /// </summary>
        public ServiceStartType StartupType { get; set; } = AppConfig.DefaultStartupType;

        /// <summary>
        /// Gets or sets the user account under which the service runs.
        /// Populated from the SCM; a null SCM value is resolved to <c>LocalSystem</c>, the Win32 default.
        /// </summary>
        public string LogOnAs { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the service.
        /// This corresponds to the <c>Description</c> field in Windows services.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
