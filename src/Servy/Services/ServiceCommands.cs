using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using Servy.Helpers;
using Servy.Resources;
using System.IO;
using System.Runtime.CompilerServices;
using static Servy.Constants.AppConstants;
using CoreHelper = Servy.Core.Helpers.Helper;

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

        private readonly IServiceManager _serviceManager;
        private readonly IMessageBoxService _messageBoxService;
        private readonly ServiceConfigurationValidator _serviceConfigurationValidator;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommands"/> class.
        /// </summary>
        /// <param name="serviceManager">The service manager responsible for performing service operations.</param>
        /// <param name="messageBoxService">The message box service used to display messages to the user.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceManager"/>, <paramref name="messageBoxService"/>, or <paramref name="serviceRepository"/> is <c>null</c>.
        /// </exception>
        public ServiceCommands(IServiceManager serviceManager, IMessageBoxService messageBoxService)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _serviceConfigurationValidator = new ServiceConfigurationValidator(_messageBoxService);
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
#if DEBUG
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{App.ServyServiceExeFileName}.exe");
#else
            var wrapperExePath = Path.Combine(Core.AppConstants.ProgramDataPath, $"{App.ServyServiceExeFileName}.exe");
#endif

            if (!File.Exists(wrapperExePath))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidWrapperExePath, Caption);
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
            if (!_serviceConfigurationValidator.Validate(dto, wrapperExePath))
            {
                return; // Validation failed, errors shown in MessageBox
            }

            try
            {
                var rotationSizeValue = int.Parse(rotationSize);
                var heartbeatIntervalValue = int.Parse(heartbeatInterval);
                var maxFailedChecksValue = int.Parse(maxFailedChecks);
                var maxRestartAttemptsValue = int.Parse(maxRestartAttempts);
                var normalizedEnvVars = Helpers.StringHelper.NormalizeString(dto.EnvironmentVariables);
                var normalizedDeps = Helpers.StringHelper.NormalizeString(dto.ServiceDependencies);
                var normalizedPreLaunchEnvVars = Helpers.StringHelper.NormalizeString(dto.PreLaunchEnvironmentVariables);
                var preLaunchTimeoutValue = int.Parse(preLaunchTimeout);
                var preLaunchRetryAttemptsValue = int.Parse(preLaunchRetryAttempts);

                if (runAsLocalSystem)
                {
                    userAccount = null;
                    password = null;
                }

                bool success = await _serviceManager.InstallService(
                    serviceName,
                    serviceDescription,
                    wrapperExePath,
                    processPath,
                    startupDirectory,
                    processParameters,
                    startupType,
                    processPriority,
                    stdoutPath,
                    stderrPath,
                    rotationSizeValue,
                    heartbeatIntervalValue,
                    maxFailedChecksValue,
                    recoveryAction,
                    maxRestartAttemptsValue,
                    normalizedEnvVars,
                    serviceDependencies,
                    userAccount,
                    password,
                    preLaunchExePath,
                    preLaunchWorkingDirectory,
                    preLaunchArgs,
                    normalizedPreLaunchEnvVars,
                    preLaunchStdoutPath,
                    preLaunchStderrPath,
                    preLaunchTimeoutValue,
                    preLaunchRetryAttemptsValue,
                    preLaunchIgnoreFailure
                    );

                if (success)
                {
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceCreated, Caption);
                }
                else
                {
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
                }
            }
            catch (UnauthorizedAccessException)
            {
                _messageBoxService.ShowError(Strings.Msg_AdminRightsRequired, Caption);
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public async Task UninstallService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _messageBoxService.ShowWarning(Strings.Msg_ValidationError, Caption);
                return;
            }

            try
            {
                bool success = await _serviceManager.UninstallService(serviceName);
                if (success)
                {
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceRemoved, Caption);
                }
                else
                {
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
                }
            }
            catch (UnauthorizedAccessException)
            {
                _messageBoxService.ShowError(Strings.Msg_AdminRightsRequired, Caption);
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public void StartService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.StartService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceStarted, Caption);
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public void StopService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.StopService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceStopped, Caption);
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
        }

        /// <inheritdoc />
        public void RestartService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.RestartService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceRestarted, Caption);
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
            }
        }

        #endregion

    }
}
