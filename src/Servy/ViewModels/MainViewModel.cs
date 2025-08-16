using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Servy.Constants;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using Servy.Helpers;
using Servy.Models;
using Servy.Resources;
using Servy.Services;
using Servy.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using static Servy.Core.AppConstants;
using static Servy.Helpers.StringHelper;

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
        private readonly IServiceCommands _serviceCommands;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceConfigurationValidator _serviceConfigurationValidator;

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
        /// Command to browse and import an XML configuration file.
        /// </summary>
        public ICommand ImportXmlCommand { get; }

        /// <summary>
        /// Command to browse and import a JSON configuration file.
        /// </summary>
        public ICommand ImportJsonCommand { get; }

        /// <summary>
        /// Command to export XML configuration file.
        /// </summary>
        public ICommand ExportXmlCommand { get; }

        /// <summary>
        /// Command to export JSON configuration file.
        /// </summary>
        public ICommand ExportJsonCommand { get; }

        /// <summary>
        /// Command to open documentation.
        /// </summary>
        public ICommand OpenDocumentation { get; }

        /// <summary>
        /// Command to check for updates.
        /// </summary>
        public ICommand CheckUpdates { get; }

        /// <summary>
        /// Command to open about dialog.
        /// </summary>
        public ICommand OpenAboutDialog { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class with the specified services.
        /// </summary>
        /// <param name="dialogService">Service to open file and folder dialogs.</param>
        /// <param name="serviceCommands">Service commands to manage Windows services.</param>
        /// <param name="messageBoxService">Service to show message dialogs.</param>
        /// <param name="serviceConfigurationValidator">Service to validate inputs.</param>
        public MainViewModel(IFileDialogService dialogService, IServiceCommands serviceCommands, IMessageBoxService messageBoxService, IServiceConfigurationValidator serviceConfigurationValidator)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceCommands = serviceCommands;
            _messageBoxService = messageBoxService;
            _serviceConfigurationValidator = serviceConfigurationValidator;

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
            BrowseProcessPathCommand = new RelayCommand(OnBrowseProcessPath);
            BrowseStartupDirectoryCommand = new RelayCommand(OnBrowseStartupDirectory);
            BrowseStdoutPathCommand = new RelayCommand(OnBrowseStdoutPath);
            BrowseStderrPathCommand = new RelayCommand(OnBrowseStderrPath);
            InstallCommand = new RelayCommand(OnInstallService);
            UninstallCommand = new RelayCommand(OnUninstallService);
            StartCommand = new RelayCommand(OnStartService);
            StopCommand = new RelayCommand(OnStopService);
            RestartCommand = new RelayCommand(OnRestartService);

            ImportXmlCommand = new RelayCommand(OnImportXmlConfig);
            ImportJsonCommand = new RelayCommand(OnImportJsonConfig);
            ExportXmlCommand = new RelayCommand(OnExportXmlConfig);
            ExportJsonCommand = new RelayCommand(OnExportJsonConfig);

            BrowsePreLaunchProcessPathCommand = new RelayCommand(OnBrowsePreLaunchProcessPath);
            BrowsePreLaunchStartupDirectoryCommand = new RelayCommand(OnBrowsePreLaunchStartupDirectory);
            BrowsePreLaunchStdoutPathCommand = new RelayCommand(OnBrowsePreLaunchStdoutPath);
            BrowsePreLaunchStderrPathCommand = new RelayCommand(OnBrowsePreLaunchStderrPath);

            OpenDocumentation = new RelayCommand(OnOpenDocumentation);
            CheckUpdates = new RelayCommand(OnCheckUpdates);
            OpenAboutDialog = new RelayCommand(OnOpenAboutDialog);

            ClearCommand = new RelayCommand(OnClearForm);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class for design-time data.
        /// </summary>
        public MainViewModel() : this(
            new DesignTimeFileDialogService(),
            null,
            null,
            null
            )
        {
            _serviceCommands = new ServiceCommands(
                new ServiceManager(name => new ServiceControllerWrapper(name), new WindowsServiceApi(), new Win32ErrorProvider(), null),
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

        /// <summary>
        /// Opens a dialog to browse for a pre-launch executable file and sets <see cref="PreLaunchExecutablePath"/>.
        /// </summary>
        private void OnBrowsePreLaunchProcessPath()
        {
            var path = _dialogService.OpenExecutable();
            if (!string.IsNullOrEmpty(path)) PreLaunchExecutablePath = path;
        }

        /// <summary>
        /// Opens a dialog to browse for a folder and sets <see cref="PreLaunchStartupDirectory"/>.
        /// </summary>
        private void OnBrowsePreLaunchStartupDirectory()
        {
            var folder = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(folder)) PreLaunchStartupDirectory = folder;
        }

        /// <summary>
        /// Opens a dialog to select a file path for pre-launch standard output redirection.
        /// </summary>
        private void OnBrowsePreLaunchStdoutPath()
        {
            var path = _dialogService.SaveFile("Select standard output file");
            if (!string.IsNullOrEmpty(path)) PreLaunchStdoutPath = path;
        }

        /// <summary>
        /// Opens a dialog to select a file path for pre-launch standard error redirection.
        /// </summary>
        private void OnBrowsePreLaunchStderrPath()
        {
            var path = _dialogService.SaveFile("Select standard error file");
            if (!string.IsNullOrEmpty(path)) PreLaunchStderrPath = path;
        }

        #endregion

        #region Import/Export Command Handlers

        /// <summary>
        /// Opens a file dialog to select an XML configuration file for a service,
        /// validates the XML against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the XML is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        private void OnImportXmlConfig()
        {
            try
            {
                var path = _dialogService.OpenXml();
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                var xml = File.ReadAllText(path);
                if (!XmlServiceValidator.TryValidate(xml, out var errorMsg))
                {
                    _messageBoxService.ShowError(errorMsg, AppConstants.Caption);
                    return;
                }

                var serializer = new XmlServiceSerializer();
                var dto = serializer.Deserialize(xml);
                if (dto == null)
                {
                    _messageBoxService.ShowError(Strings.Msg_FailedToLoadXml, AppConstants.Caption);
                    return;
                }

                string normalizedEnvVars = NormalizeString(dto.EnvironmentVariables);

                string envVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out envVarsErrorMessage))
                {
                    _messageBoxService.ShowError(envVarsErrorMessage, AppConstants.Caption);
                    return;
                }

                string normalizedServiceDependencies = NormalizeString(dto.ServiceDependencies);

                List<string> serviceDependenciesErrors;
                if (!ServiceDependenciesValidator.Validate(dto.ServiceDependencies, out serviceDependenciesErrors))
                {
                    _messageBoxService.ShowError(string.Join("\n", serviceDependenciesErrors), AppConstants.Caption);
                    return;
                }

                string normalizedPreLaunchEnvVars = NormalizeString(dto.PreLaunchEnvironmentVariables);

                string preLaunchEnvVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedPreLaunchEnvVars, out preLaunchEnvVarsErrorMessage))
                {
                    _messageBoxService.ShowError(preLaunchEnvVarsErrorMessage, AppConstants.Caption);
                    return;
                }

                // Map to MainViewModel
                BindServiceDtoToModel(dto);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"{Strings.Msg_UnexpectedConfigLoadError}: {ex.Message}", AppConstants.Caption);
            }
        }

        /// <summary>
        /// Opens a file dialog to select an JSON configuration file for a service,
        /// validates the JSON against the expected <see cref="ServiceDto"/> structure,
        /// and maps the values to the main view model.
        /// Shows an error message if the JSON is invalid, deserialization fails, or any exception occurs.
        /// </summary>
        private void OnImportJsonConfig()
        {
            try
            {
                var path = _dialogService.OpenJson();
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                var json = File.ReadAllText(path);
                if (!JsonServiceValidator.TryValidate(json, out var errorMsg))
                {
                    _messageBoxService.ShowError(errorMsg, AppConstants.Caption);
                    return;
                }

                var dto = JsonConvert.DeserializeObject<ServiceDto>(json);
                if (dto == null)
                {
                    _messageBoxService.ShowError(Strings.Msg_FailedToLoadJson, AppConstants.Caption);
                    return;
                }

                string normalizedEnvVars = NormalizeString(dto.EnvironmentVariables);

                string envVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out envVarsErrorMessage))
                {
                    _messageBoxService.ShowError(envVarsErrorMessage, AppConstants.Caption);
                    return;
                }

                string normalizedServiceDependencies = NormalizeString(dto.ServiceDependencies);

                List<string> serviceDependenciesErrors;
                if (!ServiceDependenciesValidator.Validate(dto.ServiceDependencies, out serviceDependenciesErrors))
                {
                    _messageBoxService.ShowError(string.Join("\n", serviceDependenciesErrors), AppConstants.Caption);
                    return;
                }

                string normalizedPreLaunchEnvVars = NormalizeString(dto.PreLaunchEnvironmentVariables);

                string preLaunchEnvVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedPreLaunchEnvVars, out preLaunchEnvVarsErrorMessage))
                {
                    _messageBoxService.ShowError(preLaunchEnvVarsErrorMessage, AppConstants.Caption);
                    return;
                }

                // Map to MainViewModel
                BindServiceDtoToModel(dto);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"{Strings.Msg_UnexpectedConfigLoadError}: {ex.Message}", AppConstants.Caption);
            }
        }

        /// <summary>
        /// Exports the current service configuration to an XML file selected by the user.
        /// </summary>
        private void OnExportXmlConfig()
        {
            try
            {
                var path = _dialogService.SaveXml(Strings.SaveFileDialog_XmlTitle);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                // Map ServiceConfiguration to ServiceDto
                var dto = ModelToServiceDto();

                // Validation
                if (!_serviceConfigurationValidator.Validate(dto))
                {
                    return;
                }

                // Serialize to XML
                var serializer = new XmlServiceSerializer();
                var xml = new StringWriter();
                new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto)).Serialize(xml, dto);

                // Write to file
                File.WriteAllText(path, xml.ToString());

                // Show success message
                _messageBoxService.ShowInfo(Strings.ExportXml_Success, AppConstants.Caption);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"{Strings.Msg_UnexpectedConfigLoadError}: {ex.Message}", AppConstants.Caption);
            }
        }

        /// <summary>
        /// Exports the current service configuration to a JSON file selected by the user.
        /// </summary>
        private void OnExportJsonConfig()
        {
            try
            {
                var path = _dialogService.SaveJson(Strings.SaveFileDialog_JsonTitle);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                // Map ServiceConfiguration to ServiceDto
                var dto = ModelToServiceDto();

                // Validation
                if (!_serviceConfigurationValidator.Validate(dto))
                {
                    return;
                }

                // Serialize to pretty JSON
                var json = JsonConvert.SerializeObject(dto, Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });

                // Write to file
                File.WriteAllText(path, json);

                // Show success message
                _messageBoxService.ShowInfo(Strings.ExportJson_Success, AppConstants.Caption);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"{Strings.Msg_UnexpectedConfigLoadError}: {ex.Message}", AppConstants.Caption);
            }
        }

        #endregion

        #region Help/Updates/About Commands

        /// <summary>
        /// Opens the Servy documentation page in the default browser.
        /// </summary>
        private void OnOpenDocumentation()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DocumentationLink,
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Checks for the latest Servy release on GitHub and prompts the user if an update is available.
        /// If a newer version exists, opens the latest release page in the default browser; otherwise shows an informational message.
        /// </summary>
        private async void OnCheckUpdates()
        {
            try
            {
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("ServyApp");

                    // Get latest release from GitHub API
                    var url = "https://api.github.com/repos/aelassas/servy/releases/latest";
                    var response = await http.GetStringAsync(url);

                    // Parse JSON response
                    var json = JsonConvert.DeserializeObject<JObject>(response);
                    string tagName = json?["tag_name"]?.ToString();

                    if (string.IsNullOrEmpty(tagName))
                    {
                        _messageBoxService.ShowInfo(Strings.Text_NoUpdate, AppConstants.Caption);
                        return;
                    }

                    // Convert version tag to double (e.g., "v1.2.3" -> 1.23)
                    var latestVersion = Helper.ParseVersion(tagName);
                    var currentVersion = Helper.ParseVersion(Core.AppConstants.Version);

                    if (latestVersion > currentVersion)
                    {
                        var res = _messageBoxService.ShowConfirm(Strings.Text_UpdateAvailable, AppConstants.Caption);

                        if (res)
                        {
                            // Open latest release page
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = LatestReleaseLink,
                                UseShellExecute = true
                            });
                        }
                    }
                    else
                    {
                        _messageBoxService.ShowInfo(Strings.Text_NoUpdate, AppConstants.Caption);
                    }
                }
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError("Failed to check updates: " + ex.Message, AppConstants.Caption);
            }
        }

        /// <summary>
        /// Displays the "About Servy" dialog with version and copyright information.
        /// </summary>
        private void OnOpenAboutDialog()
        {
            _messageBoxService.ShowInfo(Strings.Text_About, AppConstants.Caption);
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
                _config.PreLaunchIgnoreFailure
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
        private void OnClearForm()
        {
            // Ask for confirmation before clearing everything
            bool confirm = _messageBoxService.ShowConfirm(Strings.Confirm_ClearAll, AppConstants.Caption);

            if (!confirm)
                return;

            // Clear all fields
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
        private void BindServiceDtoToModel(ServiceDto dto)
        {
            ServiceName = dto.Name;
            ServiceDescription = dto.Description;
            ProcessPath = dto.ExecutablePath;
            StartupDirectory = dto.StartupDirectory;
            ProcessParameters = dto.Parameters;
            SelectedStartupType = dto.StartupType == null ? ServiceStartType.Automatic : (ServiceStartType)dto.StartupType;
            SelectedProcessPriority = dto.Priority == null ? ProcessPriority.Normal : (ProcessPriority)dto.Priority;
            StdoutPath = dto.StdoutPath;
            StderrPath = dto.StderrPath;
            EnableRotation = dto.EnableRotation ?? false;
            RotationSize = dto.RotationSize == null ? DefaultRotationSize.ToString() : dto.RotationSize.ToString();
            EnableHealthMonitoring = dto.EnableHealthMonitoring ?? false;
            HeartbeatInterval = dto.HeartbeatInterval == null ? DefaultHeartbeatInterval.ToString() : dto.HeartbeatInterval.ToString();
            MaxFailedChecks = dto.MaxFailedChecks == null ? DefaultMaxFailedChecks.ToString() : dto.MaxFailedChecks.ToString();
            SelectedRecoveryAction = dto.RecoveryAction == null ? RecoveryAction.RestartService : (RecoveryAction)dto.RecoveryAction;
            MaxRestartAttempts = dto.MaxRestartAttempts == null ? DefaultMaxRestartAttempts.ToString() : dto.MaxRestartAttempts.ToString();
            EnvironmentVariables = FormatEnvirnomentVariables(dto.EnvironmentVariables);
            ServiceDependencies = FormatServiceDependencies(dto.ServiceDependencies);
            RunAsLocalSystem = dto.RunAsLocalSystem ?? true;
            UserAccount = dto.UserAccount;
            Password = dto.Password;
            ConfirmPassword = dto.Password; // Assuming confirm = password
            PreLaunchExecutablePath = dto.PreLaunchExecutablePath;
            PreLaunchStartupDirectory = dto.PreLaunchStartupDirectory;
            PreLaunchParameters = dto.PreLaunchParameters;
            PreLaunchEnvironmentVariables = FormatEnvirnomentVariables(dto.PreLaunchEnvironmentVariables);
            PreLaunchStdoutPath = dto.PreLaunchStdoutPath;
            PreLaunchStderrPath = dto.PreLaunchStderrPath;
            PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds == null ? DefaultPreLaunchTimeoutSeconds.ToString() : dto.PreLaunchTimeoutSeconds.ToString();
            PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts == null ? DefaultPreLaunchRetryAttempts.ToString() : dto.PreLaunchRetryAttempts.ToString();
            PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? false;
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
        private ServiceDto ModelToServiceDto()
        {
            var dto = new ServiceDto
            {
                Name = ServiceName,
                Description = ServiceDescription,
                ExecutablePath = ProcessPath,
                StartupDirectory = StartupDirectory,
                Parameters = ProcessParameters,
                StartupType = (int)SelectedStartupType,
                Priority = (int)SelectedProcessPriority,
                StdoutPath = StdoutPath,
                StderrPath = StderrPath,
                EnableRotation = EnableRotation,
                RotationSize = int.TryParse(RotationSize, out var rs) ? rs : 0,
                EnableHealthMonitoring = EnableHealthMonitoring,
                HeartbeatInterval = int.TryParse(HeartbeatInterval, out var hi) ? hi : 0,
                MaxFailedChecks = int.TryParse(MaxFailedChecks, out var mf) ? mf : 0,
                RecoveryAction = (int)SelectedRecoveryAction,
                MaxRestartAttempts = int.TryParse(MaxRestartAttempts, out var mr) ? mr : 0,
                EnvironmentVariables = NormalizeString(EnvironmentVariables),
                ServiceDependencies = NormalizeString(ServiceDependencies),
                RunAsLocalSystem = RunAsLocalSystem,
                UserAccount = UserAccount,
                Password = Password,
                PreLaunchExecutablePath = PreLaunchExecutablePath,
                PreLaunchStartupDirectory = PreLaunchStartupDirectory,
                PreLaunchParameters = PreLaunchParameters,
                PreLaunchEnvironmentVariables = NormalizeString(PreLaunchEnvironmentVariables),
                PreLaunchStdoutPath = PreLaunchStdoutPath,
                PreLaunchStderrPath = PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = int.TryParse(PreLaunchTimeoutSeconds, out var pt) ? pt : 30,
                PreLaunchRetryAttempts = int.TryParse(PreLaunchRetryAttempts, out var pr) ? pr : 0,
                PreLaunchIgnoreFailure = PreLaunchIgnoreFailure
            };

            return dto;
        }

        #endregion

    }
}
