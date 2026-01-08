using Servy.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Models;
using Servy.Resources;
using Servy.Services;
using Servy.UI.Commands;
using Servy.UI.Services;
using Servy.Validators;
using Servy.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static Servy.Core.Config.AppConfig;

namespace Servy.ViewModels
{
    /// <summary>
    /// ViewModel for the main service management UI.
    /// Implements properties, commands, and logic for configuring and managing Windows services
    /// such as install, uninstall, start, stop, and restart.
    /// </summary>
    public partial class MainViewModel : INotifyPropertyChanged
    {
        #region Private Fields

        private readonly ServiceConfiguration _config = new ServiceConfiguration();
        private readonly IFileDialogService _dialogService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceRepository _serviceRepository;
        private readonly IHelpService _helpService;
        private bool _isManagerAppAvailable;

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
        /// Gets or sets the display name of the Windows service. 
        /// </summary>
        public string ServiceDisplayName
        {
            get => _config.DisplayName;
            set
            {
                if (_config.DisplayName != value)
                {
                    _config.DisplayName = value;
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
        /// Gets or sets the start timeout as a string (in seconds).
        /// </summary>
        public string StartTimeout
        {
            get => _config.StartTimeout;
            set { _config.StartTimeout = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the start timeout as a string (in seconds).
        /// </summary>
        public string StopTimeout
        {
            get => _config.StopTimeout;
            set { _config.StopTimeout = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether size-based log rotation is enabled.
        /// </summary>
        public bool EnableSizeRotation
        {
            get => _config.EnableSizeRotation;
            set
            {
                _config.EnableSizeRotation = value;
                OnPropertyChanged(nameof(EnableSizeRotation));
                OnPropertyChanged(nameof(EnableRotation));
            }
        }

        /// <summary>
        /// Gets or sets the log rotation size as a string (in MB).
        /// </summary>
        public string RotationSize
        {
            get => _config.RotationSize;
            set { _config.RotationSize = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether date-based log rotation is enabled.
        /// </summary>
        public bool EnableDateRotation
        {
            get => _config.EnableDateRotation;
            set
            {
                _config.EnableDateRotation = value;
                OnPropertyChanged(nameof(EnableDateRotation));
                OnPropertyChanged(nameof(EnableRotation));
            }
        }

        /// <summary>
        /// Gets a value indicating whether log rotation is enabled.
        /// </summary>
        public bool EnableRotation => EnableSizeRotation || EnableDateRotation;

        /// <summary>
        /// Gets or sets the startup type selected for the service.
        /// </summary>
        public DateRotationType SelectedDateRotationType
        {
            get => _config.DateRotationType;
            set { _config.DateRotationType = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the list of available startup types for services.
        /// </summary>
        public List<DateRotationTypeItem> DateRotationTypes { get; } = new List<DateRotationTypeItem>
        {
            new DateRotationTypeItem { DateRotationType = DateRotationType.Daily, DisplayName = Strings.DateRotationType_Daily},
            new DateRotationTypeItem { DateRotationType = DateRotationType.Weekly, DisplayName = Strings.DateRotationType_Weekly},
            new DateRotationTypeItem { DateRotationType = DateRotationType.Monthly, DisplayName = Strings.DateRotationType_Monthly },
        };

        /// <summary>
        /// Gets or sets the maximum number of rotated log files to keep.
        /// </summary>
        public string MaxRotations
        {
            get => _config.MaxRotations;
            set { _config.MaxRotations = value; OnPropertyChanged(); }
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
        /// Gets or sets failure program path as a string.
        /// </summary>
        public string FailureProgramPath
        {
            get => _config.FailureProgramPath;
            set { _config.FailureProgramPath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets failure program startup directory as a string.
        /// </summary>
        public string FailureProgramStartupDirectory
        {
            get => _config.FailureProgramStartupDirectory;
            set { _config.FailureProgramStartupDirectory = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets failure program parameters as a string.
        /// </summary>
        public string FailureProgramParameters
        {
            get => _config.FailureProgramParameters;
            set { _config.FailureProgramParameters = value; OnPropertyChanged(); }
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

        /// <summary>
        /// Gets or sets run as local system as a bool.
        /// </summary>
        public bool RunAsLocalSystem
        {
            get => _config.RunAsLocalSystem;
            set { _config.RunAsLocalSystem = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets user account as a string.
        /// </summary>
        public string UserAccount
        {
            get => _config.UserAccount;
            set { _config.UserAccount = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets user password as a string.
        /// </summary>
        public string Password
        {
            get => _config.Password;
            set { _config.Password = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets user password confirmation as a string.
        /// </summary>
        public string ConfirmPassword
        {
            get => _config.ConfirmPassword;
            set { _config.ConfirmPassword = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch executable path as a string.
        /// </summary>
        public string PreLaunchExecutablePath
        {
            get => _config.PreLaunchExecutablePath;
            set { _config.PreLaunchExecutablePath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch startup directory as a string.
        /// </summary>
        public string PreLaunchStartupDirectory
        {
            get => _config.PreLaunchStartupDirectory;
            set { _config.PreLaunchStartupDirectory = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch parameters as a string.
        /// </summary>
        public string PreLaunchParameters
        {
            get => _config.PreLaunchParameters;
            set { _config.PreLaunchParameters = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch environment variables as a string.
        /// </summary>
        public string PreLaunchEnvironmentVariables
        {
            get => _config.PreLaunchEnvironmentVariables;
            set { _config.PreLaunchEnvironmentVariables = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch stdout log file path as a string.
        /// </summary>
        public string PreLaunchStdoutPath
        {
            get => _config.PreLaunchStdoutPath;
            set { _config.PreLaunchStdoutPath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch stderr log file path as a string.
        /// </summary>
        public string PreLaunchStderrPath
        {
            get => _config.PreLaunchStderrPath;
            set { _config.PreLaunchStderrPath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch timeout as a string.
        /// </summary>
        public string PreLaunchTimeoutSeconds
        {
            get => _config.PreLaunchTimeoutSeconds;
            set { _config.PreLaunchTimeoutSeconds = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch retry attempts as a string.
        /// </summary>
        public string PreLaunchRetryAttempts
        {
            get => _config.PreLaunchRetryAttempts;
            set { _config.PreLaunchRetryAttempts = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets pre-launch ignore failure as a bool.
        /// </summary>
        public bool PreLaunchIgnoreFailure
        {
            get => _config.PreLaunchIgnoreFailure;
            set { _config.PreLaunchIgnoreFailure = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the manager application is available.
        /// </summary>
        public bool IsManagerAppAvailable
        {
            get => _isManagerAppAvailable;
            set { _isManagerAppAvailable = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets post-launch executable path as a string.
        /// </summary>
        public string PostLaunchExecutablePath
        {
            get => _config.PostLaunchExecutablePath;
            set { _config.PostLaunchExecutablePath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets post-launch startup directory as a string.
        /// </summary>
        public string PostLaunchStartupDirectory
        {
            get => _config.PostLaunchStartupDirectory;
            set { _config.PostLaunchStartupDirectory = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets post-launch parameters as a string.
        /// </summary>
        public string PostLaunchParameters
        {
            get => _config.PostLaunchParameters;
            set { _config.PostLaunchParameters = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug logs are enabled.
        /// </summary>
        public bool EnableDebugLogs
        {
            get => _config.EnableDebugLogs;
            set { _config.EnableDebugLogs = value; OnPropertyChanged(); }
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
        public ICommand OpenDocumentationCommand { get; }

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
        public MainViewModel(IFileDialogService dialogService,
            IServiceCommands serviceCommands,
            IMessageBoxService messageBoxService,
            IServiceRepository serviceRepository,
            IHelpService helpService
            )
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            ServiceCommands = serviceCommands;
            _messageBoxService = messageBoxService;
            _serviceRepository = serviceRepository;
            _helpService = helpService;

            // Initialize defaults
            ServiceName = string.Empty;
            ServiceDescription = string.Empty;
            ProcessPath = string.Empty;
            StartupDirectory = string.Empty;
            ProcessParameters = string.Empty;
            SelectedStartupType = ServiceStartType.Automatic;
            SelectedProcessPriority = ProcessPriority.Normal;
            StartTimeout = DefaultStartTimeout.ToString();
            StopTimeout = DefaultStopTimeout.ToString();
            EnableSizeRotation = false;
            RotationSize = DefaultRotationSize.ToString();
            EnableDateRotation = false;
            SelectedDateRotationType = DateRotationType.Daily;
            MaxRotations = DefaultMaxRotations.ToString();
            SelectedRecoveryAction = RecoveryAction.RestartService;
            HeartbeatInterval = DefaultHeartbeatInterval.ToString();
            MaxFailedChecks = DefaultMaxFailedChecks.ToString();
            MaxRestartAttempts = DefaultMaxRestartAttempts.ToString();

            PreLaunchExecutablePath = string.Empty;
            PreLaunchStartupDirectory = string.Empty;
            PreLaunchParameters = string.Empty;
            PreLaunchEnvironmentVariables = string.Empty;
            PreLaunchStdoutPath = string.Empty;
            PreLaunchStderrPath = string.Empty;
            PreLaunchTimeoutSeconds = DefaultPreLaunchTimeoutSeconds.ToString();
            PreLaunchRetryAttempts = DefaultPreLaunchRetryAttempts.ToString();
            PreLaunchIgnoreFailure = false;

            // Commands
            BrowseProcessPathCommand = new RelayCommand<object>(_ => BrowseProcessPath());
            BrowseStartupDirectoryCommand = new RelayCommand<object>(_ => BrowseStartupDirectory());
            BrowseStdoutPathCommand = new RelayCommand<object>(_ => BrowseStdoutPath());
            BrowseStderrPathCommand = new RelayCommand<object>(_ => BrowseStderrPath());

            InstallCommand = new AsyncCommand(InstallService);
            UninstallCommand = new AsyncCommand(UninstallService);
            StartCommand = new RelayCommand<object>(_ => StartService());
            StopCommand = new RelayCommand<object>(_ => StopService());
            RestartCommand = new RelayCommand<object>(_ => RestartService());

            ManagerCommand = new AsyncCommand(OpenManager);

            ExportXmlCommand = new AsyncCommand(ExportXmlConfig);
            ExportJsonCommand = new AsyncCommand(ExportJsonConfig);
            ImportXmlCommand = new AsyncCommand(ImportXmlConfig);
            ImportJsonCommand = new AsyncCommand(ImportJsonConfig);

            BrowseFailureProgramPathCommand = new RelayCommand<object>(_ => BrowseFailureProgramPath());
            BrowseFailureProgramStartupDirectoryCommand = new RelayCommand<object>(_ => BrowseFailureProgramStartupDirectory());

            BrowsePreLaunchProcessPathCommand = new RelayCommand<object>(_ => BrowsePreLaunchProcessPath());
            BrowsePreLaunchStartupDirectoryCommand = new RelayCommand<object>(_ => BrowsePreLaunchStartupDirectory());
            BrowsePreLaunchStdoutPathCommand = new RelayCommand<object>(_ => BrowsePreLaunchStdoutPath());
            BrowsePreLaunchStderrPathCommand = new RelayCommand<object>(_ => BrowsePreLaunchStderrPath());

            BrowsePostLaunchProcessPathCommand = new RelayCommand<object>(_ => BrowsePostLaunchProcessPath());
            BrowsePostLaunchStartupDirectoryCommand = new RelayCommand<object>(_ => BrowsePostLaunchStartupDirectory());

            OpenDocumentationCommand = new RelayCommand<object>(_ => OpenDocumentation());
            CheckUpdatesCommand = new AsyncCommand(CheckUpdatesAsync);
            OpenAboutDialogCommand = new AsyncCommand(OpenAboutDialog);

            ClearFormCommand = new AsyncCommand(ClearForm);

            var app = (App)Application.Current;
            IsManagerAppAvailable = app.IsManagerAppAvailable;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class for design-time data.
        /// </summary>
        public MainViewModel() : this(
            new DesignTimeFileDialogService(),
            null,
            null,
            null,
            null
            )
        {
            var serviceManager = new ServiceManager(name => new ServiceControllerWrapper(name), new WindowsServiceApi(), new Win32ErrorProvider(), null, new WmiSearcher());
            ServiceCommands = new ServiceCommands(
                ModelToServiceDto,
                BindServiceDtoToModel,
                serviceManager,
                new MessageBoxService(),
                new FileDialogService(),
                new ServiceConfigurationValidator(new MessageBoxService())
            );
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
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        #endregion

        #region Dialog Command Handlers

        /// <summary>
        /// Opens a dialog to browse for an executable file and sets <see cref="ProcessPath"/>.
        /// </summary>
        private void BrowseProcessPath()
        {
            var path = _dialogService.OpenExecutable();
            if (!string.IsNullOrEmpty(path)) ProcessPath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="StartupDirectory"/>.
        /// </summary>
        private void BrowseStartupDirectory()
        {
            var folder = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(folder)) StartupDirectory = folder;
        }

        /// <summary>
        /// Opens a dialog to select a file path for standard output redirection.
        /// </summary>
        private void BrowseStdoutPath()
        {
            var path = _dialogService.SaveFile("Select standard output file");
            if (!string.IsNullOrEmpty(path)) StdoutPath = path;
        }

        /// <summary>
        /// Opens a dialog to select a file path for standard error redirection.
        /// </summary>
        private void BrowseStderrPath()
        {
            var path = _dialogService.SaveFile("Select standard error file");
            if (!string.IsNullOrEmpty(path)) StderrPath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a failure program file and sets <see cref="FailureProgramExecutablePath"/>.
        /// </summary>
        private void BrowseFailureProgramPath()
        {
            var path = _dialogService.OpenExecutable();
            if (!string.IsNullOrEmpty(path)) FailureProgramPath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="FailureProgramStartupDirectory"/>.
        /// </summary>
        private void BrowseFailureProgramStartupDirectory()
        {
            var folder = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(folder)) FailureProgramStartupDirectory = folder;
        }

        /// <summary>
        /// Opens a dialog to browse for a pre-launch executable file and sets <see cref="PreLaunchExecutablePath"/>.
        /// </summary>
        private void BrowsePreLaunchProcessPath()
        {
            var path = _dialogService.OpenExecutable();
            if (!string.IsNullOrEmpty(path)) PreLaunchExecutablePath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="PreLaunchStartupDirectory"/>.
        /// </summary>
        private void BrowsePreLaunchStartupDirectory()
        {
            var folder = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(folder)) PreLaunchStartupDirectory = folder;
        }

        /// <summary>
        /// Opens a dialog to select a file path for pre-launch standard output redirection.
        /// </summary>
        private void BrowsePreLaunchStdoutPath()
        {
            var path = _dialogService.SaveFile("Select standard output file");
            if (!string.IsNullOrEmpty(path)) PreLaunchStdoutPath = path;
        }

        /// <summary>
        /// Opens a dialog to select a file path for pre-launch standard error redirection.
        /// </summary>
        private void BrowsePreLaunchStderrPath()
        {
            var path = _dialogService.SaveFile("Select standard error file");
            if (!string.IsNullOrEmpty(path)) PreLaunchStderrPath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a post-launch executable file and sets <see cref="PostLaunchExecutablePath"/>.
        /// </summary>
        private void BrowsePostLaunchProcessPath()
        {
            var path = _dialogService.OpenExecutable();
            if (!string.IsNullOrEmpty(path)) PostLaunchExecutablePath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="PostLaunchStartupDirectory"/>.
        /// </summary>
        private void BrowsePostLaunchStartupDirectory()
        {
            var folder = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(folder)) PostLaunchStartupDirectory = folder;
        }

        #endregion

        #region Service Command Handlers

        /// <summary>
        /// Calls <see cref="IServiceCommands.InstallService"/> with the current property values.
        /// </summary>
        private async Task InstallService(object parameter)
        {
            await ServiceCommands.InstallService(
                _config.Name,
                _config.Description,
                _config.ExecutablePath,
                _config.StartupDirectory,
                _config.Parameters,
                _config.StartupType,
                _config.Priority,
                _config.StdoutPath,
                _config.StderrPath,
                _config.EnableSizeRotation,
                _config.RotationSize,
                _config.EnableHealthMonitoring,
                _config.HeartbeatInterval,
                _config.MaxFailedChecks,
                _config.RecoveryAction,
                _config.MaxRestartAttempts,
                _config.EnvironmentVariables,
                _config.ServiceDependencies,
                _config.RunAsLocalSystem,
                _config.UserAccount,
                _config.Password,
                _config.ConfirmPassword,
                _config.PreLaunchExecutablePath,
                _config.PreLaunchStartupDirectory,
                _config.PreLaunchParameters,
                _config.PreLaunchEnvironmentVariables,
                _config.PreLaunchStdoutPath,
                _config.PreLaunchStderrPath,
                _config.PreLaunchTimeoutSeconds,
                _config.PreLaunchRetryAttempts,
                _config.PreLaunchIgnoreFailure,
                _config.FailureProgramPath,
                _config.FailureProgramStartupDirectory,
                _config.FailureProgramParameters,
                _config.PostLaunchExecutablePath,
                _config.PostLaunchStartupDirectory,
                _config.PostLaunchParameters,
                _config.EnableDebugLogs,
                _config.DisplayName,
                _config.MaxRotations,
                _config.EnableDateRotation,
                _config.DateRotationType,
                _config.StartTimeout,
                _config.StopTimeout
                );
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.UninstallService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private async Task UninstallService(object parameter)
        {
            await ServiceCommands.UninstallService(ServiceName);
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.StartService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private void StartService()
        {
            ServiceCommands.StartService(ServiceName);
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.StopService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private void StopService()
        {
            ServiceCommands.StopService(ServiceName);
        }

        /// <summary>
        /// Calls <see cref="IServiceCommands.RestartService"/> for the current <see cref="ServiceName"/>.
        /// </summary>
        private void RestartService()
        {
            ServiceCommands.RestartService(ServiceName);
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
            ServiceName = string.Empty;
            ServiceDisplayName = string.Empty;
            ServiceDescription = string.Empty;
            ProcessPath = string.Empty;
            StartupDirectory = string.Empty;
            ProcessParameters = string.Empty;
            SelectedStartupType = ServiceStartType.Automatic;
            SelectedProcessPriority = ProcessPriority.Normal;
            EnableSizeRotation = false;
            RotationSize = DefaultRotationSize.ToString();
            MaxRotations = DefaultMaxRotations.ToString();
            EnableDateRotation = false;
            SelectedDateRotationType = DateRotationType.Daily;
            StdoutPath = string.Empty;
            StderrPath = string.Empty;
            EnableHealthMonitoring = false;
            SelectedRecoveryAction = RecoveryAction.RestartService;
            HeartbeatInterval = DefaultHeartbeatInterval.ToString();
            MaxFailedChecks = DefaultMaxFailedChecks.ToString();
            MaxRestartAttempts = DefaultMaxRestartAttempts.ToString();
            FailureProgramPath = string.Empty;
            FailureProgramStartupDirectory = string.Empty;
            FailureProgramParameters = string.Empty;

            EnvironmentVariables = string.Empty;
            ServiceDependencies = string.Empty;

            RunAsLocalSystem = true;
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
            PreLaunchIgnoreFailure = false;

            PostLaunchExecutablePath = string.Empty;
            PostLaunchStartupDirectory = string.Empty;
            PostLaunchParameters = string.Empty;

            EnableDebugLogs = false;

            StartTimeout = DefaultStartTimeout.ToString();
            StopTimeout = DefaultStopTimeout.ToString();
        }

        #endregion

        #region Import/Export Command Handlers

        /// <summary>
        /// Exports the current service configuration to an XML file selected by the user.
        /// </summary>
        private async Task ExportXmlConfig(object parameter)
        {
            await ServiceCommands.ExportXmlConfig(ConfirmPassword);
        }

        /// <summary>
        /// Exports the current service configuration to a JSON file selected by the user.
        /// </summary>
        private async Task ExportJsonConfig(object parameter)
        {
            await ServiceCommands.ExportJsonConfig(ConfirmPassword);
        }

        /// <summary>
        /// Opens a file dialog to select an XML configuration file for a service,
        /// validates the XML against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the XML is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        private async Task ImportXmlConfig(object parameter)
        {
            await ServiceCommands.ImportXmlConfig();
        }

        /// <summary>
        /// Opens a file dialog to select an JSON configuration file for a service,
        /// validates the JSON against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the JSON is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        private async Task ImportJsonConfig(object parameter)
        {
            await ServiceCommands.ImportJsonConfig();
        }

        #endregion

        #region Help/Updates/About Commands

        /// <summary>
        /// Opens the Servy documentation page in the default browser.
        /// </summary>
        private void OpenDocumentation()
        {
            _helpService.OpenDocumentation();
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

        #region Private Helpers

        /// <summary>
        /// Populates the ViewModel's properties from a given <see cref="ServiceDto"/> instance.
        /// </summary>
        /// <param name="dto">The <see cref="ServiceDto"/> object containing the service configuration data to bind.</param>
        /// <remarks>
        /// This method maps all DTO fields to the corresponding ViewModel properties.
        /// Some fields (such as environment variables and dependencies) are transformed into 
        /// display-friendly formats using <c>FormatEnvirnomentVariables</c> and <c>FormatServiceDependencies</c>.
        /// 
        /// For security purposes, the <see cref="Password"/> and <see cref="ConfirmPassword"/> properties 
        /// are both set to the same value from the DTO. This assumes that when loading an existing service 
        /// configuration, the password is already validated and confirmed.
        /// </remarks>
        public void BindServiceDtoToModel(ServiceDto dto)
        {
            ServiceName = dto.Name;
            ServiceDisplayName = dto.DisplayName;
            ServiceDescription = dto.Description;
            ProcessPath = dto.ExecutablePath;
            StartupDirectory = dto.StartupDirectory;
            ProcessParameters = dto.Parameters;
            SelectedStartupType = dto.StartupType == null ? ServiceStartType.Automatic : (ServiceStartType)dto.StartupType;
            SelectedProcessPriority = dto.Priority == null ? ProcessPriority.Normal : (ProcessPriority)dto.Priority;
            StdoutPath = dto.StdoutPath;
            StderrPath = dto.StderrPath;
            EnableSizeRotation = dto.EnableRotation ?? false;
            RotationSize = dto.RotationSize == null ? DefaultRotationSize.ToString() : dto.RotationSize.ToString();
            EnableDateRotation = dto.EnableDateRotation ?? false;
            SelectedDateRotationType = dto.DateRotationType == null ? DateRotationType.Daily : (DateRotationType)dto.DateRotationType;
            MaxRotations = dto.MaxRotations == null ? DefaultMaxRotations.ToString() : dto.MaxRotations.ToString();
            EnableHealthMonitoring = dto.EnableHealthMonitoring ?? false;
            HeartbeatInterval = dto.HeartbeatInterval == null ? DefaultHeartbeatInterval.ToString() : dto.HeartbeatInterval.ToString();
            MaxFailedChecks = dto.MaxFailedChecks == null ? DefaultMaxFailedChecks.ToString() : dto.MaxFailedChecks.ToString();
            SelectedRecoveryAction = dto.RecoveryAction == null ? RecoveryAction.RestartService : (RecoveryAction)dto.RecoveryAction;
            MaxRestartAttempts = dto.MaxRestartAttempts == null ? DefaultMaxRestartAttempts.ToString() : dto.MaxRestartAttempts.ToString();
            FailureProgramPath = dto.FailureProgramPath;
            FailureProgramStartupDirectory = dto.FailureProgramStartupDirectory;
            FailureProgramParameters = dto.FailureProgramParameters;
            EnvironmentVariables = StringHelper.FormatEnvirnomentVariables(dto.EnvironmentVariables);
            ServiceDependencies = StringHelper.FormatServiceDependencies(dto.ServiceDependencies);
            RunAsLocalSystem = dto.RunAsLocalSystem ?? true;
            UserAccount = dto.UserAccount;
            Password = dto.Password;
            ConfirmPassword = dto.Password; // Assuming confirm = password
            PreLaunchExecutablePath = dto.PreLaunchExecutablePath;
            PreLaunchStartupDirectory = dto.PreLaunchStartupDirectory;
            PreLaunchParameters = dto.PreLaunchParameters;
            PreLaunchEnvironmentVariables = StringHelper.FormatEnvirnomentVariables(dto.PreLaunchEnvironmentVariables);
            PreLaunchStdoutPath = dto.PreLaunchStdoutPath;
            PreLaunchStderrPath = dto.PreLaunchStderrPath;
            PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds == null ? DefaultPreLaunchTimeoutSeconds.ToString() : dto.PreLaunchTimeoutSeconds.ToString();
            PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts == null ? DefaultPreLaunchRetryAttempts.ToString() : dto.PreLaunchRetryAttempts.ToString();
            PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? false;

            PostLaunchExecutablePath = dto.PostLaunchExecutablePath;
            PostLaunchStartupDirectory = dto.PostLaunchStartupDirectory;
            PostLaunchParameters = dto.PostLaunchParameters;

            EnableDebugLogs = dto.EnableDebugLogs ?? false;

            StartTimeout = dto.StartTimeout == null ? DefaultStartTimeout.ToString() : dto.StartTimeout.ToString();
            StopTimeout = dto.StopTimeout == null ? DefaultStopTimeout.ToString() : dto.StopTimeout.ToString();
        }

        /// <summary>
        /// Converts the ViewModel's current state into a <see cref="ServiceDto"/> object.
        /// </summary>
        /// <returns>
        /// A new <see cref="ServiceDto"/> instance containing the service configuration values from the ViewModel.
        /// </returns>
        /// <remarks>
        /// This method performs the inverse operation of <see cref="BindServiceDtoToModel(ServiceDto)"/>.
        /// It maps all ViewModel properties back into the DTO, converting text-based numeric values into integers
        /// and normalizing certain string inputs (such as environment variables and dependencies) into 
        /// semicolon-delimited format suitable for persistence or serialization.
        /// 
        /// If parsing fails for numeric fields, safe defaults are applied (e.g., <c>0</c> or <c>30</c> seconds for timeouts).
        /// </remarks>
        public ServiceDto ModelToServiceDto()
        {
            var dto = new ServiceDto
            {
                Name = ServiceName,
                DisplayName = ServiceDisplayName,
                Description = ServiceDescription,
                ExecutablePath = ProcessPath,
                StartupDirectory = StartupDirectory,
                Parameters = ProcessParameters,
                StartupType = (int)SelectedStartupType,
                Priority = (int)SelectedProcessPriority,
                StdoutPath = StdoutPath,
                StderrPath = StderrPath,
                EnableRotation = EnableSizeRotation,
                RotationSize = int.TryParse(RotationSize, out var rs) ? rs : 0,
                EnableDateRotation = EnableDateRotation,
                DateRotationType = (int)SelectedDateRotationType,
                MaxRotations = int.TryParse(MaxRotations, out var mrs) ? mrs : 0,
                EnableHealthMonitoring = EnableHealthMonitoring,
                HeartbeatInterval = int.TryParse(HeartbeatInterval, out var hi) ? hi : 0,
                MaxFailedChecks = int.TryParse(MaxFailedChecks, out var mf) ? mf : 0,
                RecoveryAction = (int)SelectedRecoveryAction,
                MaxRestartAttempts = int.TryParse(MaxRestartAttempts, out var mr) ? mr : 0,
                FailureProgramPath = FailureProgramPath,
                FailureProgramStartupDirectory = FailureProgramStartupDirectory,
                FailureProgramParameters = FailureProgramParameters,
                EnvironmentVariables = StringHelper.NormalizeString(EnvironmentVariables),
                ServiceDependencies = StringHelper.NormalizeString(ServiceDependencies),
                RunAsLocalSystem = RunAsLocalSystem,
                UserAccount = UserAccount,
                Password = Password,
                PreLaunchExecutablePath = PreLaunchExecutablePath,
                PreLaunchStartupDirectory = PreLaunchStartupDirectory,
                PreLaunchParameters = PreLaunchParameters,
                PreLaunchEnvironmentVariables = StringHelper.NormalizeString(PreLaunchEnvironmentVariables),
                PreLaunchStdoutPath = PreLaunchStdoutPath,
                PreLaunchStderrPath = PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = int.TryParse(PreLaunchTimeoutSeconds, out var pt) ? pt : 30,
                PreLaunchRetryAttempts = int.TryParse(PreLaunchRetryAttempts, out var pr) ? pr : 0,
                PreLaunchIgnoreFailure = PreLaunchIgnoreFailure,

                PostLaunchExecutablePath = PostLaunchExecutablePath,
                PostLaunchStartupDirectory = PostLaunchStartupDirectory,
                PostLaunchParameters = PostLaunchParameters,

                EnableDebugLogs = EnableDebugLogs,

                StartTimeout = int.TryParse(StartTimeout, out var startTimeout) ? startTimeout : DefaultStartTimeout,
                StopTimeout = int.TryParse(StopTimeout, out var stopTimeout) ? stopTimeout : DefaultStopTimeout,
            };

            return dto;
        }

        #endregion

    }
}
