using Servy.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Design;
using Servy.Models;
using Servy.Resources;
using Servy.Services;
using Servy.UI.Commands;
using Servy.UI.Services;
using Servy.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using static Servy.Core.Config.AppConfig;

namespace Servy.ViewModels
{
    /// <summary>
    /// ViewModel for the main service management UI.
    /// Implements properties, commands, and logic for configuring and managing Windows services
    /// such as install, uninstall, start, stop, and restart.
    /// </summary>
    public partial class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Private Fields

        private readonly ServiceConfiguration _config = new ServiceConfiguration();
        private readonly IFileDialogService _dialogService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceRepository _serviceRepository;
        private readonly IHelpService _helpService;
        private bool _isManagerAppAvailable;
        private readonly IAppConfiguration _appConfig;
        private bool _isBusy;
        private bool _disposed;

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

        #region Properties

        /// <summary>
        /// Service commands.
        /// </summary>
        public IServiceCommands ServiceCommands { get; set; }

        /// <summary>
        /// Indicates whether the ViewModel is currently performing a long-running operation.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(() => _isBusy, v => _isBusy = v, value);
        }

        /// <summary>
        /// Gets or sets the name of the Windows service. 
        /// Updating this property also updates the associated ServiceControllerWrapper instance.
        /// </summary>
        public string ServiceName
        {
            get => _config.Name;
            set => Set(() => _config.Name, v => _config.Name = v, value);
        }

        /// <summary>
        /// Gets or sets the display name of the Windows service. 
        /// </summary>
        public string ServiceDisplayName
        {
            get => _config.DisplayName;
            set => Set(() => _config.DisplayName, v => _config.DisplayName = v, value);
        }

        /// <summary>
        /// Gets or sets the description of the service.
        /// </summary>
        public string ServiceDescription
        {
            get => _config.Description;
            set => Set(() => _config.Description, v => _config.Description = v, value);
        }

        /// <summary>
        /// Gets or sets the path to the executable process to be run by the service.
        /// </summary>
        public string ProcessPath
        {
            get => _config.ExecutablePath;
            set => Set(() => _config.ExecutablePath, v => _config.ExecutablePath = v, value);
        }

        /// <summary>
        /// Gets or sets the startup directory for the process.
        /// </summary>
        public string StartupDirectory
        {
            get => _config.StartupDirectory;
            set => Set(() => _config.StartupDirectory, v => _config.StartupDirectory = v, value);
        }

        /// <summary>
        /// Gets or sets additional command line parameters for the process.
        /// </summary>
        public string ProcessParameters
        {
            get => _config.Parameters;
            set => Set(() => _config.Parameters, v => _config.Parameters = v, value);
        }

        /// <summary>
        /// Gets or sets the startup type selected for the service.
        /// </summary>
        public ServiceStartType SelectedStartupType
        {
            get => _config.StartupType;
            set => Set(() => _config.StartupType, v => _config.StartupType = v, value);
        }

        /// <summary>
        /// Gets or sets the process priority selected for the service process.
        /// </summary>
        public ProcessPriority SelectedProcessPriority
        {
            get => _config.Priority;
            set => Set(() => _config.Priority, v => _config.Priority = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the console user interface for the service.
        /// </summary>
        public bool EnableConsoleUI
        {
            get => _config.EnableConsoleUI;
            set => Set(() => _config.EnableConsoleUI, v => _config.EnableConsoleUI = v, value);
        }

        /// <summary>
        /// Gets the list of available startup types for services.
        /// </summary>
        public List<StartupTypeItem> StartupTypes { get; } = new List<StartupTypeItem>
        {
            new StartupTypeItem { StartupType = ServiceStartType.Automatic, DisplayName = Strings.StartupType_Automatic },
            new StartupTypeItem { StartupType = ServiceStartType.AutomaticDelayedStart, DisplayName = Strings.StartupType_AutomaticDelayedStart },
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
            set => Set(() => _config.StdoutPath, v => _config.StdoutPath = v, value);
        }

        /// <summary>
        /// Gets or sets the path for standard error redirection.
        /// </summary>
        public string StderrPath
        {
            get => _config.StderrPath;
            set => Set(() => _config.StderrPath, v => _config.StderrPath = v, value);
        }

        /// <summary>
        /// Gets or sets the start timeout in seconds.
        /// </summary>
        public string StartTimeout
        {
            get => _config.StartTimeout;
            set => Set(() => _config.StartTimeout, v => _config.StartTimeout = v, value);
        }

        /// <summary>
        /// Gets or sets the stop timeout in seconds.
        /// </summary>
        public string StopTimeout
        {
            get => _config.StopTimeout;
            set => Set(() => _config.StopTimeout, v => _config.StopTimeout = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether size-based log rotation is enabled.
        /// </summary>
        public bool EnableSizeRotation
        {
            get => _config.EnableSizeRotation;
            set
            {
                if (Set(() => _config.EnableSizeRotation, v => _config.EnableSizeRotation = v, value))
                {
                    OnPropertyChanged(nameof(EnableRotation));
                }
            }
        }

        /// <summary>
        /// Gets or sets the log rotation size as a string (in MB).
        /// </summary>
        public string RotationSize
        {
            get => _config.RotationSize;
            set => Set(() => _config.RotationSize, v => _config.RotationSize = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether date-based log rotation is enabled.
        /// </summary>
        public bool EnableDateRotation
        {
            get => _config.EnableDateRotation;
            set
            {
                if (Set(() => _config.EnableDateRotation, v => _config.EnableDateRotation = v, value))
                {
                    OnPropertyChanged(nameof(EnableRotation));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether log rotation is enabled.
        /// </summary>
        public bool EnableRotation => EnableSizeRotation || EnableDateRotation;

        /// <summary>
        /// Gets or sets the date rotation type selected for the service.
        /// </summary>
        public DateRotationType SelectedDateRotationType
        {
            get => _config.DateRotationType;
            set => Set(() => _config.DateRotationType, v => _config.DateRotationType = v, value);
        }

        /// <summary>
        /// Gets the list of available date rotation types for services.
        /// </summary>
        public List<DateRotationTypeItem> DateRotationTypes { get; } = new List<DateRotationTypeItem>
        {
            new DateRotationTypeItem { DateRotationType = DateRotationType.Daily, DisplayName = Strings.DateRotationType_Daily},
            new DateRotationTypeItem { DateRotationType = DateRotationType.Weekly, DisplayName = Strings.DateRotationType_Weekly},
            new DateRotationTypeItem { DateRotationType = DateRotationType.Monthly, DisplayName = Strings.DateRotationType_Monthly },
            new DateRotationTypeItem { DateRotationType = DateRotationType.None, DisplayName = Strings.DateRotationType_None},
        };

        /// <summary>
        /// Gets or sets the maximum number of rotated log files to keep.
        /// </summary>
        public string MaxRotations
        {
            get => _config.MaxRotations;
            set => Set(() => _config.MaxRotations, v => _config.MaxRotations = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether log rotation should follow the local system time.
        /// </summary>
        /// <value>
        /// <para>When <c>true</c>, logs rotate at local midnight (e.g., 00:00:00 according to the taskbar clock). 
        /// This is generally more intuitive for users manually inspecting log files.</para>
        /// <para>When <c>false</c>, logs rotate at UTC midnight. This provides a consistent 24-hour cycle 
        /// that is unaffected by Daylight Saving Time transitions.</para>
        /// <para>This property updates the underlying configuration and notifies the UI of the change.</para>
        /// </value>
        public bool UseLocalTimeForRotation
        {
            get => _config.UseLocalTimeForRotation;
            set => Set(() => _config.UseLocalTimeForRotation, v => _config.UseLocalTimeForRotation = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// </summary>
        public bool EnableHealthMonitoring
        {
            get => _config.EnableHealthMonitoring;
            set => Set(() => _config.EnableHealthMonitoring, v => _config.EnableHealthMonitoring = v, value);
        }

        /// <summary>
        /// Gets or sets the heartbeat interval (seconds) as a string.
        /// </summary>
        public string HeartbeatInterval
        {
            get => _config.HeartbeatInterval;
            set => Set(() => _config.HeartbeatInterval, v => _config.HeartbeatInterval = v, value);
        }

        /// <summary>
        /// Gets or sets the maximum allowed failed health checks as a string.
        /// </summary>
        public string MaxFailedChecks
        {
            get => _config.MaxFailedChecks;
            set => Set(() => _config.MaxFailedChecks, v => _config.MaxFailedChecks = v, value);
        }

        /// <summary>
        /// Gets or sets the recovery action selected for the service.
        /// </summary>
        public RecoveryAction SelectedRecoveryAction
        {
            get => _config.RecoveryAction;
            set => Set(() => _config.RecoveryAction, v => _config.RecoveryAction = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to run recovery action even if the process exits successfully.
        /// </summary>
        public bool RecoveryOnCleanExit
        {
            get => _config.RecoveryOnCleanExit;
            set => Set(() => _config.RecoveryOnCleanExit, v => _config.RecoveryOnCleanExit = v, value);
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
            set => Set(() => _config.MaxRestartAttempts, v => _config.MaxRestartAttempts = v, value);
        }

        /// <summary>
        /// Gets or sets failure program path as a string.
        /// </summary>
        public string FailureProgramPath
        {
            get => _config.FailureProgramPath;
            set => Set(() => _config.FailureProgramPath, v => _config.FailureProgramPath = v, value);
        }

        /// <summary>
        /// Gets or sets failure program startup directory as a string.
        /// </summary>
        public string FailureProgramStartupDirectory
        {
            get => _config.FailureProgramStartupDirectory;
            set => Set(() => _config.FailureProgramStartupDirectory, v => _config.FailureProgramStartupDirectory = v, value);
        }

        /// <summary>
        /// Gets or sets failure program parameters as a string.
        /// </summary>
        public string FailureProgramParameters
        {
            get => _config.FailureProgramParameters;
            set => Set(() => _config.FailureProgramParameters, v => _config.FailureProgramParameters = v, value);
        }

        /// <summary>
        /// Gets or sets environment variables as a string.
        /// </summary>
        public string EnvironmentVariables
        {
            get => _config.EnvironmentVariables;
            set => Set(() => _config.EnvironmentVariables, v => _config.EnvironmentVariables = v, value);
        }

        /// <summary>
        /// Gets or sets service dependencies as a string.
        /// </summary>
        public string ServiceDependencies
        {
            get => _config.ServiceDependencies;
            set => Set(() => _config.ServiceDependencies, v => _config.ServiceDependencies = v, value);
        }

        /// <summary>
        /// Gets or sets run as local system as a bool.
        /// </summary>
        public bool RunAsLocalSystem
        {
            get => _config.RunAsLocalSystem;
            set => Set(() => _config.RunAsLocalSystem, v => _config.RunAsLocalSystem = v, value);
        }

        /// <summary>
        /// Gets or sets user account as a string.
        /// </summary>
        public string UserAccount
        {
            get => _config.UserAccount;
            set => Set(() => _config.UserAccount, v => _config.UserAccount = v, value);
        }

        /// <summary>
        /// Gets or sets user password as a string.
        /// </summary>
        public string Password
        {
            get => _config.Password;
            set => Set(() => _config.Password, v => _config.Password = v, value);
        }

        /// <summary>
        /// Gets or sets user password confirmation as a string.
        /// </summary>
        public string ConfirmPassword
        {
            get => _config.ConfirmPassword;
            set => Set(() => _config.ConfirmPassword, v => _config.ConfirmPassword = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch executable path as a string.
        /// </summary>
        public string PreLaunchExecutablePath
        {
            get => _config.PreLaunchExecutablePath;
            set => Set(() => _config.PreLaunchExecutablePath, v => _config.PreLaunchExecutablePath = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch startup directory as a string.
        /// </summary>
        public string PreLaunchStartupDirectory
        {
            get => _config.PreLaunchStartupDirectory;
            set => Set(() => _config.PreLaunchStartupDirectory, v => _config.PreLaunchStartupDirectory = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch parameters as a string.
        /// </summary>
        public string PreLaunchParameters
        {
            get => _config.PreLaunchParameters;
            set => Set(() => _config.PreLaunchParameters, v => _config.PreLaunchParameters = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch environment variables as a string.
        /// </summary>
        public string PreLaunchEnvironmentVariables
        {
            get => _config.PreLaunchEnvironmentVariables;
            set => Set(() => _config.PreLaunchEnvironmentVariables, v => _config.PreLaunchEnvironmentVariables = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch stdout log file path as a string.
        /// </summary>
        public string PreLaunchStdoutPath
        {
            get => _config.PreLaunchStdoutPath;
            set => Set(() => _config.PreLaunchStdoutPath, v => _config.PreLaunchStdoutPath = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch stderr log file path as a string.
        /// </summary>
        public string PreLaunchStderrPath
        {
            get => _config.PreLaunchStderrPath;
            set => Set(() => _config.PreLaunchStderrPath, v => _config.PreLaunchStderrPath = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch timeout as a string.
        /// </summary>
        public string PreLaunchTimeoutSeconds
        {
            get => _config.PreLaunchTimeoutSeconds;
            set => Set(() => _config.PreLaunchTimeoutSeconds, v => _config.PreLaunchTimeoutSeconds = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch retry attempts as a string.
        /// </summary>
        public string PreLaunchRetryAttempts
        {
            get => _config.PreLaunchRetryAttempts;
            set => Set(() => _config.PreLaunchRetryAttempts, v => _config.PreLaunchRetryAttempts = v, value);
        }

        /// <summary>
        /// Gets or sets pre-launch ignore failure as a bool.
        /// </summary>
        public bool PreLaunchIgnoreFailure
        {
            get => _config.PreLaunchIgnoreFailure;
            set => Set(() => _config.PreLaunchIgnoreFailure, v => _config.PreLaunchIgnoreFailure = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the manager application is available.
        /// </summary>
        public bool IsManagerAppAvailable
        {
            get => _isManagerAppAvailable;
            set => Set(() => _isManagerAppAvailable, v => _isManagerAppAvailable = v, value);
        }

        /// <summary>
        /// Gets or sets post-launch executable path as a string.
        /// </summary>
        public string PostLaunchExecutablePath
        {
            get => _config.PostLaunchExecutablePath;
            set => Set(() => _config.PostLaunchExecutablePath, v => _config.PostLaunchExecutablePath = v, value);
        }

        /// <summary>
        /// Gets or sets post-launch startup directory as a string.
        /// </summary>
        public string PostLaunchStartupDirectory
        {
            get => _config.PostLaunchStartupDirectory;
            set => Set(() => _config.PostLaunchStartupDirectory, v => _config.PostLaunchStartupDirectory = v, value);
        }

        /// <summary>
        /// Gets or sets post-launch parameters as a string.
        /// </summary>
        public string PostLaunchParameters
        {
            get => _config.PostLaunchParameters;
            set => Set(() => _config.PostLaunchParameters, v => _config.PostLaunchParameters = v, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug logs are enabled.
        /// </summary>
        public bool EnableDebugLogs
        {
            get => _config.EnableDebugLogs;
            set => Set(() => _config.EnableDebugLogs, v => _config.EnableDebugLogs = v, value);
        }

        /// <summary>
        /// Gets or sets pre-stop executable path as a string.
        /// </summary>
        public string PreStopExecutablePath
        {
            get => _config.PreStopExecutablePath;
            set => Set(() => _config.PreStopExecutablePath, v => _config.PreStopExecutablePath = v, value);
        }

        /// <summary>
        /// Gets or sets pre-stop startup directory as a string.
        /// </summary>
        public string PreStopStartupDirectory
        {
            get => _config.PreStopStartupDirectory;
            set => Set(() => _config.PreStopStartupDirectory, v => _config.PreStopStartupDirectory = v, value);
        }

        /// <summary>
        /// Gets or sets pre-stop parameters as a string.
        /// </summary>
        public string PreStopParameters
        {
            get => _config.PreStopParameters;
            set => Set(() => _config.PreStopParameters, v => _config.PreStopParameters = v, value);
        }

        /// <summary>
        /// Gets or sets pre-stop timeout as a string.
        /// </summary>
        public string PreStopTimeoutSeconds
        {
            get => _config.PreStopTimeoutSeconds;
            set => Set(() => _config.PreStopTimeoutSeconds, v => _config.PreStopTimeoutSeconds = v, value);
        }

        /// <summary>
        /// Gets or sets pre-stop log as error as a bool.
        /// </summary>
        public bool PreStopLogAsError
        {
            get => _config.PreStopLogAsError;
            set => Set(() => _config.PreStopLogAsError, v => _config.PreStopLogAsError = v, value);
        }

        /// <summary>
        /// Gets or sets post-stop executable path as a string.
        /// </summary>
        public string PostStopExecutablePath
        {
            get => _config.PostStopExecutablePath;
            set => Set(() => _config.PostStopExecutablePath, v => _config.PostStopExecutablePath = v, value);
        }

        /// <summary>
        /// Gets or sets post-stop startup directory as a string.
        /// </summary>
        public string PostStopStartupDirectory
        {
            get => _config.PostStopStartupDirectory;
            set => Set(() => _config.PostStopStartupDirectory, v => _config.PostStopStartupDirectory = v, value);
        }

        /// <summary>
        /// Gets or sets post-stop parameters as a string.
        /// </summary>
        public string PostStopParameters
        {
            get => _config.PostStopParameters;
            set => Set(() => _config.PostStopParameters, v => _config.PostStopParameters = v, value);
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
        public IAsyncCommand InstallCommand { get; }

        /// <summary>
        /// Command to uninstall the service.
        /// </summary>
        public IAsyncCommand UninstallCommand { get; }

        /// <summary>
        /// Command to start the service.
        /// </summary>
        public IAsyncCommand StartCommand { get; }

        /// <summary>
        /// Command to stop the service.
        /// </summary>
        public IAsyncCommand StopCommand { get; }

        /// <summary>
        /// Command to restart the service.
        /// </summary>
        public IAsyncCommand RestartCommand { get; }

        /// <summary>
        /// Command to browse and select the failure program path.
        /// </summary>
        public ICommand BrowseFailureProgramPathCommand { get; }

        /// <summary>
        /// Command to browse and select the failure program startup directory.
        /// </summary>
        public ICommand BrowseFailureProgramStartupDirectoryCommand { get; }

        /// <summary>
        /// Command to browse and select the pre-launch executable process path.
        /// </summary>
        public ICommand BrowsePreLaunchProcessPathCommand { get; }

        /// <summary>
        /// Command to browse and select the pre-launch startup directory.
        /// </summary>
        public ICommand BrowsePreLaunchStartupDirectoryCommand { get; }

        /// <summary>
        /// Command to browse and select the pre-launch standard output file path.
        /// </summary>
        public ICommand BrowsePreLaunchStdoutPathCommand { get; }

        /// <summary>
        /// Command to browse and select the pre-launch error output file path.
        /// </summary>
        public ICommand BrowsePreLaunchStderrPathCommand { get; }

        /// <summary>
        /// Command to export XML configuration file.
        /// </summary>
        public IAsyncCommand ExportXmlCommand { get; }

        /// <summary>
        /// Command to export JSON configuration file.
        /// </summary>
        public IAsyncCommand ExportJsonCommand { get; }

        /// <summary>
        /// Command to browse and import an XML configuration file.
        /// </summary>
        public IAsyncCommand ImportXmlCommand { get; }

        /// <summary>
        /// Command to browse and import a JSON configuration file.
        /// </summary>
        public IAsyncCommand ImportJsonCommand { get; }

        /// <summary>
        /// Command to open Servy Manager to manage services.
        /// </summary>
        public IAsyncCommand ManagerCommand { get; }

        /// <summary>
        /// Command to open documentation.
        /// </summary>
        public IAsyncCommand OpenDocumentationCommand { get; }

        /// <summary>
        /// Command to check for updates.
        /// </summary>
        public IAsyncCommand CheckUpdatesCommand { get; }

        /// <summary>
        /// Command to open about dialog.
        /// </summary>
        public IAsyncCommand OpenAboutDialogCommand { get; }

        /// <summary>
        /// Command to clear the form fields.
        /// </summary>
        public IAsyncCommand ClearFormCommand { get; }

        /// <summary>
        /// Command to browse and select the post-launch executable process path.
        /// </summary>
        public ICommand BrowsePostLaunchProcessPathCommand { get; }

        /// <summary>
        /// Command to browse and select the post-launch startup directory.
        /// </summary>
        public ICommand BrowsePostLaunchStartupDirectoryCommand { get; }

        /// <summary>
        /// Command to browse and select the pre-stop executable process path.
        /// </summary>
        public ICommand BrowsePreStopProcessPathCommand { get; }

        /// <summary>
        /// Command to browse and select the pre-stop startup directory.
        /// </summary>
        public ICommand BrowsePreStopStartupDirectoryCommand { get; }

        /// <summary>
        /// Command to browse and select the post-stop executable process path.
        /// </summary>
        public ICommand BrowsePostStopProcessPathCommand { get; }

        /// <summary>
        /// Command to browse and select the post-stop startup directory.
        /// </summary>
        public ICommand BrowsePostStopStartupDirectoryCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class with the specified services.
        /// </summary>
        /// <param name="dialogService">Service to open file and folder dialogs.</param>
        /// <param name="serviceCommands">Service commands to manage Windows services.</param>
        /// <param name="messageBoxService">Service to show message dialogs.</param>
        /// <param name="serviceRepository">Service Repository.</param>
        /// <param name="helpService">Help service.</param>
        /// <param name="appConfig">Application configuration.</param>
        public MainViewModel(
            IFileDialogService dialogService,
            IServiceCommands serviceCommands,
            IMessageBoxService messageBoxService,
            IServiceRepository serviceRepository,
            IHelpService helpService,
            IAppConfiguration appConfig
            )
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            ServiceCommands = serviceCommands ?? throw new ArgumentNullException(nameof(serviceCommands));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _helpService = helpService ?? throw new ArgumentNullException(nameof(helpService));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));

            // Initialize defaults
            ResetToDefaults();

            // Commands
            BrowseProcessPathCommand = new RelayCommand<object>(_ => BrowseProcessPath());
            BrowseStartupDirectoryCommand = new RelayCommand<object>(_ => BrowseStartupDirectory());
            BrowseStdoutPathCommand = new RelayCommand<object>(_ => BrowseStdoutPath());
            BrowseStderrPathCommand = new RelayCommand<object>(_ => BrowseStderrPath());

            InstallCommand = new AsyncCommand(InstallService, _ => !IsBusy, name: nameof(InstallCommand));
            UninstallCommand = new AsyncCommand(UninstallService, _ => !IsBusy, name: nameof(UninstallCommand));
            StartCommand = new AsyncCommand(StartService, _ => !IsBusy, name: nameof(StartCommand));
            StopCommand = new AsyncCommand(StopService, _ => !IsBusy, name: nameof(StopCommand));
            RestartCommand = new AsyncCommand(RestartService, _ => !IsBusy, name: nameof(RestartCommand));

            ManagerCommand = new AsyncCommand(OpenManager, name: nameof(ManagerCommand));

            ExportXmlCommand = new AsyncCommand(ExportXmlConfig, _ => !IsBusy, name: nameof(ExportXmlCommand));
            ExportJsonCommand = new AsyncCommand(ExportJsonConfig, _ => !IsBusy, name: nameof(ExportJsonCommand));
            ImportXmlCommand = new AsyncCommand(ImportXmlConfig, _ => !IsBusy, name: nameof(ImportXmlCommand));
            ImportJsonCommand = new AsyncCommand(ImportJsonConfig, _ => !IsBusy, name: nameof(ImportJsonCommand));

            BrowseFailureProgramPathCommand = new RelayCommand<object>(_ => BrowseFailureProgramPath());
            BrowseFailureProgramStartupDirectoryCommand = new RelayCommand<object>(_ => BrowseFailureProgramStartupDirectory());

            BrowsePreLaunchProcessPathCommand = new RelayCommand<object>(_ => BrowsePreLaunchProcessPath());
            BrowsePreLaunchStartupDirectoryCommand = new RelayCommand<object>(_ => BrowsePreLaunchStartupDirectory());
            BrowsePreLaunchStdoutPathCommand = new RelayCommand<object>(_ => BrowsePreLaunchStdoutPath());
            BrowsePreLaunchStderrPathCommand = new RelayCommand<object>(_ => BrowsePreLaunchStderrPath());

            BrowsePostLaunchProcessPathCommand = new RelayCommand<object>(_ => BrowsePostLaunchProcessPath());
            BrowsePostLaunchStartupDirectoryCommand = new RelayCommand<object>(_ => BrowsePostLaunchStartupDirectory());

            BrowsePreStopProcessPathCommand = new RelayCommand<object>(_ => BrowsePreStopProcessPath());
            BrowsePreStopStartupDirectoryCommand = new RelayCommand<object>(_ => BrowsePreStopStartupDirectory());

            BrowsePostStopProcessPathCommand = new RelayCommand<object>(_ => BrowsePostStopProcessPath());
            BrowsePostStopStartupDirectoryCommand = new RelayCommand<object>(_ => BrowsePostStopStartupDirectory());

            OpenDocumentationCommand = new AsyncCommand(OpenDocumentation, name: nameof(OpenDocumentationCommand));
            CheckUpdatesCommand = new AsyncCommand(CheckUpdatesAsync, name: nameof(CheckUpdatesCommand));
            OpenAboutDialogCommand = new AsyncCommand(OpenAboutDialog, name: nameof(OpenAboutDialogCommand));

            ClearFormCommand = new AsyncCommand(ClearForm);

            IsManagerAppAvailable = _appConfig.IsManagerAppAvailable;
            _appConfig.PropertyChanged += AppConfig_PropertyChanged;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class for design-time data.
        /// </summary>
        public MainViewModel() : this(
            new UI.Design.DesignTimeFileDialogService(),
            new DesignTimeServiceCommands(),
            new UI.Design.DesignTimeMessageBoxService(),
            new UI.Design.DesignTimeServiceRepository(),
            new UI.Design.DesignTimeHelpService(),
            new DesignTimeAppConfig()
            )
        {
            // The body is now intentionally empty. 
            // All dependencies are safely injected via the chained call, bypassing the ArgumentNullException traps.
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// PropertyChanged event handler to capture dynamically updated settings from the application.
        /// </summary>
        private void AppConfig_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(IAppConfiguration.IsManagerAppAvailable))
            {
                IsManagerAppAvailable = ((IAppConfiguration)sender).IsManagerAppAvailable;
            }
        }

        /// <summary>
        /// Resets all fields to their default values.
        /// </summary>
        private void ResetToDefaults()
        {
            ServiceName = string.Empty;
            ServiceDisplayName = string.Empty;
            ServiceDescription = string.Empty;
            ProcessPath = string.Empty;
            StartupDirectory = string.Empty;
            ProcessParameters = string.Empty;
            SelectedStartupType = DefaultStartupType;
            SelectedProcessPriority = DefaultProcessPriority;
            EnableConsoleUI = DefaultEnableConsoleUI;
            EnableSizeRotation = DefaultEnableSizeRotation;
            RotationSize = DefaultRotationSizeMB.ToString();
            MaxRotations = DefaultMaxRotations.ToString();
            EnableDateRotation = DefaultEnableDateRotation;
            SelectedDateRotationType = DefaultDateRotationType;
            StdoutPath = string.Empty;
            StderrPath = string.Empty;
            EnableHealthMonitoring = DefaultEnableHealthMonitoring;
            SelectedRecoveryAction = DefaultRecoveryAction;
            RecoveryOnCleanExit = DefaultRecoveryOnCleanExit;
            UseLocalTimeForRotation = DefaultUseLocalTimeForRotation;
            HeartbeatInterval = DefaultHeartbeatInterval.ToString();
            MaxFailedChecks = DefaultMaxFailedChecks.ToString();
            MaxRestartAttempts = DefaultMaxRestartAttempts.ToString();
            FailureProgramPath = string.Empty;
            FailureProgramStartupDirectory = string.Empty;
            FailureProgramParameters = string.Empty;

            EnvironmentVariables = string.Empty;
            ServiceDependencies = string.Empty;

            RunAsLocalSystem = DefaultRunAsLocalSystem;
            UserAccount = string.Empty;
            Password = string.Empty;
            ConfirmPassword = string.Empty;

            PreLaunchExecutablePath = string.Empty;
            PreLaunchStartupDirectory = string.Empty;
            PreLaunchParameters = string.Empty;
            PreLaunchEnvironmentVariables = string.Empty;
            PreLaunchStdoutPath = string.Empty;
            PreLaunchStderrPath = string.Empty;
            PreLaunchTimeoutSeconds = DefaultPreLaunchTimeoutSeconds.ToString();
            PreLaunchRetryAttempts = DefaultPreLaunchRetryAttempts.ToString();
            PreLaunchIgnoreFailure = DefaultPreLaunchIgnoreFailure;

            PostLaunchExecutablePath = string.Empty;
            PostLaunchStartupDirectory = string.Empty;
            PostLaunchParameters = string.Empty;

            EnableDebugLogs = DefaultEnableDebugLogs;

            StartTimeout = DefaultStartTimeout.ToString();
            StopTimeout = DefaultStopTimeout.ToString();

            // Pre-Stop
            PreStopExecutablePath = string.Empty;
            PreStopStartupDirectory = string.Empty;
            PreStopParameters = string.Empty;
            PreStopTimeoutSeconds = DefaultPreStopTimeoutSeconds.ToString();
            PreStopLogAsError = DefaultPreStopLogAsError;

            // Post-Stop
            PostStopExecutablePath = string.Empty;
            PostStopStartupDirectory = string.Empty;
            PostStopParameters = string.Empty;
        }

        /// <summary>
        /// Compares the current property value with a new value. If they are different, updates 
        /// the property and raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="getValue">A delegate to retrieve the current property value.</param>
        /// <param name="setValue">A delegate to update the property value.</param>
        /// <param name="value">The new value to assign.</param>
        /// <param name="propertyName">The name of the property (automatically populated via CallerMemberName).</param>
        /// <returns>True if the value changed and notification was sent; otherwise false.</returns>
        private bool Set<T>(Func<T> getValue, Action<T> setValue, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(getValue(), value)) return false;

            setValue(value);
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region Dialog Command Handlers

        /// <summary>
        /// Executes a file or folder dialog and assigns the resulting path to a property if the selection is valid.
        /// </summary>
        /// <param name="dialogAction">A function that invokes the specific <see cref="IFileDialogService"/> method (e.g., OpenExecutable or OpenFolder).</param>
        /// <param name="propertySetter">An action that assigns the returned string to the target ViewModel property.</param>
        /// <remarks>
        /// This helper encapsulates the common pattern of checking for null or empty strings 
        /// before updating configuration properties, ensuring that user cancellation of a dialog 
        /// does not overwrite existing valid paths.
        /// </remarks>
        private void BrowseAndAssign(Func<string> dialogAction, Action<string> propertySetter)
        {
            var result = dialogAction();
            if (!string.IsNullOrEmpty(result)) propertySetter(result);
        }

        /// <summary>
        /// Opens a dialog to browse for an executable file and sets <see cref="ProcessPath"/>.
        /// </summary>
        private void BrowseProcessPath() => BrowseAndAssign(_dialogService.OpenExecutable, v => ProcessPath = v);

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="StartupDirectory"/>.
        /// </summary>
        private void BrowseStartupDirectory() => BrowseAndAssign(_dialogService.OpenFolder, v => StartupDirectory = v);

        /// <summary>
        /// Opens a dialog to select a file path for standard output redirection.
        /// </summary>
        private void BrowseStdoutPath() => BrowseAndAssign(() => _dialogService.SaveFile(Strings.Dialog_SelectStdoutFile), v => StdoutPath = v);

        /// <summary>
        /// Opens a dialog to select a file path for standard error redirection.
        /// </summary>
        private void BrowseStderrPath() => BrowseAndAssign(() => _dialogService.SaveFile(Strings.Dialog_SelectStderrFile), v => StderrPath = v);

        /// <summary>
        /// Opens a dialog to browse for a failure program file and sets <see cref="FailureProgramPath"/>.
        /// </summary>
        private void BrowseFailureProgramPath() => BrowseAndAssign(_dialogService.OpenExecutable, v => FailureProgramPath = v);

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="FailureProgramStartupDirectory"/>.
        /// </summary>
        private void BrowseFailureProgramStartupDirectory() => BrowseAndAssign(_dialogService.OpenFolder, v => FailureProgramStartupDirectory = v);

        /// <summary>
        /// Opens a dialog to browse for a pre-launch executable file and sets <see cref="PreLaunchExecutablePath"/>.
        /// </summary>
        private void BrowsePreLaunchProcessPath() => BrowseAndAssign(_dialogService.OpenExecutable, v => PreLaunchExecutablePath = v);

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="PreLaunchStartupDirectory"/>.
        /// </summary>
        private void BrowsePreLaunchStartupDirectory() => BrowseAndAssign(_dialogService.OpenFolder, v => PreLaunchStartupDirectory = v);

        /// <summary>
        /// Opens a dialog to select a file path for pre-launch standard output redirection.
        /// </summary>
        private void BrowsePreLaunchStdoutPath() => BrowseAndAssign(() => _dialogService.SaveFile(Strings.Dialog_SelectStdoutFile), v => PreLaunchStdoutPath = v);

        /// <summary>
        /// Opens a dialog to select a file path for pre-launch standard error redirection.
        /// </summary>
        private void BrowsePreLaunchStderrPath() => BrowseAndAssign(() => _dialogService.SaveFile(Strings.Dialog_SelectStderrFile), v => PreLaunchStderrPath = v);

        /// <summary>
        /// Opens a dialog to browse for a post-launch executable file and sets <see cref="PostLaunchExecutablePath"/>.
        /// </summary>
        private void BrowsePostLaunchProcessPath() => BrowseAndAssign(_dialogService.OpenExecutable, v => PostLaunchExecutablePath = v);

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="PostLaunchStartupDirectory"/>.
        /// </summary>
        private void BrowsePostLaunchStartupDirectory() => BrowseAndAssign(_dialogService.OpenFolder, v => PostLaunchStartupDirectory = v);

        /// <summary>
        /// Opens a dialog to browse for a pre-stop executable file and sets <see cref="PreStopExecutablePath"/>.
        /// </summary>
        private void BrowsePreStopProcessPath() => BrowseAndAssign(_dialogService.OpenExecutable, v => PreStopExecutablePath = v);

        /// <summary>
        /// Opens a dialog to browse for a pre-stop folder and sets <see cref="PreStopStartupDirectory"/>.
        /// </summary>
        private void BrowsePreStopStartupDirectory() => BrowseAndAssign(_dialogService.OpenFolder, v => PreStopStartupDirectory = v);

        /// <summary>
        /// Opens a dialog to browse for a post-stop executable file and sets <see cref="PostStopExecutablePath"/>.
        /// </summary>
        private void BrowsePostStopProcessPath() => BrowseAndAssign(_dialogService.OpenExecutable, v => PostStopExecutablePath = v);

        /// <summary>
        /// Opens a dialog to browse for a post-stop folder and sets <see cref="PostStopStartupDirectory"/>.
        /// </summary>
        private void BrowsePostStopStartupDirectory() => BrowseAndAssign(_dialogService.OpenFolder, v => PostStopStartupDirectory = v);

        #endregion

        #region Service Command Handlers

        /// <summary>
        /// Resets the <see cref="IsBusy"/> property to false.
        /// </summary>
        private void ResetIsBusy()
        {
            IsBusy = false;
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.InstallService"/> with the current property values.
        /// </summary>
        private async Task InstallService(object parameter)
        {
            try
            {
                IsBusy = true;
                await Task.Run(() => ServiceCommands.InstallService(_config));
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.UninstallService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private async Task UninstallService(object parameter)
        {
            try
            {
                IsBusy = true;
                await Task.Run(() => ServiceCommands.UninstallService(ServiceName));
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.StartService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private async Task StartService(object parameter)
        {
            try
            {
                IsBusy = true;
                await Task.Run(() => ServiceCommands.StartService(ServiceName));
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.StopService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private async Task StopService(object parameter)
        {
            try
            {
                IsBusy = true;
                await Task.Run(() => ServiceCommands.StopService(ServiceName));
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.RestartService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private async Task RestartService(object parameter)
        {
            try
            {
                IsBusy = true;
                await Task.Run(() => ServiceCommands.RestartService(ServiceName));
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.OpenManager"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private async Task OpenManager(object parameter)
        {
            await ServiceCommands.OpenManager();
        }

        #endregion

        #region Form Command Handlers

        /// <summary>
        /// Clears all form fields and resets to default values.
        /// </summary>
        private async Task ClearForm(object parameter)
        {
            // Ask for confirmation before clearing everything
            bool confirm = await _messageBoxService.ShowConfirmAsync(Strings.Confirm_ClearAll, AppConfig.Caption);

            if (!confirm)
                return;

            // Clear all fields
            ResetToDefaults();
        }

        #endregion

        #region Import/Export Command Handlers

        /// <summary>
        /// Exports the current service configuration to an XML file selected by the user.
        /// </summary>
        private async Task ExportXmlConfig(object parameter)
        {
            try
            {
                IsBusy = true;
                await ServiceCommands.ExportXmlConfig(ConfirmPassword);
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Exports the current service configuration to a JSON file selected by the user.
        /// </summary>
        private async Task ExportJsonConfig(object parameter)
        {
            try
            {
                IsBusy = true;
                await ServiceCommands.ExportJsonConfig(ConfirmPassword);
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Opens a file dialog to select an XML configuration file for a service,
        /// validates the XML against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the XML is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        private async Task ImportXmlConfig(object parameter)
        {
            try
            {
                IsBusy = true;
                await ServiceCommands.ImportXmlConfig();
            }
            finally
            {
                ResetIsBusy();
            }
        }

        /// <summary>
        /// Opens a file dialog to select an JSON configuration file for a service,
        /// validates the JSON against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the JSON is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        private async Task ImportJsonConfig(object parameter)
        {
            try
            {
                IsBusy = true;
                await ServiceCommands.ImportJsonConfig();
            }
            finally
            {
                ResetIsBusy();
            }
        }

        #endregion

        #region Help/Updates/About Commands

        /// <summary>
        /// Opens the Servy documentation page in the default browser.
        /// </summary>
        private async Task OpenDocumentation(object parameter)
        {
            await _helpService.OpenDocumentation(AppConfig.Caption);
        }

        /// <summary>
        /// Checks for the latest Servy release on GitHub and prompts the user if an update is available.
        /// If a newer version exists, opens the latest release page in the default browser; otherwise shows an informational message.
        /// </summary>
        private async Task CheckUpdatesAsync(object parameter)
        {
            await _helpService.CheckUpdates(AppConfig.Caption);
        }

        /// <summary>
        /// Displays the "About Servy" dialog with version and copyright information.
        /// </summary>
        private async Task OpenAboutDialog(object parameter)
        {
            await _helpService.OpenAboutDialog(
                string.Format(Strings.Text_About,
                    Core.Config.AppConfig.Version,
                    Helper.GetBuiltWithFramework(),
                    DateTime.Now.Year),
                AppConfig.Caption);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Load current service configuration based on windows service name.
        /// </summary>
        /// <param name="serviceName">Service Name.</param>
        /// <returns>A task representing the asynchronous load operation.</returns>
        public async Task LoadServiceConfiguration(string serviceName)
        {
            try
            {
                var dto = await _serviceRepository.GetByNameAsync(serviceName);

                if (dto == null)
                {
                    return;
                }

                BindServiceDtoToModel(dto);
                Logger.Info($"Loaded configuration for service '{serviceName}'");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading configuration for service '{serviceName}'", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <summary>
        /// Populates the ViewModel's properties from a given <see cref="ServiceDto"/> instance.
        /// </summary>
        /// <param name="dto">The <see cref="ServiceDto"/> object containing the service configuration data to bind.</param>
        /// <remarks>
        /// This method maps all DTO fields to the corresponding ViewModel properties.
        /// Some fields (such as environment variables and dependencies) are transformed into 
        /// display-friendly formats using <c>FormatEnvirnomentVariables</c> and <c>FormatServiceDependencies</c>.
        /// 
        /// For security purposes, only <see cref="Password"/> is restored from the DTO.
        /// <see cref="ConfirmPassword"/> is intentionally cleared so that any subsequent
        /// save action requires the user to re-type and re-confirm the password.
        /// </remarks>
        public void BindServiceDtoToModel(ServiceDto dto)
        {
            ServiceName = dto.Name;
            ServiceDisplayName = dto.DisplayName;
            ServiceDescription = dto.Description ?? string.Empty;
            ProcessPath = dto.ExecutablePath;
            StartupDirectory = dto.StartupDirectory ?? string.Empty;
            ProcessParameters = dto.Parameters ?? string.Empty;
            SelectedStartupType = dto.StartupType == null ? DefaultStartupType : (ServiceStartType)dto.StartupType;
            SelectedProcessPriority = dto.Priority == null ? DefaultProcessPriority : (ProcessPriority)dto.Priority;
            EnableConsoleUI = dto.EnableConsoleUI ?? DefaultEnableConsoleUI;
            StdoutPath = dto.StdoutPath ?? string.Empty;
            StderrPath = dto.StderrPath ?? string.Empty;
            EnableSizeRotation = dto.EnableSizeRotation ?? DefaultEnableSizeRotation;
            RotationSize = dto.RotationSize == null ? DefaultRotationSizeMB.ToString() : dto.RotationSize.Value.ToString();
            EnableDateRotation = dto.EnableDateRotation ?? DefaultEnableDateRotation;
            SelectedDateRotationType = dto.DateRotationType == null ? DefaultDateRotationType : (DateRotationType)dto.DateRotationType;
            MaxRotations = dto.MaxRotations == null ? DefaultMaxRotations.ToString() : dto.MaxRotations.Value.ToString();
            UseLocalTimeForRotation = dto.UseLocalTimeForRotation ?? DefaultUseLocalTimeForRotation;
            EnableHealthMonitoring = dto.EnableHealthMonitoring ?? DefaultEnableHealthMonitoring;
            HeartbeatInterval = dto.HeartbeatInterval == null ? DefaultHeartbeatInterval.ToString() : dto.HeartbeatInterval.Value.ToString();
            MaxFailedChecks = dto.MaxFailedChecks == null ? DefaultMaxFailedChecks.ToString() : dto.MaxFailedChecks.Value.ToString();
            SelectedRecoveryAction = dto.RecoveryAction == null ? DefaultRecoveryAction : (RecoveryAction)dto.RecoveryAction;
            RecoveryOnCleanExit = dto.RecoveryOnCleanExit ?? DefaultRecoveryOnCleanExit;
            MaxRestartAttempts = dto.MaxRestartAttempts == null ? DefaultMaxRestartAttempts.ToString() : dto.MaxRestartAttempts.Value.ToString();
            FailureProgramPath = dto.FailureProgramPath ?? string.Empty;
            FailureProgramStartupDirectory = dto.FailureProgramStartupDirectory ?? string.Empty;
            FailureProgramParameters = dto.FailureProgramParameters ?? string.Empty;
            EnvironmentVariables = StringHelper.FormatEnvironmentVariables(dto.EnvironmentVariables) ?? string.Empty;
            ServiceDependencies = StringHelper.FormatServiceDependencies(dto.ServiceDependencies) ?? string.Empty;
            RunAsLocalSystem = dto.RunAsLocalSystem ?? DefaultRunAsLocalSystem;
            UserAccount = dto.UserAccount ?? string.Empty;
            Password = dto.Password ?? string.Empty;
            ConfirmPassword = string.Empty;
            PreLaunchExecutablePath = dto.PreLaunchExecutablePath ?? string.Empty;
            PreLaunchStartupDirectory = dto.PreLaunchStartupDirectory ?? string.Empty;
            PreLaunchParameters = dto.PreLaunchParameters ?? string.Empty;
            PreLaunchEnvironmentVariables = StringHelper.FormatEnvironmentVariables(dto.PreLaunchEnvironmentVariables) ?? string.Empty;
            PreLaunchStdoutPath = dto.PreLaunchStdoutPath ?? string.Empty;
            PreLaunchStderrPath = dto.PreLaunchStderrPath ?? string.Empty;
            PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds == null ? DefaultPreLaunchTimeoutSeconds.ToString() : dto.PreLaunchTimeoutSeconds.Value.ToString();
            PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts == null ? DefaultPreLaunchRetryAttempts.ToString() : dto.PreLaunchRetryAttempts.Value.ToString();
            PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? DefaultPreLaunchIgnoreFailure;

            PostLaunchExecutablePath = dto.PostLaunchExecutablePath ?? string.Empty;
            PostLaunchStartupDirectory = dto.PostLaunchStartupDirectory ?? string.Empty;
            PostLaunchParameters = dto.PostLaunchParameters ?? string.Empty;

            EnableDebugLogs = dto.EnableDebugLogs ?? DefaultEnableDebugLogs;

            StartTimeout = dto.StartTimeout == null ? DefaultStartTimeout.ToString() : dto.StartTimeout.Value.ToString();
            StopTimeout = dto.StopTimeout == null ? DefaultStopTimeout.ToString() : dto.StopTimeout.Value.ToString();

            PreStopExecutablePath = dto.PreStopExecutablePath ?? string.Empty;
            PreStopStartupDirectory = dto.PreStopStartupDirectory ?? string.Empty;
            PreStopParameters = dto.PreStopParameters ?? string.Empty;
            PreStopTimeoutSeconds = dto.PreStopTimeoutSeconds == null ? DefaultPreStopTimeoutSeconds.ToString() : dto.PreStopTimeoutSeconds.Value.ToString();
            PreStopLogAsError = dto.PreStopLogAsError ?? DefaultPreStopLogAsError;

            PostStopExecutablePath = dto.PostStopExecutablePath ?? string.Empty;
            PostStopStartupDirectory = dto.PostStopStartupDirectory ?? string.Empty;
            PostStopParameters = dto.PostStopParameters ?? string.Empty;
        }

        /// <summary>
        /// Converts the ViewModel's current state into a <see cref="ServiceDto"/> object.
        /// </summary>
        /// <returns>
        /// A new <see cref="ServiceDto"/> instance containing the service configuration values from the ViewModel.
        /// </returns>
        /// <remarks>
        /// This method performs the inverse operation of <see cref="BindServiceDtoToModel(ServiceDto)"/>.
        /// It maps all ViewModel properties back into the DTO, utilizing <see cref="ConfigParser.ParseInt"/> 
        /// to ensure malformed numeric inputs are logged as warnings before applying defaults.
        /// </remarks>
        public ServiceDto ModelToServiceDto()
        {
            return new ServiceDto
            {
                Name = ServiceName ?? string.Empty,
                DisplayName = ServiceDisplayName ?? string.Empty,
                Description = ServiceDescription,
                ExecutablePath = ProcessPath ?? string.Empty,
                StartupDirectory = StartupDirectory,
                Parameters = ProcessParameters,
                StartupType = (int)SelectedStartupType,
                Priority = (int)SelectedProcessPriority,

                // Basic UI and Logging
                EnableConsoleUI = EnableConsoleUI,
                StdoutPath = StdoutPath,
                StderrPath = StderrPath,

                // Log Rotation
                EnableSizeRotation = EnableSizeRotation,
                RotationSize = ConfigParser.ParseInt(RotationSize, -1),
                EnableDateRotation = EnableDateRotation,
                DateRotationType = (int)SelectedDateRotationType,
                MaxRotations = ConfigParser.ParseInt(MaxRotations, -1),
                UseLocalTimeForRotation = UseLocalTimeForRotation,

                // Health Monitoring
                EnableHealthMonitoring = EnableHealthMonitoring,
                HeartbeatInterval = ConfigParser.ParseInt(HeartbeatInterval, -1),
                MaxFailedChecks = ConfigParser.ParseInt(MaxFailedChecks, -1),
                RecoveryAction = (int)SelectedRecoveryAction,
                RecoveryOnCleanExit = RecoveryOnCleanExit,
                MaxRestartAttempts = ConfigParser.ParseInt(MaxRestartAttempts, -1),

                // Failure Actions
                FailureProgramPath = FailureProgramPath,
                FailureProgramStartupDirectory = FailureProgramStartupDirectory,
                FailureProgramParameters = FailureProgramParameters,

                // Normalized Strings
                EnvironmentVariables = StringHelper.NormalizeString(EnvironmentVariables),
                ServiceDependencies = StringHelper.NormalizeString(ServiceDependencies),

                // Credentials
                RunAsLocalSystem = RunAsLocalSystem,
                UserAccount = UserAccount,
                Password = Password,

                // Pre-Launch Configuration
                PreLaunchExecutablePath = PreLaunchExecutablePath,
                PreLaunchStartupDirectory = PreLaunchStartupDirectory,
                PreLaunchParameters = PreLaunchParameters,
                PreLaunchEnvironmentVariables = StringHelper.NormalizeString(PreLaunchEnvironmentVariables),
                PreLaunchStdoutPath = PreLaunchStdoutPath,
                PreLaunchStderrPath = PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = ConfigParser.ParseInt(PreLaunchTimeoutSeconds, -1),
                PreLaunchRetryAttempts = ConfigParser.ParseInt(PreLaunchRetryAttempts, -1),
                PreLaunchIgnoreFailure = PreLaunchIgnoreFailure,

                // Post-Launch Configuration
                PostLaunchExecutablePath = PostLaunchExecutablePath,
                PostLaunchStartupDirectory = PostLaunchStartupDirectory,
                PostLaunchParameters = PostLaunchParameters,

                // Debug and Lifecycle Timeouts
                EnableDebugLogs = EnableDebugLogs,
                StartTimeout = ConfigParser.ParseInt(StartTimeout, -1),
                StopTimeout = ConfigParser.ParseInt(StopTimeout, -1),

                // Pre-Stop Configuration
                PreStopExecutablePath = PreStopExecutablePath,
                PreStopStartupDirectory = PreStopStartupDirectory,
                PreStopParameters = PreStopParameters,
                PreStopTimeoutSeconds = ConfigParser.ParseInt(PreStopTimeoutSeconds, -1),
                PreStopLogAsError = PreStopLogAsError,

                // Post-Stop Configuration
                PostStopExecutablePath = PostStopExecutablePath,
                PostStopStartupDirectory = PostStopStartupDirectory,
                PostStopParameters = PostStopParameters,
            };
        }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Public dispose method that clients call.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected virtual dispose method following the standard pattern.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _appConfig.PropertyChanged -= AppConfig_PropertyChanged;
            }

            _disposed = true;
        }

        #endregion
    }
}