using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Validators;
using Servy.UI.Helpers;
using Servy.UI.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Servy.Manager.Services
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
        /// <summary>
        /// Per-service locking mechanism to prevent Head-of-Line blocking.
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _serviceLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private bool _isDisposed;

        #region Private Fields

        private readonly IServiceManager _serviceManager;
        private readonly IServiceRepository _serviceRepository;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IFileDialogService _fileDialogService;
        private readonly Action<string> _removeServiceCallback;
        private readonly Func<Task> _refreshCallback;
        private readonly IServiceConfigurationValidator _serviceConfigurationValidator;
        private readonly IXmlServiceValidator _xmlServiceValidator;
        private readonly IJsonServiceValidator _jsonServiceValidator;
        private readonly IAppConfiguration _appConfig;
        private readonly IProcessHelper _processHelper;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommands"/> class.
        /// </summary>
        /// <param name="serviceManager">The <see cref="IServiceManager"/> used to manage Windows services.</param>
        /// <param name="serviceRepository">The repository interface for accessing service data.</param>
        /// <param name="messageBoxService">The service used to show message boxes to the user.</param>
        /// <param name="fileDialogService">The service used to show file dialogs.</param>
        /// <param name="removeServiceCallback">A callback invoked when a service should be removed from the UI or collection.</param>
        /// <param name="refreshCallback">A callback invoked when a services list should be refreshed.</param>
        /// <param name="serviceConfigurationValidator">The service configuration validator.</param>
        /// <param name="xmlServiceValidator">XML service validator.</param>
        /// <param name="jsonServiceValidator">JSON service validator.</param>
        /// <param name="appConfig">The application configuration interface.</param>
        /// <param name="processHelper">The process helper used to format process commands.</param>
        public ServiceCommands(
            IServiceManager serviceManager,
            IServiceRepository serviceRepository,
            IMessageBoxService messageBoxService,
            IFileDialogService fileDialogService,
            Action<string> removeServiceCallback,
            Func<Task> refreshCallback,
            IServiceConfigurationValidator serviceConfigurationValidator,
            IXmlServiceValidator xmlServiceValidator,
            IJsonServiceValidator jsonServiceValidator,
            IAppConfiguration appConfig,
            IProcessHelper processHelper
        )
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _removeServiceCallback = removeServiceCallback ?? throw new ArgumentNullException(nameof(removeServiceCallback));
            _refreshCallback = refreshCallback ?? throw new ArgumentNullException(nameof(refreshCallback));
            _serviceConfigurationValidator = serviceConfigurationValidator ?? throw new ArgumentNullException(nameof(serviceConfigurationValidator));
            _xmlServiceValidator = xmlServiceValidator ?? throw new ArgumentNullException(nameof(xmlServiceValidator));
            _jsonServiceValidator = jsonServiceValidator ?? throw new ArgumentNullException(nameof(jsonServiceValidator));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        }

        #endregion

        #region Locking Orchestrator

        /// <summary>
        /// Executes an asynchronous operation within a per-service lock.
        /// Automatically prunes the lock from the dictionary when the operation is complete
        /// and no other threads are waiting.
        /// </summary>
        private async Task<T> ExecuteLockedAsync<T>(string serviceName, Func<Task<T>> action)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentNullException(nameof(serviceName));

            // Get or create the lock
            var sem = _serviceLocks.GetOrAdd(serviceName, _ => new SemaphoreSlim(1, 1));

            await sem.WaitAsync();
            try
            {
                return await action();
            }
            finally
            {
                sem.Release();

                // EAGER EVICTION:
                // If the semaphore is now idle (CurrentCount == 1), try to remove it.
                // We use the KeyValuePair overload to ensure we don't remove a NEW 
                // semaphore that might have been added by a concurrent GetOrAdd.
                if (sem.CurrentCount == 1)
                {
                    var collection = (ICollection<KeyValuePair<string, SemaphoreSlim>>)_serviceLocks;
                    var kvp = new KeyValuePair<string, SemaphoreSlim>(serviceName, sem);

                    collection.Remove(kvp);

                    // NOTE: Do NOT dispose 'sem' here. If another thread managed to get 
                    // a reference to it via GetOrAdd just before we removed it, 
                    // disposing it would cause an ObjectDisposedException for them.
                    // Let the GC handle the cleanup of the evicted object.
                }
            }
        }

        #endregion

        #region IServiceCommands Implementation

        /// <inheritdoc />
        public async Task<List<Service>> SearchServicesAsync(string searchText, bool calculatePerf, CancellationToken cancellationToken = default)
        {
            var results = await _serviceRepository.SearchAsync(
                searchText ?? string.Empty, decrypt: false, cancellationToken);

            // Map all domain services to Service models in parallel
            var tasks = results.Select(r => ServiceMapper.ToModelAsync(
                Core.Mappers.ServiceMapper.ToDomain(_serviceManager, r),
                _appConfig.IsDesktopAppAvailable,
                calculatePerf,
                _processHelper));
            var services = await Task.WhenAll(tasks);

            // Filter out nulls resulting from malformed/orphaned DTOs 
            // to prevent NullReferenceExceptions during UI data binding.
            return services
                .Where(s => s != null)
                .ToList();
        }

        /// <inheritdoc />
        public Task<bool> StartServiceAsync(Service service, bool showMessageBox = true) =>
            ExecuteServiceCommandAsync(service, d => d.Start(), ServiceStatus.Running, Strings.Msg_ServiceStarted, checkDisabled: true, showMessageBox);

        /// <inheritdoc />
        public Task<bool> StopServiceAsync(Service service, bool showMessageBox = true) =>
            ExecuteServiceCommandAsync(service, d => d.Stop(), ServiceStatus.Stopped, Strings.Msg_ServiceStopped, checkDisabled: false, showMessageBox);

        /// <inheritdoc />
        public Task<bool> RestartServiceAsync(Service service, bool showMessageBox = true) =>
            ExecuteServiceCommandAsync(service, d => d.Restart(), ServiceStatus.Running, Strings.Msg_ServiceRestarted, checkDisabled: true, showMessageBox);

        /// <inheritdoc />
        public async Task ConfigureServiceAsync(Service service)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_appConfig.DesktopAppPublishPath) || !File.Exists(_appConfig.DesktopAppPublishPath))
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_DesktopAppNotFound, AppConfig.Caption);
                    return;
                }

                var forceFlag = _appConfig.ForceSoftwareRendering ? $" {Core.Config.AppConfig.ForceSoftwareRenderingArg}" : string.Empty;

                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _appConfig.DesktopAppPublishPath,
                        Arguments = $"\"false\"{forceFlag}", // Pass false to skip splash screen
                        UseShellExecute = true,
                    }
                })
                {
                    if (service == null)
                    {
                        StartProcess(process);
                        return;
                    }

                    var serviceDomain = await GetServiceDomain(service.Name);
                    if (serviceDomain == null)
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                        return;
                    }

                    // Pass false to skip splash screen
                    process.StartInfo.Arguments = $"\"false\" {_processHelper.EscapeProcessArgument(service.Name)}{forceFlag}";

                    StartProcess(process);
                }
            }
            catch (Exception ex)
            {
                string serviceName = service?.Name ?? "<unknown>";
                Logger.Error($"Failed to configure {serviceName}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <inheritdoc />
        public async Task<bool> InstallServiceAsync(Service service)
        {
            if (service == null) return false;

            return await ExecuteLockedAsync(service.Name, async () =>
            {

                try
                {
                    var exists = _serviceManager.IsServiceInstalled(service.Name);

                    if (exists)
                    {
                        var result = await _messageBoxService.ShowConfirmAsync(Strings.Msg_ServiceAlreadyExists, AppConfig.Caption);
                        if (!result)
                        {
                            return false;
                        }

                    }

                    var serviceDomain = await GetServiceDomain(service.Name);
                    if (serviceDomain == null)
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                        return false;
                    }

                    string wrapperExeDir = null;
#if DEBUG
                    if (wrapperExeDir == null)
                    {
                        wrapperExeDir = Path.GetFullPath(Core.Config.AppConfig.ServyServiceManagerDebugFolder);
                    }
                    if (!Directory.Exists(wrapperExeDir))
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidWrapperExePath, AppConfig.Caption);
                        return false;
                    }
#endif
                    var res = await Task.Run(() => serviceDomain.Install(wrapperExeDir));
                    if (res.IsSuccess)
                    {
                        service.IsInstalled = true;
                        await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceInstalled, AppConfig.Caption);
                    }
                    else
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                    }

                    return res.IsSuccess;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to install {service.Name}.", ex);
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<bool> UninstallServiceAsync(Service service, CancellationToken cancellationToken = default)
        {
            if (service == null) return false;

            return await ExecuteLockedAsync(service.Name, async () =>
            {
                try
                {
                    var confirm = await _messageBoxService.ShowConfirmAsync(Strings.Msg_UninstallServiceConfirm, AppConfig.Caption);
                    if (!confirm) return false;

                    var serviceDomain = await GetServiceDomain(service.Name);
                    if (serviceDomain == null)
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                        return false;
                    }

                    var res = await Task.Run(() => serviceDomain.Uninstall(cancellationToken));
                    if (res.IsSuccess) await Task.Run(() => RemoveService(service));

                    return res.IsSuccess;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to uninstall {service.Name}.", ex);
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<bool> RemoveServiceAsync(Service service)
        {
            if (service == null) return false;

            return await ExecuteLockedAsync(service.Name, async () =>
            {
                try
                {
                    var confirm = await _messageBoxService.ShowConfirmAsync(Strings.Msg_RemoveServiceConfirm, AppConfig.Caption);
                    if (!confirm) return false;

                    var serviceDomain = await GetServiceDomain(service.Name);
                    if (serviceDomain == null)
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                        return false;
                    }

                    var res = await Task.Run(() => _serviceRepository.DeleteAsync(service.Name));
                    if (res > 0) await Task.Run(() => RemoveService(service));

                    var success = res > 0;

                    if (success)
                    {
                        Logger.Info($"Service {service.Name} removed successfully.");
                    }
                    else
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                        Logger.Error($"Failed to remove service {service.Name} from repository.");
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to remove {service.Name}.", ex);
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public Task ExportServiceToXmlAsync(Service service) =>
            ExportServiceConfigAsync(
                service,
                () => _fileDialogService.SaveXml(Strings.SaveFileDialog_XmlTitle),
                ServiceExporter.ExportXml,
                "XML",
                Strings.ExportXml_Success);

        /// <inheritdoc />
        public Task ExportServiceToJsonAsync(Service service) =>
            ExportServiceConfigAsync(
                service,
                () => _fileDialogService.SaveJson(Strings.SaveFileDialog_JsonTitle),
                ServiceExporter.ExportJson,
                "JSON",
                Strings.ExportJson_Success);

        /// <inheritdoc />
        public Task ImportXmlConfigAsync() =>
            ImportConfigAsync(
                _fileDialogService.OpenXml,
                (content) => { var isValid = _xmlServiceValidator.TryValidate(content, out var err); return (isValid, err); },
                (content) => new XmlServiceSerializer().Deserialize(content),
                "XML",
                Strings.Msg_FailedToLoadXml,
                Strings.ImportXml_Success,
                Strings.ImportXml_Error);

        /// <inheritdoc />
        public Task ImportJsonConfigAsync() =>
            ImportConfigAsync(
                _fileDialogService.OpenJson,
                (content) => { var isValid = _jsonServiceValidator.TryValidate(content, out var err); return (isValid, err); },
                (content) => new JsonServiceSerializer().Deserialize(content),
                "JSON",
                Strings.Msg_FailedToLoadJson,
                Strings.ImportJson_Success,
                Strings.ImportJson_Error);

        ///<inheritdoc/>
        public async Task CopyPid(Service service)
        {
            try
            {
                var pid = service.Pid;
                if (pid == null) return;
                Clipboard.SetText(pid.Value.ToString());
                await _messageBoxService.ShowInfoAsync(Strings.Msg_PidCopied, AppConfig.Caption);
                Logger.Info($"PID {service.Pid} of service {service.Name} copied to clipboard.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to copy PID to clipboard.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ServiceCommands"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Free managed objects here
                foreach (var sem in _serviceLocks.Values)
                {
                    try
                    {
                        sem.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Fail-silent on disposal to prevent crash during shutdown
                        Logger.Error("Error disposing semaphore during ServiceCommands teardown.", ex);
                    }
                }
                _serviceLocks.Clear();
            }

            // Free unmanaged objects here (if any)

            _isDisposed = true;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Executes a service management operation within a per-service lock, managing background execution, 
        /// UI state synchronization, and optional user notifications.
        /// </summary>
        /// <param name="service">The <see cref="Service"/> UI model to be updated upon successful operation.</param>
        /// <param name="operation">An asynchronous delegate that performs the core domain logic using a 
        /// <see cref="Core.Domain.Service"/> instance.</param>
        /// <param name="targetStatus">The <see cref="ServiceStatus"/> that the UI model should transition 
        /// to if the operation succeeds (e.g., Running, Stopped).</param>
        /// <param name="successMessage">The localized message string to display in a success dialog 
        /// if <paramref name="showMessageBox"/> is <c>true</c>.</param>
        /// <param name="checkDisabled">If <c>true</c>, verifies the service is not 'Disabled' before 
        /// invoking the operation.</param>
        /// <param name="showMessageBox">Indicates whether to display success/error dialogs to the user 
        /// after execution.</param>
        /// <returns>
        /// A task representing the asynchronous operation. The task result is <c>true</c> if the operation 
        /// completed successfully and the service state was updated; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method utilizes <see cref="ExecuteLockedAsync{T}"/> to prevent concurrent, conflicting 
        /// operations on the same service (Head-of-Line blocking). The core operation is explicitly 
        /// offloaded to <see cref="Task.Run"/> to keep the UI responsive during long-running service state 
        /// transitions.
        /// </remarks>
        private async Task<bool> ExecuteServiceCommandAsync(
            Service service,
            Func<Core.Domain.Service, Task<OperationResult>> operation,
            ServiceStatus targetStatus,
            string successMessage,
            bool checkDisabled,
            bool showMessageBox)
        {
            if (service == null) return false;

            return await ExecuteLockedAsync(service.Name, async () =>
            {
                bool success = false;
                string errorMessage = null;
                string infoMessage = null;

                try
                {
                    var serviceDomain = await GetServiceDomain(service.Name);
                    if (serviceDomain == null)
                    {
                        errorMessage = Strings.Msg_ServiceNotFound;
                    }
                    else if (checkDisabled && _serviceManager.GetServiceStartupType(service.Name) == ServiceStartType.Disabled)
                    {
                        errorMessage = Strings.Msg_ServiceDisabledError;
                    }
                    else
                    {
                        // Execute the core logic on a background thread
                        var res = await Task.Run(() => operation(serviceDomain));

                        if (res.IsSuccess)
                        {
                            service.Status = targetStatus;
                            infoMessage = successMessage;
                            success = true;
                        }
                        else
                        {
                            errorMessage = Strings.Msg_UnexpectedError;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to execute operation on {service.Name}.", ex);
                    errorMessage = Strings.Msg_UnexpectedError;
                }

                if (showMessageBox)
                {
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                        await _messageBoxService.ShowErrorAsync(errorMessage, AppConfig.Caption);
                    else if (!string.IsNullOrWhiteSpace(infoMessage))
                        await _messageBoxService.ShowInfoAsync(infoMessage, AppConfig.Caption);
                }

                return success;
            });
        }

        /// <summary>
        /// Retrieves the domain representation of a service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <returns>The domain service if found; otherwise, <c>null</c>.</returns>
        private async Task<Core.Domain.Service> GetServiceDomain(string serviceName)
        {
            var serviceDto = await _serviceRepository.GetByNameAsync(serviceName, decrypt: true);

            // If the service is not in the repository, we must return null 
            // to allow callers to show the "Service Not Found" message.
            if (serviceDto == null)
            {
                Logger.Warn($"Lookup failed: Service '{serviceName}' was not found in the repository.");
                return null;
            }

            // Map to the domain engine only if we have a valid data transfer object
            return Core.Mappers.ServiceMapper.ToDomain(_serviceManager, serviceDto);
        }

        /// <summary>
        /// Standardizes the service configuration export pipeline by retrieving the persistence-layer DTO 
        /// and executing a format-specific serialization delegate.
        /// </summary>
        /// <param name="service">The UI model representing the service to be exported.</param>
        /// <param name="getFilePath">A delegate that opens a save file dialog and returns the chosen destination path.</param>
        /// <param name="exportAction">A delegate responsible for serializing the <see cref="ServiceDto"/> and writing it to disk.</param>
        /// <param name="formatName">The name of the format (e.g., "XML", "JSON") for logging and error context.</param>
        /// <param name="successMessage">The localized string to display upon successful export.</param>
        /// <returns>A task representing the asynchronous export operation.</returns>
        /// <remarks>
        /// Unlike the Desktop App variant, this method retrieves the <see cref="ServiceDto"/> directly 
        /// from the <see cref="IServiceRepository"/> to ensure the exported file reflects the actual 
        /// stored state, including encrypted credentials if applicable.
        /// </remarks>
        private async Task ExportServiceConfigAsync(
            Service service,
            Func<string> getFilePath,
            Action<ServiceDto, string> exportAction,
            string formatName,
            string successMessage)
        {
            try
            {
                var path = getFilePath();
                if (string.IsNullOrEmpty(path)) return;

                var dto = await _serviceRepository.GetByNameAsync(service.Name);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                    return;
                }

                exportAction(dto, path);

                await _messageBoxService.ShowInfoAsync(successMessage, AppConfig.Caption);
                Logger.Info($"Service configuration exported to {formatName} at: {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export {formatName} of {service.Name}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <summary>
        /// Standardizes the configuration import pipeline, enforcing a multi-stage validation gate 
        /// before persisting the configuration to the repository and refreshing the UI.
        /// </summary>
        /// <param name="getFilePath">A delegate that opens an open file dialog and returns the source path.</param>
        /// <param name="validateContent">A delegate that performs raw format validation (e.g., schema or syntax checks).</param>
        /// <param name="deserialize">A delegate that converts the validated string content into a <see cref="ServiceDto"/>.</param>
        /// <param name="formatName">The name of the format (e.g., "XML", "JSON") for logging purposes.</param>
        /// <param name="loadErrorMessage">The message to display if the file content is incompatible with the DTO structure.</param>
        /// <param name="successMessage">The message to display upon successful repository persistence.</param>
        /// <param name="errorMessage">The message to display if the repository upsert operation fails.</param>
        /// <returns>A task representing the asynchronous import operation.</returns>
        /// <remarks>
        /// The import follows a strict "Gatekeeper" pattern:
        /// <list type="number">
        /// <item><description>Size Check: Prevents large file attacks via <see cref="ImportGuard"/>.</description></item>
        /// <item><description>Format Check: Ensures the raw string is valid XML/JSON.</description></item>
        /// <item><description>Domain Check: Validates business rules via <see cref="IServiceConfigurationValidator"/>.</description></item>
        /// <item><description>Persistence: Executes an Upsert in the database.</description></item>
        /// <item><description>UI Sync: Triggers the <see cref="RefreshServices"/> callback to update the dashboard.</description></item>
        /// </list>
        /// </remarks>
        private async Task ImportConfigAsync(
            Func<string> getFilePath,
            Func<string, (bool IsValid, string ErrorMsg)> validateContent,
            Func<string, ServiceDto> deserialize,
            string formatName,
            string loadErrorMessage,
            string successMessage,
            string errorMessage)
        {
            var path = getFilePath();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (!await ImportGuard.ValidateFileSizeAsync(path, _messageBoxService, AppConfig.Caption, Core.Config.AppConfig.MaxConfigFileSizeMB, Strings.Msg_ConfigSizeLimitReached))
                    return;

                var content = File.ReadAllText(path);
                var validation = validateContent(content);
                if (!validation.IsValid)
                {
                    await _messageBoxService.ShowErrorAsync(validation.ErrorMsg, AppConfig.Caption);
                    return;
                }

                var dto = deserialize(content);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(loadErrorMessage, AppConfig.Caption);
                    return;
                }

                if (!await _serviceConfigurationValidator.Validate(dto)) return;

                var res = await _serviceRepository.UpsertAsync(dto);
                if (res > 0)
                {
                    await _messageBoxService.ShowInfoAsync(successMessage, AppConfig.Caption);
                    await RefreshServices();
                    Logger.Info($"Service configuration imported from {formatName} at: {path}");
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(errorMessage, AppConfig.Caption);
                    Logger.Error($"Failed to import {formatName} config from {path}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import {formatName} config from {path}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <summary>
        /// Removes a service using the configured removal callback.
        /// </summary>
        /// <param name="service">The service to remove.</param>
        private void RemoveService(Service service)
        {
            _removeServiceCallback?.Invoke(service.Name);
        }

        /// <summary>
        /// Refreshes services list using the configured resfresh callback.
        /// </summary>
        private async Task RefreshServices()
        {
            if (_refreshCallback != null)
                await _refreshCallback();
        }

        /// <summary>
        /// Attempts to launch an external process and logs a warning if the start operation fails.
        /// </summary>
        /// <param name="process">The <see cref="Process"/> instance configured with the necessary start information.</param>
        /// <param name="app">The application context containing the publish path used for logging failure details.</param>
        /// <remarks>
        /// This method does not throw an exception on failure; instead, it utilizes the 
        /// <see cref="Logger.Warn(string)"/> method to record the failed attempt to launch 
        /// the external configuration tool.
        /// </remarks>
        private void StartProcess(Process process)
        {
            if (!process.Start())
            {
                Logger.Warn($"Failed to start external process {_appConfig.DesktopAppPublishPath}.");
            }
        }

        #endregion

    }
}
