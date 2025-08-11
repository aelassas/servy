using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Interfaces;
using Servy.Core.ServiceDependencies;
using Servy.Resources;
using System;
using System.Collections.Generic;
using System.IO;

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

        private const string Caption = "Servy";
        private const int MinRotationSize = 1 * 1024 * 1024;       // 1 MB
        private const int MinHeartbeatInterval = 5;                // 5 seconds
        private const int MinMaxFailedChecks = 1;                  // 1 attempt
        private const int MinMaxRestartAttempts = 1;               // 1 attempt

        #endregion

        #region Private Fields

        private readonly IServiceManager _serviceManager;
        private readonly IMessageBoxService _messageBoxService;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommands"/> class.
        /// </summary>
        /// <param name="serviceManager">The service manager to handle service operations.</param>
        /// <param name="messageBoxService">The message box service to show user messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
        public ServiceCommands(IServiceManager serviceManager, IMessageBoxService messageBoxService)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        #endregion


        #region IServiceCommands Implementation

        /// <inheritdoc />
        public void InstallService(
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
            string confirmPassword
            )
        {
            if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(processPath))
            {
                _messageBoxService.ShowWarning(Strings.Msg_ValidationError, Caption);
                return;
            }

            if (!Helper.IsValidPath(processPath) || !File.Exists(processPath))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPath, Caption);
                return;
            }

            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{App.ServyServiceExeFileName}.exe");

            if (!File.Exists(wrapperExePath))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidWrapperExePath, Caption);
                return;
            }

            if (!string.IsNullOrWhiteSpace(startupDirectory) && (!Helper.IsValidPath(startupDirectory) || !Directory.Exists(startupDirectory)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStartupDirectory, Caption);
                return;
            }

            if (!string.IsNullOrWhiteSpace(stdoutPath) && (!Helper.IsValidPath(stdoutPath) || !Helper.CreateParentDirectory(stdoutPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStdoutPath, Caption);
                return;
            }

            if (!string.IsNullOrWhiteSpace(stderrPath) && (!Helper.IsValidPath(stderrPath) || !Helper.CreateParentDirectory(stderrPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStderrPath, Caption);
                return;
            }

            int rotationSizeValue = 0;
            if (enableRotation)
            {
                if (!int.TryParse(rotationSize, out rotationSizeValue) || rotationSizeValue < MinRotationSize)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidRotationSize, Caption);
                    return;
                }
            }

            int heartbeatIntervalValue = 0, maxFailedChecksValue = 0, maxRestartAttemptsValue = 0;
            if (enableHealthMonitoring)
            {
                if (!int.TryParse(heartbeatInterval, out heartbeatIntervalValue) || heartbeatIntervalValue < MinHeartbeatInterval)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidHeartbeatInterval, Caption);
                    return;
                }

                if (!int.TryParse(maxFailedChecks, out maxFailedChecksValue) || maxFailedChecksValue < MinMaxFailedChecks)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidMaxFailedChecks, Caption);
                    return;
                }

                if (!int.TryParse(maxRestartAttempts, out maxRestartAttemptsValue) || maxRestartAttemptsValue < MinMaxRestartAttempts)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidMaxRestartAttempts, Caption);
                    return;
                }
            }

            string normalizedEnvVars = environmentVariables?.Replace("\r\n", ";").Replace("\n", ";").Replace("\r", ";") ?? string.Empty;

            string envVarsErrorMessage;
            if (!EnvironmentVariablesValidator.Validate(normalizedEnvVars, out envVarsErrorMessage))
            {
                _messageBoxService.ShowError(envVarsErrorMessage, Caption);
                return;
            }

            List<string> serviceDependenciesErrors;
            if (!ServiceDependenciesValidator.Validate(serviceDependencies, out serviceDependenciesErrors))
            {
                _messageBoxService.ShowError(string.Join("\n", serviceDependenciesErrors), Caption);
                return;
            }

            if (!runAsLocalSystem)
            {
                try
                {
                    if (password != null && !password.Equals(confirmPassword, StringComparison.Ordinal))
                    {
                        _messageBoxService.ShowError(Strings.Msg_PasswordsDontMatch, Caption);
                        return;
                    }

                    _serviceManager.ValidateCredentials(userAccount, password);
                }
                catch (Exception ex)
                {
                    _messageBoxService.ShowError(ex.Message, Caption);
                    return;
                }
            }
            else
            {
                userAccount = null;
                password = null;
            }

            try
            {
                bool success = _serviceManager.InstallService(
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
                    password
                    );

                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceCreated, Caption);
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
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
        public void UninstallService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _messageBoxService.ShowWarning(Strings.Msg_ValidationError, Caption);
                return;
            }

            try
            {
                bool success = _serviceManager.UninstallService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceRemoved, Caption);
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, Caption);
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
