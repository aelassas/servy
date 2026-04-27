using Servy.Config;
using Servy.Core.Common;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Models;
using Servy.Resources;
using Servy.UI.Helpers;
using Servy.UI.Services;
using Servy.Validators;
using System.Diagnostics;
using System.IO;
using static Servy.Config.AppConfig;
using AppConfig = Servy.Core.Config.AppConfig;

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

        private readonly Func<ServiceDto?> _modelToServiceDto;
        private readonly Action<ServiceDto> _bindServiceDtoToModel;
        private readonly IServiceManager _serviceManager;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceConfigurationValidator _serviceConfigurationValidator;
        private readonly IFileDialogService _dialogService;
        private readonly IXmlServiceValidator _xmlServiceValidator;
        private readonly IJsonServiceValidator _jsonServiceValidator;
        private readonly IAppConfiguration _appConfig;
        private readonly ICursorService _cursorService;

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
        /// <param name="xmlServiceValidator">XML service validator.</param>
        /// <param name="jsonServiceValidator">JSON service validator.</param>
        /// <param name="appConfig">Application configuration.</param>
        /// <param name="cursorService">Cursor service for managing cursor state during operations.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceManager"/>, <paramref name="messageBoxService"/>, or <paramref name="serviceRepository"/> is <c>null</c>.
        /// </exception>
        public ServiceCommands(
            Func<ServiceDto?> modelToServiceDto,
            Action<ServiceDto> bindServiceDtoToModel,
            IServiceManager serviceManager,
            IMessageBoxService messageBoxService,
            IFileDialogService dialogService,
            IServiceConfigurationValidator serviceConfigurationValidator,
            IXmlServiceValidator xmlServiceValidator,
            IJsonServiceValidator jsonServiceValidator,
            IAppConfiguration? appConfig,
            ICursorService cursorService
            )
        {
            _modelToServiceDto = modelToServiceDto ?? throw new ArgumentNullException(nameof(modelToServiceDto));
            _bindServiceDtoToModel = bindServiceDtoToModel ?? throw new ArgumentNullException(nameof(bindServiceDtoToModel));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceConfigurationValidator = serviceConfigurationValidator ?? throw new ArgumentNullException(nameof(serviceConfigurationValidator));
            _xmlServiceValidator = xmlServiceValidator ?? throw new ArgumentNullException(nameof(xmlServiceValidator));
            _jsonServiceValidator = jsonServiceValidator ?? throw new ArgumentNullException(nameof(jsonServiceValidator));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _cursorService = cursorService ?? throw new ArgumentNullException(nameof(cursorService));
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

            // 1. Build the DTO (The Single Source of Truth for this operation)
            var dto = new ServiceDto
            {
                Name = config.Name ?? string.Empty,
                DisplayName = config.DisplayName ?? string.Empty,
                Description = config.Description,
                ExecutablePath = config.ExecutablePath ?? string.Empty,
                StartupDirectory = config.StartupDirectory,
                Parameters = config.Parameters,
                StartupType = (int)config.StartupType,
                Priority = (int)config.Priority,
                EnableConsoleUI = config.EnableConsoleUI,
                StdoutPath = config.StdoutPath,
                StderrPath = config.StderrPath,
                EnableSizeRotation = config.EnableSizeRotation,
                RotationSize = int.TryParse(config.RotationSize, out var rs) ? rs : -1,
                EnableDateRotation = config.EnableDateRotation,
                DateRotationType = (int)config.DateRotationType,
                MaxRotations = int.TryParse(config.MaxRotations, out var mrn) ? mrn : -1,
                UseLocalTimeForRotation = config.UseLocalTimeForRotation,
                EnableDebugLogs = config.EnableDebugLogs,
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

                // Trust the DTO to handle the identity logic
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

            // 2. Validate the DTO
            if (!await _serviceConfigurationValidator.Validate(dto, wrapperExePath: wrapperExePath, confirmPassword: config.ConfirmPassword))
            {
                return false; // Validation failed, errors shown in MessageBox
            }

            if (_serviceManager.IsServiceInstalled(dto.Name))
            {
                var confirmExists = await _messageBoxService.ShowConfirmAsync(Strings.Msg_ServiceAlreadyExists, Caption);
                if (!confirmExists)
                {
                    return false;
                }
            }

            try
            {
                _cursorService.SetWaitCursor();
                var rotationSizeValue = dto.RotationSize > 0
                    ? (ulong)dto.RotationSize * 1024 * 1024
                    : 0;

                var normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);
                var normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);

                // 3. Map Install Options strictly from the validated DTO
                var options = new InstallServiceOptions
                {
                    ServiceName = dto.Name,
                    DisplayName = dto.DisplayName,
                    Description = dto.Description ?? string.Empty,
                    WrapperExePath = wrapperExePath,
                    RealExePath = dto.ExecutablePath,
                    WorkingDirectory = dto.StartupDirectory,
                    RealArgs = dto.Parameters,
                    StartType = (ServiceStartType)dto.StartupType,
                    ProcessPriority = (ProcessPriority)dto.Priority,
                    EnableConsoleUI = dto.EnableConsoleUI ?? AppConfig.DefaultEnableConsoleUI,
                    Username = dto.UserAccount,
                    Password = dto.Password,

                    StdoutPath = dto.StdoutPath,
                    StderrPath = dto.StderrPath,
                    EnableSizeRotation = dto.EnableSizeRotation ?? AppConfig.DefaultEnableRotation,
                    RotationSizeInBytes = rotationSizeValue,
                    MaxRotations = dto.MaxRotations,
                    EnableDateRotation = dto.EnableDateRotation ?? AppConfig.DefaultEnableDateRotation,
                    DateRotationType = (DateRotationType)dto.DateRotationType,
                    UseLocalTimeForRotation = dto.UseLocalTimeForRotation ?? AppConfig.DefaultUseLocalTimeForRotation,

                    EnableHealthMonitoring = dto.EnableHealthMonitoring ?? AppConfig.DefaultEnableHealthMonitoring,
                    HeartbeatInterval = dto.HeartbeatInterval ?? AppConfig.DefaultHeartbeatInterval,
                    MaxFailedChecks = dto.MaxFailedChecks ?? AppConfig.DefaultMaxFailedChecks,
                    RecoveryAction = (RecoveryAction)dto.RecoveryAction,
                    MaxRestartAttempts = dto.MaxRestartAttempts ?? AppConfig.DefaultMaxRestartAttempts,

                    EnvironmentVariables = normalizedEnvVars,
                    ServiceDependencies = dto.ServiceDependencies,

                    PreLaunchExePath = dto.PreLaunchExecutablePath,
                    PreLaunchWorkingDirectory = dto.PreLaunchStartupDirectory,
                    PreLaunchArgs = dto.PreLaunchParameters,
                    PreLaunchEnvironmentVariables = normalizedPreLaunchEnvVars,
                    PreLaunchStdoutPath = dto.PreLaunchStdoutPath,
                    PreLaunchStderrPath = dto.PreLaunchStderrPath,
                    PreLaunchTimeout = dto.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds,
                    PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts ?? AppConfig.DefaultPreLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? AppConfig.DefaultPreLaunchIgnoreFailure,

                    FailureProgramPath = dto.FailureProgramPath,
                    FailureProgramWorkingDirectory = dto.FailureProgramStartupDirectory,
                    FailureProgramArgs = dto.FailureProgramParameters,

                    PostLaunchExePath = dto.PostLaunchExecutablePath,
                    PostLaunchWorkingDirectory = dto.PostLaunchStartupDirectory,
                    PostLaunchArgs = dto.PostLaunchParameters,

                    StartTimeout = dto.StartTimeout,
                    StopTimeout = dto.StopTimeout,

                    PreStopExePath = dto.PreStopExecutablePath,
                    PreStopWorkingDirectory = dto.PreStopStartupDirectory,
                    PreStopArgs = dto.PreStopParameters,
                    PreStopTimeout = dto.PreStopTimeoutSeconds,
                    PreStopLogAsError = dto.PreStopLogAsError,

                    PostStopExePath = dto.PostStopExecutablePath,
                    PostStopWorkingDirectory = dto.PostStopStartupDirectory,
                    PostStopArgs = dto.PostStopParameters,

                    // Fields not tracked in the persistent DTO fall back to the config
                    EnableDebugLogs = config.EnableDebugLogs
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
            finally
            {
                _cursorService.ResetCursor();
            }
        }

        /// <inheritdoc />
        public async Task<bool> UninstallService(string? serviceName, CancellationToken cancellationToken = default)
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
                _cursorService.SetWaitCursor();
                var res = await _serviceManager.UninstallServiceAsync(serviceName, cancellationToken);
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
            finally
            {
                _cursorService.ResetCursor();
            }
        }

        /// <inheritdoc />
        public Task<bool> StartService(string? serviceName) =>
            ExecuteServiceCommandAsync(
                serviceName,
                (name) => _serviceManager.StartServiceAsync(name, logSuccessfulStart: true),
                Strings.Msg_ServiceStarted,
                checkDisabled: true);

        /// <inheritdoc />
        public Task<bool> StopService(string? serviceName) =>
            ExecuteServiceCommandAsync(
                serviceName,
                (name) => _serviceManager.StopServiceAsync(name, logSuccessfulStop: true),
                Strings.Msg_ServiceStopped,
                checkDisabled: false);

        /// <inheritdoc />
        public Task<bool> RestartService(string? serviceName) =>
            ExecuteServiceCommandAsync(serviceName, _serviceManager.RestartServiceAsync, Strings.Msg_ServiceRestarted, checkDisabled: true);

        ///<inheritdoc/>
        public Task ExportXmlConfig(string? confirmPassword) =>
            ExportConfigAsync(
                confirmPassword,
                () => _dialogService.SaveXml(Strings.SaveFileDialog_XmlTitle),
                ServiceExporter.ExportXml,
                "XML",
                Strings.ExportXml_Success);

        ///<inheritdoc/>
        public Task ExportJsonConfig(string? confirmPassword) =>
            ExportConfigAsync(
                confirmPassword,
                () => _dialogService.SaveJson(Strings.SaveFileDialog_JsonTitle),
                ServiceExporter.ExportJson,
                "JSON",
                Strings.ExportJson_Success);

        ///<inheritdoc/>
        public Task ImportXmlConfig() =>
            ImportConfigAsync(
                _dialogService.OpenXml,
                (content) => { var isValid = _xmlServiceValidator.TryValidate(content, out var err); return (isValid, err); },
                (content) => new XmlServiceSerializer().Deserialize(content),
                "XML",
                Strings.Msg_FailedToLoadXml);

        ///<inheritdoc/>
        public Task ImportJsonConfig() =>
            ImportConfigAsync(
                _dialogService.OpenJson,
                (content) => { var isValid = _jsonServiceValidator.TryValidate(content, out var err); return (isValid, err); },
                (content) => new JsonServiceSerializer().Deserialize(content),
                "JSON",
                Strings.Msg_FailedToLoadJson);

        ///<inheritdoc/>
        public async Task OpenManager()
        {
            if (string.IsNullOrWhiteSpace(_appConfig.ManagerAppPublishPath) || !File.Exists(_appConfig.ManagerAppPublishPath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_ManagerAppNotFound, Caption);
                return;
            }

            var forceFlag = _appConfig.ForceSoftwareRendering ? $" {AppConfig.ForceSoftwareRenderingArg}" : string.Empty;

            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _appConfig.ManagerAppPublishPath,
                    UseShellExecute = true,
                    Arguments = $"\"false\"{forceFlag}", // Pass false to skip splash screen
                }
            })
            {
                if (!process.Start())
                {
                    Logger.Warn($"Failed to start external process {_appConfig.ManagerAppPublishPath}.");
                }
            }

        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Executes a unified service operation, providing a standardized pipeline for validation, 
        /// state checking, exception handling, and user feedback.
        /// </summary>
        /// <param name="serviceName">The unique name of the Windows service to be operated on.</param>
        /// <param name="operation">An asynchronous delegate representing the specific service manager 
        /// action (e.g., Start, Stop, or Restart).</param>
        /// <param name="successMessage">The localized message string to display in a success dialog 
        /// if the operation completes successfully.</param>
        /// <param name="checkDisabled">If <c>true</c>, the method will verify that the service is not 
        /// in a 'Disabled' state before attempting the operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is <c>true</c> 
        /// if the service operation and all associated UI feedback completed successfully; 
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method centralizes the boilerplate logic for service control, ensuring that 
        /// logging and error reporting remain consistent across all management commands.
        /// </remarks>
        private async Task<bool> ExecuteServiceCommandAsync(
            string? serviceName,
            Func<string?, Task<OperationResult>> operation,
            string successMessage,
            bool checkDisabled)
        {
            try
            {
                _cursorService.SetWaitCursor();

                if (!await IsServiceNameValid(serviceName)) return false;

                if (!_serviceManager.IsServiceInstalled(serviceName))
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, Caption);
                    return false;
                }

                if (checkDisabled && _serviceManager.GetServiceStartupType(serviceName) == ServiceStartType.Disabled)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceDisabledError, Caption);
                    return false;
                }

                var res = await operation(serviceName);
                if (res.IsSuccess)
                {
                    await _messageBoxService.ShowInfoAsync(successMessage, Caption);
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
            finally
            {
                _cursorService.ResetCursor();
            }
        }

        /// <summary>
        /// Validates the service name.
        /// </summary>
        /// <param name="serviceName">Service name.</param>
        /// <returns>Returns true if valid; otherwise, false.</returns>
        private async Task<bool> IsServiceNameValid(string? serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                await _messageBoxService.ShowWarningAsync(Strings.Msg_ServiceNameError, Caption);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Standardizes the configuration export pipeline, handling path resolution, security validation, 
        /// and formatted logging for both XML and JSON outputs.
        /// </summary>
        /// <param name="confirmPassword">The password provided by the user to authorize the export of sensitive configuration data.</param>
        /// <param name="getFilePath">A delegate that triggers a file save dialog and returns the selected destination path.</param>
        /// <param name="exportAction">A delegate that performs the actual serialization and file writing for the specific format.</param>
        /// <param name="formatName">A display-friendly name of the format (e.g., "XML", "JSON") used for logging and error reporting.</param>
        /// <param name="successMessage">The localized message to display in the UI upon successful completion.</param>
        /// <returns>A task representing the asynchronous export operation.</returns>
        /// <remarks>
        /// This method ensures that the domain model is mapped to a DTO and strictly validated before any file I/O occurs, 
        /// preventing the export of invalid or inconsistent configurations.
        /// </remarks>
        private async Task ExportConfigAsync(
            string? confirmPassword,
            Func<string?> getFilePath,
            Action<ServiceDto?, string> exportAction,
            string formatName,
            string successMessage)
        {
            try
            {
                var path = getFilePath();
                if (string.IsNullOrEmpty(path)) return;

                var dto = _modelToServiceDto();

                if (!await _serviceConfigurationValidator.Validate(dto: dto, wrapperExePath: null, checkServiceStatus: false, confirmPassword: confirmPassword))
                    return;

                exportAction(dto, path);

                Logger.Info($"Service configuration exported to {formatName} at: {path}");
                await _messageBoxService.ShowInfoAsync(successMessage, Caption);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export service configuration to {formatName}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <summary>
        /// Standardizes the configuration import pipeline, enforcing security guards, format-specific validation, 
        /// and model binding for incoming configuration files.
        /// </summary>
        /// <param name="getFilePath">A delegate that triggers a file open dialog and returns the selected source path.</param>
        /// <param name="validateContent">A delegate that performs raw content validation (e.g., schema or syntax checks) 
        /// and returns a tuple indicating success and any associated error message.</param>
        /// <param name="deserialize">A delegate that converts the raw file content into a <see cref="ServiceDto"/>.</param>
        /// <param name="formatName">A display-friendly name of the format (e.g., "XML", "JSON") used for logging.</param>
        /// <param name="loadErrorMessage">The localized message to display if the file content cannot be mapped to the DTO.</param>
        /// <returns>A task representing the asynchronous import operation.</returns>
        /// <remarks>
        /// The import process follows a multi-stage security gate:
        /// 1. File size verification (via <see cref="ImportGuard"/>).
        /// 2. Raw content/syntax validation.
        /// 3. Logical domain validation (via <see cref="IServiceConfigurationValidator"/>).
        /// Only after passing all gates is the UI model updated.
        /// </remarks>
        private async Task ImportConfigAsync(
            Func<string?> getFilePath,
            Func<string, (bool IsValid, string? ErrorMsg)> validateContent,
            Func<string, ServiceDto?> deserialize,
            string formatName,
            string loadErrorMessage)
        {
            try
            {
                var path = getFilePath();
                if (string.IsNullOrEmpty(path)) return;

                if (!await ImportGuard.ValidateFileSizeAsync(path, _messageBoxService, Caption, AppConfig.MaxConfigFileSizeMB, Strings.Msg_ConfigSizeLimitReached))
                    return;

                var content = await File.ReadAllTextAsync(path);
                var validation = validateContent(content);
                if (!validation.IsValid)
                {
                    await _messageBoxService.ShowErrorAsync(validation.ErrorMsg, Caption);
                    return;
                }

                var dto = deserialize(content);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(loadErrorMessage, Caption);
                    return;
                }

                if (!await _serviceConfigurationValidator.Validate(dto))
                {
                    Logger.Info($"{formatName} File '{path}' not valid.");
                    return;
                }

                Logger.Info($"Service configuration imported from {formatName} at: {path}");
                _bindServiceDtoToModel(dto);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import service configuration from {formatName}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        #endregion

    }
}
