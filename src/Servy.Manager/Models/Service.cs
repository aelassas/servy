using Servy.Core.Enums;
using System.ServiceProcess;

namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a Windows service and its metadata within Servy Manager.
    /// </summary>
    public class Service
    {
        private string _description;
        private ServiceControllerStatus? _status;
        private bool _isInstalled;
        private bool _isConfigurationAppAvailable;
        private ServiceStartType _startupType;
        private string _userSession;

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the service description.
        /// </summary>
        public string Description
        {
            get => _description;
            set => _description = value;
        }

        /// <summary>
        /// Gets or sets the current status of the service.
        /// </summary>
        public ServiceControllerStatus? Status
        {
            get => _status;
            set => _status = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the service is installed.
        /// </summary>
        public bool IsInstalled
        {
            get => _isInstalled;
            set => _isInstalled = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the service's configuration application is available.
        /// </summary>
        public bool IsConfigurationAppAvailable
        {
            get => _isConfigurationAppAvailable;
            set => _isConfigurationAppAvailable = value;
        }

        /// <summary>
        /// Gets or sets the service's startup type.
        /// </summary>
        public ServiceStartType StartupType
        {
            get => _startupType;
            set => _startupType = value;
        }

        /// <summary>
        /// Gets or sets the user session under which the service runs.
        /// </summary>
        public string UserSession
        {
            get => _userSession;
            set => _userSession = value;
        }
    }
}
