using Servy.Core.Enums;
using Servy.Core.Interfaces;
using Servy.Core.Services;
using Servy.Models;
using Servy.Resources;
using Servy.Services;
using Servy.ViewModels.Items;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Servy.ViewModels
{
    /// <summary>
    /// ViewModel for the main service management UI.
    /// Implements properties, commands, and logic for configuring and managing Windows services
    /// such as install, uninstall, start, stop, and restart.
    /// </summary>
    public partial class MainViewModel : INotifyPropertyChanged
    {
        #region Services

        private readonly IFileDialogService _dialogService;
        private IServiceCommands _serviceCommands;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// Used for data binding updates.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property name.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Constants 

        private const int DefaultRotationSize = 10 * 1024 * 1024; // Default to 10 MB
        private const int DefaultHeartbeatInterval = 30;          // 30 seconds
        private const int DefaultMaxFailedChecks = 3;             // 3 attempts
        private const int DefaultMaxRestartAttempts = 3;          // 3 attempts

        #endregion

        #region Private Fields

        private readonly ServiceConfiguration _config = new ServiceConfiguration();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the name of the Windows service. 
        /// Updating this property also updates the associated ServiceControllerWrapper instance.
        /// </summary>
        public string ServiceName
        {
            get => _config.Name;
            set
            {
                if (_config.Name != value)
                {
                    _config.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the service.
        /// </summary>
        public string ServiceDescription
        {
            get => _config.Description;
            set { _config.Description = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the path to the executable process to be run by the service.
        /// </summary>
        public string ProcessPath
        {
            get => _config.ExecutablePath;
            set { _config.ExecutablePath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the startup directory for the process.
        /// </summary>
        public string StartupDirectory
        {
            get => _config.StartupDirectory;
            set { _config.StartupDirectory = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets additional command line parameters for the process.
        /// </summary>
        public string ProcessParameters
        {
            get => _config.Parameters;
            set { _config.Parameters = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the startup type selected for the service.
        /// </summary>
        public ServiceStartType SelectedStartupType
        {
            get => _config.StartupType;
            set { _config.StartupType = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the process priority selected for the service process.
        /// </summary>
        public ProcessPriority SelectedProcessPriority
        {
            get => _config.Priority;
            set { _config.Priority = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the list of available startup types for services.
        /// </summary>
        public List<StartupTypeItem> StartupTypes { get; } = new List<StartupTypeItem>
        {
            new StartupTypeItem { StartupType = ServiceStartType.Automatic, DisplayName = Strings.StartupType_Automatic },
            new StartupTypeItem { StartupType = ServiceStartType.Manual, DisplayName = Strings.StartupType_Manual },
            new StartupTypeItem { StartupType = ServiceStartType.Disabled, DisplayName = Strings.StartupType_Disabled },
        };

        /// <summary>
        /// Gets the list of available process priority options.
        /// </summary>
        public List<ProcessPriorityItem> ProcessPriorities { get; } = new List<ProcessPriorityItem>
        {
            new ProcessPriorityItem { Priority = ProcessPriority.Idle, DisplayName = Strings.ProcessPriority_Idle },
            new ProcessPriorityItem { Priority = ProcessPriority.BelowNormal, DisplayName = Strings.ProcessPriority_BelowNormal },
            new ProcessPriorityItem { Priority = ProcessPriority.Normal, DisplayName = Strings.ProcessPriority_Normal },
            new ProcessPriorityItem { Priority = ProcessPriority.AboveNormal, DisplayName = Strings.ProcessPriority_AboveNormal },
            new ProcessPriorityItem { Priority = ProcessPriority.High, DisplayName = Strings.ProcessPriority_High },
            new ProcessPriorityItem { Priority = ProcessPriority.RealTime, DisplayName = Strings.ProcessPriority_RealTime },
        };

        /// <summary>
        /// Gets or sets the path for standard output redirection.
        /// </summary>
        public string StdoutPath
        {
            get => _config.StdoutPath;
            set { _config.StdoutPath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the path for standard error redirection.
        /// </summary>
        public string StderrPath
        {
            get => _config.StderrPath;
            set { _config.StderrPath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether log rotation is enabled.
        /// </summary>
        public bool EnableRotation
        {
            get => _config.EnableRotation;
            set { _config.EnableRotation = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the log rotation size as a string (in bytes).
        /// </summary>
        public string RotationSize
        {
            get => _config.RotationSize;
            set { _config.RotationSize = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// </summary>
        public bool EnableHealthMonitoring
        {
            get => _config.EnableHealthMonitoring;
            set { _config.EnableHealthMonitoring = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the heartbeat interval (seconds) as a string.
        /// </summary>
        public string HeartbeatInterval
        {
            get => _config.HeartbeatInterval;
            set { _config.HeartbeatInterval = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the maximum allowed failed health checks as a string.
        /// </summary>
        public string MaxFailedChecks
        {
            get => _config.MaxFailedChecks;
            set { _config.MaxFailedChecks = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the recovery action selected for the service.
        /// </summary>
        public RecoveryAction SelectedRecoveryAction
        {
            get => _config.RecoveryAction;
            set { _config.RecoveryAction = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the list of available recovery actions.
        /// </summary>
        public List<RecoveryActionItem> RecoveryActions { get; } = new List<RecoveryActionItem>
        {
            new RecoveryActionItem { RecoveryAction= RecoveryAction.None, DisplayName = Strings.RecoveryAction_None },
            new RecoveryActionItem { RecoveryAction= RecoveryAction.RestartService, DisplayName = Strings.RecoveryAction_RestartService },
            new RecoveryActionItem { RecoveryAction= RecoveryAction.RestartProcess, DisplayName = Strings.RecoveryAction_RestartProcess },
            new RecoveryActionItem { RecoveryAction= RecoveryAction.RestartComputer, DisplayName = Strings.RecoveryAction_RestartComputer },
        };

        /// <summary>
        /// Gets or sets the maximum number of restart attempts as a string.
        /// </summary>
        public string MaxRestartAttempts
        {
            get => _config.MaxRestartAttempts;
            set { _config.MaxRestartAttempts = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets environment variables as a string.
        /// </summary>
        public string EnvironmentVariables
        {
            get => _config.EnvironmentVariables;
            set { _config.EnvironmentVariables = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets service dependencies as a string.
        /// </summary>
        public string ServiceDependencies
        {
            get => _config.ServiceDependencies;
            set { _config.ServiceDependencies = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to browse and select the executable process path.
        /// </summary>
        public ICommand BrowseProcessPathCommand { get; }

        /// <summary>
        /// Command to browse and select the startup directory.
        /// </summary>
        public ICommand BrowseStartupDirectoryCommand { get; }

        /// <summary>
        /// Command to browse and select the standard output file path.
        /// </summary>
        public ICommand BrowseStdoutPathCommand { get; }

        /// <summary>
        /// Command to browse and select the standard error file path.
        /// </summary>
        public ICommand BrowseStderrPathCommand { get; }

        /// <summary>
        /// Command to install the configured service.
        /// </summary>
        public ICommand InstallCommand { get; }

        /// <summary>
        /// Command to uninstall the service.
        /// </summary>
        public ICommand UninstallCommand { get; }

        /// <summary>
        /// Command to start the service.
        /// </summary>
        public ICommand StartCommand { get; }

        /// <summary>
        /// Command to stop the service.
        /// </summary>
        public ICommand StopCommand { get; }

        /// <summary>
        /// Command to restart the service.
        /// </summary>
        public ICommand RestartCommand { get; }

        /// <summary>
        /// Command to clear the form fields.
        /// </summary>
        public ICommand ClearCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class with the specified services.
        /// </summary>
        /// <param name="dialogService">Service to open file and folder dialogs.</param>
        /// <param name="serviceCommands">Service commands to manage Windows services.</param>
        public MainViewModel(IFileDialogService dialogService, IServiceCommands serviceCommands)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceCommands = serviceCommands;

            // Initialize defaults
            ServiceName = string.Empty;
            ServiceDescription = string.Empty;
            ProcessPath = string.Empty;
            StartupDirectory = string.Empty;
            ProcessParameters = string.Empty;
            SelectedStartupType = ServiceStartType.Automatic;
            SelectedProcessPriority = ProcessPriority.Normal;
            EnableRotation = false;
            RotationSize = DefaultRotationSize.ToString();
            SelectedRecoveryAction = RecoveryAction.RestartService;
            HeartbeatInterval = DefaultHeartbeatInterval.ToString();
            MaxFailedChecks = DefaultMaxFailedChecks.ToString();
            MaxRestartAttempts = DefaultMaxRestartAttempts.ToString();

            // Commands
            BrowseProcessPathCommand = new RelayCommand(OnBrowseProcessPath);
            BrowseStartupDirectoryCommand = new RelayCommand(OnBrowseStartupDirectory);
            BrowseStdoutPathCommand = new RelayCommand(OnBrowseStdoutPath);
            BrowseStderrPathCommand = new RelayCommand(OnBrowseStderrPath);
            InstallCommand = new RelayCommand(OnInstallService);
            UninstallCommand = new RelayCommand(OnUninstallService);
            StartCommand = new RelayCommand(OnStartService);
            StopCommand = new RelayCommand(OnStopService);
            RestartCommand = new RelayCommand(OnRestartService);
            ClearCommand = new RelayCommand(ClearForm);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class for design-time data.
        /// </summary>
        public MainViewModel() : this(
            new DesignTimeFileDialogService(),
            null
            )
        {
            _serviceCommands = new ServiceCommands(
                new ServiceManager(name => new ServiceControllerWrapper(name), new WindowsServiceApi(), new Win32ErrorProvider()),
                new MessageBoxService()
            );
        }

        #endregion

        #region Dialog Command Handlers

        /// <summary>
        /// Opens a dialog to browse for an executable file and sets <see cref="ProcessPath"/>.
        /// </summary>
        private void OnBrowseProcessPath()
        {
            var path = _dialogService.OpenExecutable();
            if (!string.IsNullOrEmpty(path)) ProcessPath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="StartupDirectory"/>.
        /// </summary>
        private void OnBrowseStartupDirectory()
        {
            var folder = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(folder)) StartupDirectory = folder;
        }

        /// <summary>
        /// Opens a dialog to select a file path for standard output redirection.
        /// </summary>
        private void OnBrowseStdoutPath()
        {
            var path = _dialogService.SaveFile("Select standard output file");
            if (!string.IsNullOrEmpty(path)) StdoutPath = path;
        }

        /// <summary>
        /// Opens a dialog to select a file path for standard error redirection.
        /// </summary>
        private void OnBrowseStderrPath()
        {
            var path = _dialogService.SaveFile("Select standard error file");
            if (!string.IsNullOrEmpty(path)) StderrPath = path;
        }

        #endregion

        #region Service Command Handlers

        /// <summary>
        /// Calls <see cref="IServiceCommands.InstallService"/> with the current property values.
        /// </summary>
        private void OnInstallService()
        {
            _serviceCommands.InstallService(
                _config.Name,
                _config.Description,
                _config.ExecutablePath,
                _config.StartupDirectory,
                _config.Parameters,
                _config.StartupType,
                _config.Priority,
                _config.StdoutPath,
                _config.StderrPath,
                _config.EnableRotation,
                _config.RotationSize,
                _config.EnableHealthMonitoring,
                _config.HeartbeatInterval,
                _config.MaxFailedChecks,
                _config.RecoveryAction,
                _config.MaxRestartAttempts,
                _config.EnvironmentVariables,
                _config.ServiceDependencies
                );
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.UninstallService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private void OnUninstallService()
        {
            _serviceCommands.UninstallService(ServiceName);
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.StartService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private void OnStartService()
        {
            _serviceCommands.StartService(ServiceName);
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.StopService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private void OnStopService()
        {
            _serviceCommands.StopService(ServiceName);
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.RestartService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private void OnRestartService()
        {
            _serviceCommands.RestartService(ServiceName);
        }

        #endregion

        #region Form Command Handlers

        /// <summary>
        /// Clears all form fields and resets to default values.
        /// </summary>
        private void ClearForm()
        {
            ServiceName = string.Empty;
            ServiceDescription = string.Empty;
            ProcessPath = string.Empty;
            StartupDirectory = string.Empty;
            ProcessParameters = string.Empty;
            SelectedStartupType = ServiceStartType.Automatic;
            SelectedProcessPriority = ProcessPriority.Normal;
            EnableRotation = false;
            RotationSize = DefaultRotationSize.ToString();
            StdoutPath = string.Empty;
            StderrPath = string.Empty;
            EnableHealthMonitoring = false;
            SelectedRecoveryAction = RecoveryAction.RestartService;
            HeartbeatInterval = DefaultHeartbeatInterval.ToString();
            MaxFailedChecks = DefaultMaxFailedChecks.ToString();
            MaxRestartAttempts = DefaultMaxRestartAttempts.ToString();
            EnvironmentVariables = string.Empty;
            ServiceDependencies = string.Empty;
        }

        #endregion

    }
}
