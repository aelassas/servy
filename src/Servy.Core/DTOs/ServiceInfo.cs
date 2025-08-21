using Servy.Core.Enums;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Represents detailed information about a Windows service.
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// Sets or gets the unique identifier of the service.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the current state of the service.
        /// Uses <see cref="Servy.Core.Enums.ServiceStatus"/> enum.
        /// </summary>
        public ServiceStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the startup type of the service.
        /// Uses <see cref="Servy.Core.Enums.ServiceStartType"/> enum.
        /// </summary>
        public ServiceStartType StartupType { get; set; }

        /// <summary>
        /// Gets or sets the user account under which the service runs.
        /// Defaults to <c>LocalSystem</c> if not specified.
        /// </summary>
        public string UserSession { get; set; }

        /// <summary>
        /// Gets or sets the description of the service.
        /// This corresponds to the <c>Description</c> field in Windows services.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="ServiceInfo"/> with default values.
        /// </summary>
        public ServiceInfo()
        {
            Status = ServiceStatus.None;
            StartupType = ServiceStartType.Automatic;
            UserSession = "LocalSystem";
            Description = string.Empty;
        }
    }
}

