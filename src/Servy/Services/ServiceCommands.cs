using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Interfaces;
using Servy.Resources;
using System;
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
        private readonly IServiceManager _serviceManager;
        private readonly IMessageBoxService _messageBoxService;

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

        private const int MinRotationSize = 1 * 1024 * 1024;       // 1 MB
        private const int MinHeartbeatInterval = 5;                // 5 seconds
        private const int MinMaxFailedChecks = 1;                  // 1 attempt
        private const int MinMaxRestartAttempts = 1;               // 1 attempt

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
            string maxRestartAttempts)
        {
            if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(processPath))
            {
                _messageBoxService.ShowWarning(Strings.Msg_ValidationError, "Servy");
                return;
            }

            if (!Helper.IsValidPath(processPath) || !File.Exists(processPath))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidPath, "Servy");
                return;
            }

            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servy.Service.exe");

            if (!File.Exists(wrapperExePath))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidWrapperExePath, "Servy");
                return;
            }

            if (!string.IsNullOrWhiteSpace(startupDirectory) && (!Helper.IsValidPath(startupDirectory) || !Directory.Exists(startupDirectory)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStartupDirectory, "Servy");
                return;
            }

            if (!string.IsNullOrWhiteSpace(stdoutPath) && (!Helper.IsValidPath(stdoutPath) || !Helper.CreateParentDirectory(stdoutPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStdoutPath, "Servy");
                return;
            }

            if (!string.IsNullOrWhiteSpace(stderrPath) && (!Helper.IsValidPath(stderrPath) || !Helper.CreateParentDirectory(stderrPath)))
            {
                _messageBoxService.ShowError(Strings.Msg_InvalidStderrPath, "Servy");
                return;
            }

            int rotationSizeValue = 0;
            if (enableRotation)
            {
                if (!int.TryParse(rotationSize, out rotationSizeValue) || rotationSizeValue < MinRotationSize)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidRotationSize, "Servy");
                    return;
                }
            }

            int heartbeatIntervalValue = 0, maxFailedChecksValue = 0, maxRestartAttemptsValue = 0;
            if (enableHealthMonitoring)
            {
                if (!int.TryParse(heartbeatInterval, out heartbeatIntervalValue) || heartbeatIntervalValue < MinHeartbeatInterval)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidHeartbeatInterval, "Servy");
                    return;
                }

                if (!int.TryParse(maxFailedChecks, out maxFailedChecksValue) || maxFailedChecksValue < MinMaxFailedChecks)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidMaxFailedChecks, "Servy");
                    return;
                }

                if (!int.TryParse(maxRestartAttempts, out maxRestartAttemptsValue) || maxRestartAttemptsValue < MinMaxRestartAttempts)
                {
                    _messageBoxService.ShowError(Strings.Msg_InvalidMaxRestartAttempts, "Servy");
                    return;
                }
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
                    maxRestartAttemptsValue);

                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceCreated, "Servy");
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
            catch (UnauthorizedAccessException)
            {
                _messageBoxService.ShowError(Strings.Msg_AdminRightsRequired, "Servy");
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
        }

        /// <inheritdoc />
        public void UninstallService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _messageBoxService.ShowWarning(Strings.Msg_ValidationError, "Servy");
                return;
            }

            try
            {
                bool success = _serviceManager.UninstallService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceRemoved, "Servy");
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
            catch (UnauthorizedAccessException)
            {
                _messageBoxService.ShowError(Strings.Msg_AdminRightsRequired, "Servy");
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
        }

        /// <inheritdoc />
        public void StartService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.StartService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceStarted, "Servy");
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
        }

        /// <inheritdoc />
        public void StopService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.StopService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceStopped, "Servy");
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
        }

        /// <inheritdoc />
        public void RestartService(string serviceName)
        {
            try
            {
                bool success = _serviceManager.RestartService(serviceName);
                if (success)
                    _messageBoxService.ShowInfo(Strings.Msg_ServiceRestarted, "Servy");
                else
                    _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
            catch (Exception)
            {
                _messageBoxService.ShowError(Strings.Msg_UnexpectedError, "Servy");
            }
        }
    }
}
