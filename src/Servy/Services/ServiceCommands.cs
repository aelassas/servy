using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using Servy.Helpers;
using Servy.Resources;
using Servy.UI.Services;
using System.Diagnostics;
using System.IO;
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
            _modelToServiceDto = modelToServiceDto;
            _bindServiceDtoToModel = bindServiceDtoToModel;
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _serviceConfigurationValidator = serviceConfigurationValidator;
            _dialogService = dialogService;
        }


        #endregion

        #region IServiceCommands Implementation

        /// <inheritdoc />
        public async Task InstallService(
            string serviceName,
            string serviceDescription,
            string processPath,
            string startupDirectory,
            string processParameters,
            ServiceStartType startupType,
            ProcessPriority processPriority,
            string stdoutPath,
            string stderrPath,
            bool enableRotation,
            string rotationSize,
            bool enableHealthMonitoring,
            string heartbeatInterval,
            string maxFailedChecks,
            RecoveryAction recoveryAction,
            string maxRestartAttempts,
            string environmentVariables,
            string serviceDependencies,
            bool runAsLocalSystem,
            string userAccount,
            string password,
            string confirmPassword,
            string preLaunchExePath,
            string preLaunchWorkingDirectory,
            string preLaunchArgs,
            string preLaunchEnvironmentVariables,
            string preLaunchStdoutPath,
            string preLaunchStderrPath,
            string preLaunchTimeout,
            string preLaunchRetryAttempts,
            bool preLaunchIgnoreFailure
            )
        {
            var wrapperExePath = AppConfig.GetServyUIServicePath();

            if (!File.Exists(wrapperExePath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidWrapperExePath, Caption);
                return;
            }

            // Build DTO
            var dto = new ServiceDto
            {
                Name = serviceName,
                Description = serviceDescription,
                ExecutablePath = processPath,
                StartupDirectory = startupDirectory,
                Parameters = processParameters,
                StartupType = (int)startupType,
                Priority = (int)processPriority,
                StdoutPath = stdoutPath,
                StderrPath = stderrPath,
                EnableRotation = enableRotation,
                RotationSize = int.TryParse(rotationSize, out var rs) ? rs : 1_048_576,
                EnableHealthMonitoring = enableHealthMonitoring,
                HeartbeatInterval = int.TryParse(heartbeatInterval, out var hi) ? hi : 30,
                MaxFailedChecks = int.TryParse(maxFailedChecks, out var mf) ? mf : 3,
                RecoveryAction = (int)recoveryAction,
                MaxRestartAttempts = int.TryParse(maxRestartAttempts, out var mr) ? mr : 3,
                EnvironmentVariables = environmentVariables,
                ServiceDependencies = serviceDependencies,
                RunAsLocalSystem = runAsLocalSystem,
                UserAccount = runAsLocalSystem ? null : userAccount,
                Password = runAsLocalSystem ? null : password,
                PreLaunchExecutablePath = preLaunchExePath,
                PreLaunchStartupDirectory = preLaunchWorkingDirectory,
                PreLaunchParameters = preLaunchArgs,
                PreLaunchEnvironmentVariables = preLaunchEnvironmentVariables,
                PreLaunchStdoutPath = preLaunchStdoutPath,
                PreLaunchStderrPath = preLaunchStderrPath,
                PreLaunchTimeoutSeconds = int.TryParse(preLaunchTimeout, out var pt) ? pt : 30,
                PreLaunchRetryAttempts = int.TryParse(preLaunchRetryAttempts, out var pra) ? pra : 0,
                PreLaunchIgnoreFailure = preLaunchIgnoreFailure
            };

            // Validate
            if (!(await _serviceConfigurationValidator.Validate(dto, wrapperExePath)))
            {
                return; // Validation failed, errors shown in MessageBox
            }

            try
            {
                var rotationSizeValue = int.Parse(rotationSize);
                var heartbeatIntervalValue = int.Parse(heartbeatInterval);
                var maxFailedChecksValue = int.Parse(maxFailedChecks);
                var maxRestartAttemptsValue = int.Parse(maxRestartAttempts);
                var normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);
                var normalizedDeps = StringHelper.NormalizeString(dto.ServiceDependencies);
                var normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);
                var preLaunchTimeoutValue = int.Parse(preLaunchTimeout);
                var preLaunchRetryAttemptsValue = int.Parse(preLaunchRetryAttempts);

                if (runAsLocalSystem)
                {
                    userAccount = null;
                    password = null;
                }

                bool success = await _serviceManager.InstallService(
                    serviceName: serviceName,
                    description: serviceDescription,
                    wrapperExePath: wrapperExePath,
                    realExePath: processPath,
                    workingDirectory: startupDirectory,
                    realArgs: processParameters,
                    startType: startupType,
                    processPriority: processPriority,
                    stdoutPath: stdoutPath,
                    stderrPath: stderrPath,
                    rotationSizeInBytes: rotationSizeValue,
                    heartbeatInterval: heartbeatIntervalValue,
                    maxFailedChecks: maxFailedChecksValue,
                    recoveryAction: recoveryAction,
                    maxRestartAttempts: maxRestartAttemptsValue,
                    environmentVariables: normalizedEnvVars,
                    serviceDependencies: serviceDependencies,
                    username: userAccount,
                    password: password,
                    preLaunchExePath: preLaunchExePath,
                    preLaunchWorkingDirectory: preLaunchWorkingDirectory,
                    preLaunchArgs: preLaunchArgs,
                    preLaunchEnvironmentVariables: normalizedPreLaunchEnvVars,
                    preLaunchStdoutPath: preLaunchStdoutPath,
                    preLaunchStderrPath: preLaunchStderrPath,
                    preLaunchTimeout: preLaunchTimeoutValue,
                    preLaunchRetryAttempts: preLaunchRetryAttemptsValue,
                    preLaunchIgnoreFailure: preLaunchIgnoreFailure
                );


                if (success)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceCreated, Caption);
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                }
            }
            catch (UnauthorizedAccessException)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_AdminRightsRequired, Caption);
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public async Task UninstallService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                await _messageBoxService.ShowWarningAsync(Strings.Msg_ValidationError, Caption);
                return;
            }

            try
            {
                bool success = await _serviceManager.UninstallService(serviceName);
                if (success)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceRemoved, Caption);
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
                }
            }
            catch (UnauthorizedAccessException)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_AdminRightsRequired, Caption);
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public async void StartService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.StartService(serviceName);
                if (success)
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceStarted, Caption);
                else
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public async void StopService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.StopService(serviceName);
                if (success)
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceStopped, Caption);
                else
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public async void RestartService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.RestartService(serviceName);
                if (success)
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceRestarted, Caption);
                else
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        ///<inheritdoc/>
        public async Task ExportXmlConfig()
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
                if (!(await _serviceConfigurationValidator.Validate(dto)))
                {
                    return;
                }

                // Serialize to XML and save to file
                ServiceExporter.ExportXml(dto, path);

                // Show success message
                await _messageBoxService.ShowInfoAsync(Strings.ExportXml_Success, Caption);
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        ///<inheritdoc/>
        public async Task ExportJsonConfig()
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
                if (!(await _serviceConfigurationValidator.Validate(dto)))
                {
                    return;
                }

                // Serialize to pretty JSON and save to file
                ServiceExporter.ExportJson(dto, path);

                // Show success message
                await _messageBoxService.ShowInfoAsync(Strings.ExportJson_Success, Caption);
            }
            catch (Exception)
            {
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

                string normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);

                string envVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out envVarsErrorMessage))
                {
                    await _messageBoxService.ShowErrorAsync(envVarsErrorMessage, Caption);
                    return;
                }

                string normalizedServiceDependencies = StringHelper.NormalizeString(dto.ServiceDependencies);

                List<string> serviceDependenciesErrors;
                if (!ServiceDependenciesValidator.Validate(dto.ServiceDependencies, out serviceDependenciesErrors))
                {
                    await _messageBoxService.ShowErrorAsync(string.Join("\n", serviceDependenciesErrors), Caption);
                    return;
                }

                string normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);

                string preLaunchEnvVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedPreLaunchEnvVars, out preLaunchEnvVarsErrorMessage))
                {
                    await _messageBoxService.ShowErrorAsync(preLaunchEnvVarsErrorMessage, Caption);
                    return;
                }

                // Map to MainViewModel
                _bindServiceDtoToModel(dto);
            }
            catch (Exception)
            {
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

                var json = File.ReadAllText(path);
                if (!JsonServiceValidator.TryValidate(json, out var errorMsg))
                {
                    await _messageBoxService.ShowErrorAsync(errorMsg, Caption);
                    return;
                }

                var dto = JsonConvert.DeserializeObject<ServiceDto>(json);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_FailedToLoadJson, Caption);
                    return;
                }

                string normalizedEnvVars = StringHelper.NormalizeString(dto.EnvironmentVariables);

                string envVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out envVarsErrorMessage))
                {
                    await _messageBoxService.ShowErrorAsync(envVarsErrorMessage, Caption);
                    return;
                }

                string normalizedServiceDependencies = StringHelper.NormalizeString(dto.ServiceDependencies);

                List<string> serviceDependenciesErrors;
                if (!ServiceDependenciesValidator.Validate(dto.ServiceDependencies, out serviceDependenciesErrors))
                {
                    await _messageBoxService.ShowErrorAsync(string.Join("\n", serviceDependenciesErrors), Caption);
                    return;
                }

                string normalizedPreLaunchEnvVars = StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);

                string preLaunchEnvVarsErrorMessage;
                if (!EnvironmentVariablesValidator.Validate(normalizedPreLaunchEnvVars, out preLaunchEnvVarsErrorMessage))
                {
                    await _messageBoxService.ShowErrorAsync(preLaunchEnvVarsErrorMessage, Caption);
                    return;
                }

                // Map to MainViewModel
                _bindServiceDtoToModel(dto);
            }
            catch (Exception)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, Caption);
            }
        }

        ///<inheritdoc/>
        public async Task OpenManager()
        {
            var app = (App)Application.Current;
            var managerAppPath = app.ManagerAppPublishPath;

            if(string.IsNullOrWhiteSpace(app.ManagerAppPublishPath)|| !File.Exists(app.ManagerAppPublishPath))
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_ManagerAppNotFound, Caption);
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = app.ManagerAppPublishPath,
                    UseShellExecute = true,
                }
            };

            process.Start();
        }

        #endregion

    }
}
