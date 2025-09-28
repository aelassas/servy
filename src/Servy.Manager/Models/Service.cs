using Servy.Core.Enums;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a Windows service and its metadata within Servy Manager.
    /// Implements INotifyPropertyChanged to support UI bindings.
    /// </summary>
    public class Service : INotifyPropertyChanged
    {
        private string _description;
        private ServiceStatus? _status;
        private bool _isInstalled;
        private bool _isConfigurationAppAvailable;
        private ServiceStartType? _startupType;
        private string _userSession;
        private int? _pid;
        private bool _isPidEnabled;
        private double? _cpuUsage;
        private long? _ramUsage;

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
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Gets or sets the current status of the service.
        /// </summary>
        public ServiceStatus? Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the service is installed.
        /// </summary>
        public bool IsInstalled
        {
            get => _isInstalled;
            set => SetProperty(ref _isInstalled, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the service's configuration application is available.
        /// </summary>
        public bool IsConfigurationAppAvailable
        {
            get => _isConfigurationAppAvailable;
            set => SetProperty(ref _isConfigurationAppAvailable, value);
        }

        /// <summary>
        /// Gets or sets the service's startup type.
        /// </summary>
        public ServiceStartType? StartupType
        {
            get => _startupType;
            set => SetProperty(ref _startupType, value);
        }

        /// <summary>
        /// Gets or sets the user session under which the service runs.
        /// </summary>
        public string UserSession
        {
            get => _userSession;
            set => SetProperty(ref _userSession, value);
        }

        /// <summary>
        /// Gets or sets the PID.
        /// </summary>
        public int? Pid
        {
            get => _pid;
            set => SetProperty(ref _pid, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether PID is available.
        /// </summary>
        public bool IsPidEnabled
        {
            get => _isPidEnabled;
            set => SetProperty(ref _isPidEnabled, value);
        }

        /// <summary>
        /// Gets or sets CPU usage in percentage.
        /// </summary>
        public double? CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        /// <summary>
        /// Gets or sets RAM usage in bytes.
        /// </summary>
        public long? RamUsage
        {
            get => _ramUsage;
            set => SetProperty(ref _ramUsage, value);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises PropertyChanged event for the specified property.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Helper to set property and raise PropertyChanged if value is different.
        /// </summary>
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
