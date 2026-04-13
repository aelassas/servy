using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Models;
using Servy.Resources;
using Servy.UI.Services;
using Servy.Validators;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using static Servy.Config.AppConfig;

namespace Servy.Services
{
    /// <summary>
    ///  Concrete implementation of <see cref="IServiceCommands"/> that provides service management commands such as install, uninstall, start, stop, and restart.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ServiceCommands"/> class.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
    public class ServiceCommands : IServiceCommands
    {
        #region Constants

        private const string UnexpectedError = "Unexpected error in service operation.";

        #endregion

        #region Private Fields

        private readonly Func<ServiceDto> _modelToServiceDto;
        private readonly Action<ServiceDto> _bindServiceDtoToModel;
        private readonly IServiceManager _serviceManager;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceConfigurationValidator _serviceConfigurationValidator;
        private readonly IFileDialogService _dialogService;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommands"/> class.
        /// </summary>
        /// <param name="modelToServiceDto">MainViewModel to ServiceDto mapper.</param>
        /// <param name="bindServiceDtoToModel">Binds a service dto MainViewModel.</param>
        /// <param name="serviceManager">The service manager responsible for performing service operations.</param>
        /// <param name="messageBoxService">The message box service used to display messages to the user.</param>
        /// <param name="dialogService">File Dialog service.</param>
        /// <param name="serviceConfigurationValidator">Service to validate inputs.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceManager"/>, <paramref name="messageBoxService"/>, or <paramref name="serviceRepository"/> is <c>null</c>.
        /// </exception>
        public ServiceCommands(
            Func<ServiceDto> modelToServiceDto,
            Action<ServiceDto> bindServiceDtoToModel,
            IServiceManager serviceManager,
            IMessageBoxService messageBoxService,
            IFileDialogService dialogService,
            IServiceConfigurationValidator serviceConfigurationValidator)
        {
            _modelToServiceDto = modelToServiceDto ?? throw new ArgumentNullException(nameof(modelToServiceDto));
            _bindServiceDtoToModel = bindServiceDtoToModel ?? throw new ArgumentNullException(nameof(bindServiceDtoToModel));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceConfigurationValidator = serviceConfigurationValidator ?? throw new ArgumentNullException(nameof(serviceConfigurationValidator));
        }

        #endregion

        #region IServiceCommands Implementation

        /// <inheritdoc />
        public async Task<bool> InstallService(ServiceConfiguration config)
        {
            var wrapperExePath = AppConfig.GetServyUIServicePath();

            if (!File.Exists(wrapperExePath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidWrapperExePath, Caption);
                return false;
            }

            // Build DTO
            var dto = new ServiceDto
            {
                Name = config.Name,
                DisplayName = config.DisplayName,
                Description = config.Description,
                ExecutablePath = config.ExecutablePath,
                StartupDirectory = config.StartupDirectory,
                Parameters = config.Parameters,
                StartupType = (int)config.StartupType,
                Priority = (int)config.Priority,
                StdoutPath = config.StdoutPath,
                StderrPath = config.StderrPath,
                EnableRotation = config.EnableSizeRotation,
                RotationSize = int.TryParse(config.RotationSize, out var rs) ? rs : -1,
                EnableDateRotation = config.EnableDateRotation,
                DateRotationType = (int)config.DateRotationType,
                MaxRotations = int.TryParse(config.MaxRotations, out var mrn) ? mrn : -1,
                UseLocalTimeForRotation = config.UseLocalTimeForRotation,
                EnableHealthMonitoring = config.EnableHealthMonitoring,
                HeartbeatInterval = int.TryParse(config.HeartbeatInterval, out var hi) ? hi : -1,
                MaxFailedChecks = int.TryParse(config.MaxFailedChecks, out var mf) ? mf : -1,
                RecoveryAction = (int)config.RecoveryAction,
                MaxRestartAttempts = int.TryParse(config.MaxRestartAttempts, out var mr) ? mr : -1,
                FailureProgramPath = config.FailureProgramPath,
                FailureProgramStartupDirectory = config.FailureProgramStartupDirectory,
                FailureProgramParameters = config.FailureProgramParameters,
                EnvironmentVariables = config.EnvironmentVariables,
                ServiceDependencies = config.ServiceDependencies,
                RunAsLocalSystem = config.RunAsLocalSystem,
                UserAccount = config.RunAsLocalSystem ? null : config.UserAccount,
                Password = config.RunAsLocalSystem ? null : config.Password,
                PreLaunchExecutablePath = config.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = config.PreLaunchStartupDirectory,
                PreLaunchParameters = config.PreLaunchParameters,
                PreLaunchEnvironmentVariables = config.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = config.PreLaunchStdoutPath,
                PreLaunchStderrPath = config.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = int.TryParse(config.PreLaunchTimeoutSeconds, out var pt) ? pt : -1,
                PreLaunchRetryAttempts = int.TryParse(config.PreLaunchRetryAttempts, out var pra) ? pra : -1,
                PreLaunchIgnoreFailure = config.PreLaunchIgnoreFailure,

                PostLaunchExecutablePath = config.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = config.PostLaunchStartupDirectory,
                PostLaunchParameters = config.PostLaunchParameters,

                StartTimeout = int.TryParse(config.StartTimeout, out var st) ? st : -1,
                StopTimeout = int.TryParse(config.StopTimeout, out var sot) ? sot : -1,

                // Pre-Stop
                PreStopExecutablePath = config.PreStopExecutablePath,
                PreStopStartupDirectory = config.PreStopStartupDirectory,
                PreStopParameters = config.PreStopParameters,
                PreStopTimeoutSeconds = int.TryParse(config.PreStopTimeoutSeconds, out var pst) ? pst : -1,
                PreStopLogAsError = config.PreStopLogAsError,

                // Post-Stop
                PostStopExecutablePath = config.PostStopExecutablePath,
                PostStopStartupDirectory = config.PostStopStartupDirectory,
                PostStopParameters = config.PostStopParameters,
            };

            // Validate
            if (!await _serviceConfigurationValidator.Validate(dto, wrapperExePath: wrapperExePath, confirmPassword: config.ConfirmPassword))
            {
                return false; // Validation failed, errors shown in MessageBox
            }

            if (_serviceManager.IsServiceInstalled(dto.Name))
            {
                var res = await _messageBoxService.ShowConfirmAsync(Strings.Msg_ServiceAlreadyExists, Caption);

                if (!res)
                {
                    return false;
                }
            }

            try
            {
                var rotationSizeValue = dto.RotationSize > 0
                    ? (ulong)dto.RotationSize * 1024 * 1024
                    : 0;

                var normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);
                var normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);

                var finalUserAccount = config.RunAsLocalSystem ? null : config.UserAccount;
                var finalPassword = config.RunAsLocalSystem ? null : config.Password;

                var options = new InstallServiceOptions
                {
                    ServiceName = config.Name,
                    Description = config.Description,
                    WrapperExePath = wrapperExePath,
                    RealExePath = config.ExecutablePath,
                    WorkingDirectory = config.StartupDirectory,
                    RealArgs = config.Parameters,
                    StartType = config.StartupType,
                    ProcessPriority = config.Priority,
                    StdoutPath = config.StdoutPath,
                    StderrPath = config.StderrPath,
                    EnableSizeRotation = config.EnableSizeRotation,
                    RotationSizeInBytes = rotationSizeValue,
                    UseLocalTimeForRotation = config.UseLocalTimeForRotation,
                    EnableHealthMonitoring = config.EnableHealthMonitoring,
                    HeartbeatInterval = dto.HeartbeatInterval ?? AppConfig.DefaultHeartbeatInterval,
                    MaxFailedChecks = dto.MaxFailedChecks ?? AppConfig.DefaultMaxFailedChecks,
                    RecoveryAction = config.RecoveryAction,
                    MaxRestartAttempts = dto.MaxRestartAttempts ?? AppConfig.DefaultMaxRestartAttempts,
                    EnvironmentVariables = normalizedEnvVars,
                    ServiceDependencies = config.ServiceDependencies,
                    Username = finalUserAccount,
                    Password = finalPassword,

                    PreLaunchExePath = config.PreLaunchExecutablePath,
                    PreLaunchWorkingDirectory = config.PreLaunchStartupDirectory,
                    PreLaunchArgs = config.PreLaunchParameters,
                    PreLaunchEnvironmentVariables = normalizedPreLaunchEnvVars,
                    PreLaunchStdoutPath = config.PreLaunchStdoutPath,
                    PreLaunchStderrPath = config.PreLaunchStderrPath,
                    PreLaunchTimeout = dto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds,
                    PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = config.PreLaunchIgnoreFailure,

                    FailureProgramPath = config.FailureProgramPath,
                    FailureProgramWorkingDirectory = config.FailureProgramStartupDirectory,
                    FailureProgramArgs = config.FailureProgramParameters,

                    PostLaunchExePath = config.PostLaunchExecutablePath,
                    PostLaunchWorkingDirectory = config.PostLaunchStartupDirectory,
                    PostLaunchArgs = config.PostLaunchParameters,

                    EnableDebugLogs = config.EnableDebugLogs,
                    DisplayName = config.DisplayName,
                    MaxRotations = dto.MaxRotations,
                    EnableDateRotation = config.EnableDateRotation,
                    DateRotationType = config.DateRotationType,
                    StartTimeout = dto.StartTimeout,
                    StopTimeout = dto.StopTimeout,

                    PreStopExePath = config.PreStopExecutablePath,
                    PreStopWorkingDirectory = config.PreStopStartupDirectory,
                    PreStopArgs = config.PreStopParameters,
                    PreStopTimeout = dto.PreStopTimeoutSeconds,
                    PreStopLogAsError = config.PreStopLogAsError,

                    PostStopExePath = config.PostStopExecutablePath,
                    PostStopWorkingDirectory = config.PostStopStartupDirectory,
                    PostStopArgs = config.PostStopParameters
                };

                var res = await _serviceManager.InstallServiceAsync(options);

                if (res.IsSuccess)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceInstalled, Caption);
                    return true;
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                    return false;
                }
            }
            catch (UnauthorizedAccessException)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_AdminRightsRequired, Caption);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(UnexpectedError, ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UninstallService(string serviceName)
        {
            if (!await IsServiceNameValid(serviceName))
            {
                return false;
            }

            var exists = _serviceManager.IsServiceInstalled(serviceName);
            if (!exists)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, Caption);
                return false;
            }

            try
            {
                var res = await _serviceManager.UninstallServiceAsync(serviceName);
                if (res.IsSuccess)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceRemoved, Caption);
                    return true;
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                    return false;
                }
            }
            catch (UnauthorizedAccessException)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_AdminRightsRequired, Caption);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(UnexpectedError, ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> StartService(string serviceName)
        {
            try
            {
                if (!await IsServiceNameValid(serviceName))
                {
                    return false;
                }

                var exists = _serviceManager.IsServiceInstalled(serviceName);
                if (!exists)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, Caption);
                    return false;
                }

                var startupType = _serviceManager.GetServiceStartupType(serviceName);
                if (startupType == ServiceStartType.Disabled)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceDisabledError, Caption);
                    return false;
                }

                var res = await _serviceManager.StartServiceAsync(serviceName);
                if (res.IsSuccess)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceStarted, Caption);
                    return true;
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(UnexpectedError, ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> StopService(string serviceName)
        {
            try
            {
                if (!await IsServiceNameValid(serviceName))
                {
                    return false;
                }

                var exists = _serviceManager.IsServiceInstalled(serviceName);
                if (!exists)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, Caption);
                    return false;
                }

                var res = await _serviceManager.StopServiceAsync(serviceName);
                if (res.IsSuccess)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceStopped, Caption);
                    return true;
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(UnexpectedError, ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RestartService(string serviceName)
        {
            try
            {
                if (!await IsServiceNameValid(serviceName))
                {
                    return false;
                }

                var exists = _serviceManager.IsServiceInstalled(serviceName);
                if (!exists)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, Caption);
                    return false;
                }

                var startupType = _serviceManager.GetServiceStartupType(serviceName);
                if (startupType == ServiceStartType.Disabled)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceDisabledError, Caption);
                    return false;
                }

                var res = await _serviceManager.RestartServiceAsync(serviceName);
                if (res.IsSuccess)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceRestarted, Caption);
                    return true;
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(UnexpectedError, ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                return false;
            }
        }

        ///<inheritdoc/>
        public async Task ExportXmlConfig(string confirmPassword)
        {
            try
            {
                var path = _dialogService.SaveXml(Strings.SaveFileDialog_XmlTitle);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                // Map ServiceConfiguration to ServiceDto
                var dto = _modelToServiceDto();

                // Validation
                if (!await _serviceConfigurationValidator.Validate(dto: dto, wrapperExePath: null, checkServiceStatus: false, confirmPassword: confirmPassword))
                {
                    return;
                }

                // Serialize to XML and save to file
                ServiceExporter.ExportXml(dto, path);

                // Show success message
                Logger.Info($"Service configuration exported to XML at: {path}");
                await _messageBoxService.ShowInfoAsync(Strings.ExportXml_Success, Caption);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export service configuration to XML.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        ///<inheritdoc/>
        public async Task ExportJsonConfig(string confirmPassword)
        {
            try
            {
                var path = _dialogService.SaveJson(Strings.SaveFileDialog_JsonTitle);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                // Map ServiceConfiguration to ServiceDto
                var dto = _modelToServiceDto();

                // Validation
                if (!await _serviceConfigurationValidator.Validate(dto: dto, wrapperExePath: null, checkServiceStatus: false, confirmPassword: confirmPassword))
                {
                    return;
                }

                // Serialize to pretty JSON and save to file
                ServiceExporter.ExportJson(dto, path);

                // Show success message
                Logger.Info($"Service configuration exported to JSON at: {path}");
                await _messageBoxService.ShowInfoAsync(Strings.ExportJson_Success, Caption);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export service configuration to JSON.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        ///<inheritdoc/>
        public async Task ImportXmlConfig()
        {
            try
            {
                var path = _dialogService.OpenXml();
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (!await ValidateFileSize(path))
                {
                    return;
                }

                var xml = File.ReadAllText(path);
                if (!XmlServiceValidator.TryValidate(xml, out var errorMsg))
                {
                    await _messageBoxService.ShowErrorAsync(errorMsg, Caption);
                    return;
                }

                var serializer = new XmlServiceSerializer();
                var dto = serializer.Deserialize(xml);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_FailedToLoadXml, Caption);
                    return;
                }

                var isValid = await _serviceConfigurationValidator.Validate(dto);

                if (!isValid)
                {
                    // _serviceConfigurationValidator already shows the errors
                    Logger.Info($"XML File '{path}' not valid.");
                    return;
                }

                // Map to MainViewModel
                Logger.Info($"Service configuration imported from XML at: {path}");
                _bindServiceDtoToModel(dto);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import service configuration from XML.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        ///<inheritdoc/>
        public async Task ImportJsonConfig()
        {
            try
            {
                var path = _dialogService.OpenJson();
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (!await ValidateFileSize(path))
                {
                    return;
                }

                var json = File.ReadAllText(path);
                if (!JsonServiceValidator.TryValidate(json, out var errorMsg))
                {
                    await _messageBoxService.ShowErrorAsync(errorMsg, Caption);
                    return;
                }

                var dto = JsonConvert.DeserializeObject<ServiceDto>(json, JsonSecurity.UntrustedDataSettings);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_FailedToLoadJson, Caption);
                    return;
                }

                var isValid = await _serviceConfigurationValidator.Validate(dto);

                if (!isValid)
                {
                    // _serviceConfigurationValidator already shows the errors
                    Logger.Info($"JSON File '{path}' not valid.");
                    return;
                }

                // Map to MainViewModel
                Logger.Info($"Service configuration imported from JSON at: {path}");
                _bindServiceDtoToModel(dto);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import service configuration from JSON.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        ///<inheritdoc/>
        public async Task OpenManager()
        {
            var app = (App)Application.Current;

            if (string.IsNullOrWhiteSpace(app.ManagerAppPublishPath) || !File.Exists(app.ManagerAppPublishPath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_ManagerAppNotFound, Caption);
                return;
            }

            var forceFlag = app.ForceSoftwareRendering ? $" {AppConfig.ForceSoftwareRenderingArg}" : string.Empty;

            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = app.ManagerAppPublishPath,
                    UseShellExecute = true,
                    Arguments = $"\"false\"{forceFlag}", // Pass false to skip splash screen
                }
            })
            {
                process.Start();
            }

        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Validates the service name.
        /// </summary>
        /// <param name="serviceName">Service name.</param>
        /// <returns>Returns true if valid; otherwise, false.</returns>
        private async Task<bool> IsServiceNameValid(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                await _messageBoxService.ShowWarningAsync(Strings.Msg_ServiceNameError, Caption);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the configuration file exists and does not exceed the maximum allowed size.
        /// </summary>
        /// <param name="path">The relative or absolute path to the configuration file.</param>
        /// <returns>
        /// <c>true</c> if the file exists and its size is within the limit defined by 
        /// <see cref="AppConfig.MaxConfigFileSizeMB"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method performs path canonicalization to resolve relative segments and 
        /// catch illegal path characters before attempting filesystem operations.
        /// </remarks>
        private async Task<bool> ValidateFileSize(string path)
        {
            string fullPath;
            try
            {
                // Resolve absolute path to handle ".." segments and validate characters
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                Logger.Error($"Invalid path provided for file size validation: {path}", ex);
                return false;
            }

            var fileInfo = new FileInfo(fullPath);

            // 1. Existence Check
            if (!fileInfo.Exists)
            {
                var errorMsg = $"[Import] File not found: {fullPath}";
                Logger.Error(errorMsg);
                await _messageBoxService.ShowErrorAsync(errorMsg, Caption);
                return false;
            }

            // 2. Size Guard (Safety threshold against large/malicious files)
            // Cast to long to prevent integer overflow during the byte calculation
            if (fileInfo.Length > (long)AppConfig.MaxConfigFileSizeMB * 1024 * 1024)
            {
                // Use string.Format if your resource supports the filename placeholder
                var errorMsg = string.Format(Strings.Msg_ConfigSizeLimitReached, fullPath);
                Logger.Error(errorMsg);
                await _messageBoxService.ShowErrorAsync(errorMsg, Caption);
                return false;
            }

            return true;
        }

        #endregion

    }
}
