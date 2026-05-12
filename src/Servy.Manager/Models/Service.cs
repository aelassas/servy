using Servy.Core.Enums;
using Servy.UI.ViewModels; // Inheriting from the centralized UI ViewModel base

namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a Windows service and its metadata within Servy Manager.
    /// Implements INotifyPropertyChanged via ViewModelBase to support UI bindings.
    /// </summary>
    public class Service : ViewModelBase
    {
        private string? _name;
        private string? _description;
        private ServiceStatus? _status;
        private bool _isInstalled;
        private bool _isDesktopAppAvailable;
        private ServiceStartType? _startupType;
        private string? _logOnAs;
        private int? _pid;
        private bool _isPidEnabled;
        private double? _cpuUsage;
        private long? _ramUsage;
        private string? _stdoutPath;
        private string? _stderrPath;
        private string? _activeStdoutPath;
        private string? _activeStderrPath;

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        public string? Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        /// <summary>
        /// Gets or sets the service description.
        /// </summary>
        public string? Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        /// <summary>
        /// Gets or sets the current status of the service.
        /// </summary>
        public ServiceStatus? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the service is installed.
        /// </summary>
        public bool IsInstalled
        {
            get => _isInstalled;
            set => Set(ref _isInstalled, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the service's configuration application is available.
        /// </summary>
        public bool IsDesktopAppAvailable
        {
            get => _isDesktopAppAvailable;
            set => Set(ref _isDesktopAppAvailable, value);
        }

        /// <summary>
        /// Gets or sets the service's startup type.
        /// </summary>
        public ServiceStartType? StartupType
        {
            get => _startupType;
            set => Set(ref _startupType, value);
        }

        /// <summary>
        /// Gets or sets the LogOnAs identity (account or built-in identity) under which the service runs.
        /// </summary>
        public string? LogOnAs
        {
            get => _logOnAs;
            set => Set(ref _logOnAs, value);
        }

        /// <summary>
        /// Gets or sets the PID.
        /// </summary>
        public int? Pid
        {
            get => _pid;
            set => Set(ref _pid, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether PID is available.
        /// </summary>
        public bool IsPidEnabled
        {
            get => _isPidEnabled;
            set => Set(ref _isPidEnabled, value);
        }

        /// <summary>
        /// Gets or sets the aggregate CPU usage in percentage of whole-machine capacity.
        /// Bounded to the range [0, 100] by <see cref="Core.Helpers.IProcessHelper.GetProcessTreeMetrics"/>.
        /// </summary>
        public double? CpuUsage
        {
            get => _cpuUsage;
            set => Set(ref _cpuUsage, value);
        }

        /// <summary>
        /// Gets or sets RAM usage in bytes.
        /// </summary>
        public long? RamUsage
        {
            get => _ramUsage;
            set => Set(ref _ramUsage, value);
        }

        /// <summary>
        /// Gets or sets the file system path where standard output is redirected.
        /// </summary>
        public string? StdoutPath
        {
            get => _stdoutPath;
            set => Set(ref _stdoutPath, value);
        }

        /// <summary>
        /// Gets or sets the file system path where standard error output is redirected.
        /// </summary>
        public string? StderrPath
        {
            get => _stderrPath;
            set => Set(ref _stderrPath, value);
        }

        /// <summary>
        /// Gets or sets the active file system path where standard output is redirected.
        /// </summary>
        public string? ActiveStdoutPath
        {
            get => _activeStdoutPath;
            set => Set(ref _activeStdoutPath, value);
        }

        /// <summary>
        /// Gets or sets the active file system path where standard error output is redirected.
        /// </summary>
        public string? ActiveStderrPath
        {
            get => _activeStderrPath;
            set => Set(ref _activeStderrPath, value);
        }
    }
}