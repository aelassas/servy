using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Manager.Config;
using Servy.Manager.Helpers;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.UI.Services;
using System;
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
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);

        #region Private Fields

        private readonly IServiceManager _serviceManager;
        private readonly IServiceRepository _serviceRepository;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IFileDialogService _fileDialogService;
        private readonly Action<string> _removeServiceCallback;
        private readonly Func<Task> _refreshCallback;
        private readonly IServiceConfigurationValidator _serviceConfigurationValidator;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommands"/> class.
        /// </summary>
        /// <param name="serviceManager">The <see cref="ServiceManager"/> used to manage Windows services.</param>
        /// <param name="serviceRepository">The repository interface for accessing service data.</param>
        /// <param name="messageBoxService">The service used to show message boxes to the user.</param>
        /// <param name="fileDialogService">The service used to show file dialogs.</param>
        /// <param name="removeServiceCallback">A callback invoked when a service should be removed from the UI or collection.</param>
        /// <param name="refreshCallback">A callback invoked when a services list should be refreshed.</param>
        public ServiceCommands(
            ServiceManager serviceManager,
            IServiceRepository serviceRepository,
            IMessageBoxService messageBoxService,
            IFileDialogService fileDialogService,
            Action<string> removeServiceCallback,
            Func<Task> refreshCallback,
            IServiceConfigurationValidator serviceConfigurationValidator
        )
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _removeServiceCallback = removeServiceCallback ?? throw new ArgumentNullException(nameof(removeServiceCallback));
            _refreshCallback = refreshCallback ?? throw new ArgumentNullException(nameof(refreshCallback));
            _serviceConfigurationValidator = serviceConfigurationValidator ?? throw new ArgumentNullException(nameof(serviceConfigurationValidator));
        }

        #endregion

        #region IServiceCommands Implementation

        /// <inheritdoc />
        public async Task<List<Service>> SearchServicesAsync(string searchText, bool calculatePerf, CancellationToken cancellationToken = default)
        {
            var results = await _serviceRepository.SearchAsync(
                 searchText ?? string.Empty, decrypt: false, cancellationToken);

            // Map all domain services to Service models in parallel
            var tasks = results.Select(r => ServiceMapper.ToModelAsync(Core.Mappers.ServiceMapper.ToDomain(_serviceManager, r), calculatePerf));
            var services = await Task.WhenAll(tasks);

            return services.ToList();
        }

        /// <inheritdoc />
        public async Task<bool> StartServiceAsync(Service service, bool showMessageBox = true)
        {
            if (service == null) return false;

            bool success = false;
            string errorMessage = null;
            string infoMessage = null;

            await _commandLock.WaitAsync();

            try
            {
                var serviceDomain = await GetServiceDomain(service.Name);
                if (serviceDomain == null)
                {
                    errorMessage = Strings.Msg_ServiceNotFound;
                }
                else
                {
                    var startupType = _serviceManager.GetServiceStartupType(service.Name);
                    if (startupType == ServiceStartType.Disabled)
                    {
                        errorMessage = Strings.Msg_ServiceDisabledError;
                    }
                    else
                    {
                        var res = await Task.Run(() => serviceDomain.Start());
                        if (res.IsSuccess)
                        {
                            service.Status = ServiceStatus.Running;
                            infoMessage = Strings.Msg_ServiceStarted;
                            success = true;
                        }
                        else
                        {
                            errorMessage = Strings.Msg_UnexpectedError;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start {service.Name}.", ex);
                errorMessage = Strings.Msg_UnexpectedError;
            }
            finally
            {
                _commandLock.Release();
            }

            if (showMessageBox)
            {
                if (!string.IsNullOrEmpty(errorMessage))
                    await _messageBoxService.ShowErrorAsync(errorMessage, AppConfig.Caption);
                else if (!string.IsNullOrEmpty(infoMessage))
                    await _messageBoxService.ShowInfoAsync(infoMessage, AppConfig.Caption);
            }

            return success;
        }

        public async Task<bool> StopServiceAsync(Service service, bool showMessageBox = true)
        {
            // 1. Guard clause outside the lock
            if (service == null) return false;

            bool success = false;
            string errorMessage = null;
            string infoMessage = null;

            await _commandLock.WaitAsync();

            try
            {
                var serviceDomain = await GetServiceDomain(service.Name);
                if (serviceDomain == null)
                {
                    errorMessage = Strings.Msg_ServiceNotFound;
                }
                else
                {
                    // Execute the stop logic on a background thread
                    var res = await Task.Run(() => serviceDomain.Stop());

                    if (res.IsSuccess)
                    {
                        service.Status = ServiceStatus.Stopped;
                        infoMessage = Strings.Msg_ServiceStopped;
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
                Logger.Error($"Failed to stop {service.Name}.", ex);
                errorMessage = Strings.Msg_UnexpectedError;
            }
            finally
            {
                // 2. Release the lock immediately after the engine operation completes
                _commandLock.Release();
            }

            if (showMessageBox)
            {
                if (!string.IsNullOrEmpty(errorMessage))
                    await _messageBoxService.ShowErrorAsync(errorMessage, AppConfig.Caption);
                else if (!string.IsNullOrEmpty(infoMessage))
                    await _messageBoxService.ShowInfoAsync(infoMessage, AppConfig.Caption);
            }

            return success;
        }

        /// <inheritdoc />
        public async Task<bool> RestartServiceAsync(Service service, bool showMessageBox = true)
        {
            // 1. Guard clause outside the lock
            if (service == null) return false;

            bool success = false;
            string errorMessage = null;
            string infoMessage = null;

            await _commandLock.WaitAsync();

            try
            {
                var serviceDomain = await GetServiceDomain(service.Name);
                if (serviceDomain == null)
                {
                    errorMessage = Strings.Msg_ServiceNotFound;
                }
                else
                {
                    var startupType = _serviceManager.GetServiceStartupType(service.Name);
                    if (startupType == ServiceStartType.Disabled)
                    {
                        errorMessage = Strings.Msg_ServiceDisabledError;
                    }
                    else
                    {
                        // Execute the restart logic on a background thread
                        var res = await Task.Run(() => serviceDomain.Restart());

                        if (res.IsSuccess)
                        {
                            service.Status = ServiceStatus.Running;
                            infoMessage = Strings.Msg_ServiceRestarted;
                            success = true;
                        }
                        else
                        {
                            errorMessage = Strings.Msg_UnexpectedError;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to restart {service.Name}.", ex);
                errorMessage = Strings.Msg_UnexpectedError;
            }
            finally
            {
                // 2. Release the lock immediately after the operation finishes
                _commandLock.Release();
            }

            if (showMessageBox)
            {
                if (!string.IsNullOrEmpty(errorMessage))
                    await _messageBoxService.ShowErrorAsync(errorMessage, AppConfig.Caption);
                else if (!string.IsNullOrEmpty(infoMessage))
                    await _messageBoxService.ShowInfoAsync(infoMessage, AppConfig.Caption);
            }

            return success;
        }

        /// <inheritdoc />
        public async Task ConfigureServiceAsync(Service service)
        {
            try
            {
                var app = (App)Application.Current;
                if (string.IsNullOrWhiteSpace(app.ConfigurationAppPublishPath) || !File.Exists(app.ConfigurationAppPublishPath))
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ConfigurationAppNotFound, AppConfig.Caption);
                    return;
                }

                var forceFlag = app.ForceSoftwareRendering ? $" {Core.Config.AppConfig.ForceSoftwareRenderingArg}" : string.Empty;

                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = app.ConfigurationAppPublishPath,
                        Arguments = $"\"false\"{forceFlag}", // Pass false to skip splash screen
                        UseShellExecute = true,
                    }
                })
                {
                    if (service == null)
                    {
                        process.Start();
                        return;
                    }

                    var serviceDomain = await GetServiceDomain(service.Name);
                    if (serviceDomain == null)
                    {
                        await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                        return;
                    }

                    // Pass false to skip splash screen
                    process.StartInfo.Arguments = $"\"false\" \"{service.Name}\"{forceFlag}";

                    process.Start();
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

            try
            {
                var exists = _serviceManager.IsServiceInstalled(service.Name);

                if (exists)
                {
                    var result = await _messageBoxService.ShowConfirmAsync(Strings.Msg_ServiceAlreadyExists, AppConfig.Caption);
                    if (!result)
                    {
                        return true;
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
        }

        /// <inheritdoc />
        public async Task<bool> UninstallServiceAsync(Service service)
        {
            if (service == null) return false;

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

                var res = await Task.Run(() => serviceDomain.Uninstall());
                if (res.IsSuccess) RemoveService(service);

                return res.IsSuccess;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to uninstall {service.Name}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveServiceAsync(Service service)
        {
            if (service == null) return false;

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
                if (res > 0) RemoveService(service);

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
        }

        /// <inheritdoc />
        public async Task ExportServiceToXmlAsync(Service service)
        {
            try
            {
                var path = _fileDialogService.SaveXml(Strings.SaveFileDialog_XmlTitle);
                if (string.IsNullOrEmpty(path)) return;

                var dto = await _serviceRepository.GetByNameAsync(service.Name);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                    return;
                }

                ServiceExporter.ExportXml(dto, path);
                await _messageBoxService.ShowInfoAsync(Strings.ExportXml_Success, AppConfig.Caption);
                Logger.Info($"Service configuration exported to XML at: {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export XML of {service.Name}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <inheritdoc />
        public async Task ExportServiceToJsonAsync(Service service)
        {
            try
            {
                var path = _fileDialogService.SaveJson(Strings.SaveFileDialog_JsonTitle);
                if (string.IsNullOrEmpty(path)) return;

                var dto = await _serviceRepository.GetByNameAsync(service.Name);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                    return;
                }

                ServiceExporter.ExportJson(dto, path);
                await _messageBoxService.ShowInfoAsync(Strings.ExportJson_Success, AppConfig.Caption);
                Logger.Info($"Service configuration exported to JSON at: {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export JSON of {service.Name}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <inheritdoc />
        public async Task ImportXmlConfigAsync()
        {
            var path = _fileDialogService.OpenXml();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var xml = File.ReadAllText(path);
                if (!XmlServiceValidator.TryValidate(xml, out var errorMsg))
                {
                    await _messageBoxService.ShowErrorAsync(errorMsg, AppConfig.Caption);
                    return;
                }

                var serializer = new XmlServiceSerializer();
                var dto = serializer.Deserialize(xml);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_FailedToLoadXml, AppConfig.Caption);
                    return;
                }

                if (!await _serviceConfigurationValidator.Validate(dto)) return;

                var res = await _serviceRepository.UpsertAsync(dto);
                if (res > 0)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.ImportXml_Success, AppConfig.Caption);
                    await RefreshServices();
                    Logger.Info($"Service configuration imported from XML at: {path}");
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.ImportXml_Error, AppConfig.Caption);
                    Logger.Error($"Failed to import XML config from {path}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import XML config from {path}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        /// <inheritdoc />
        public async Task ImportJsonConfigAsync()
        {
            var path = _fileDialogService.OpenJson();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                if (!JsonServiceValidator.TryValidate(json, out var errorMsg))
                {
                    await _messageBoxService.ShowErrorAsync(errorMsg, AppConfig.Caption);
                    return;
                }

                var dto = JsonConvert.DeserializeObject<ServiceDto>(json, JsonSecurity.UntrustedDataSettings);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_FailedToLoadJson, AppConfig.Caption);
                    return;
                }

                if (!await _serviceConfigurationValidator.Validate(dto)) return;

                ServiceDtoHelper.ApplyDefaults(dto);

                var res = await _serviceRepository.UpsertAsync(dto);
                if (res > 0)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.ImportJson_Success, AppConfig.Caption);
                    await RefreshServices();
                    Logger.Info($"Service configuration imported from JSON at: {path}");
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.ImportJson_Error, AppConfig.Caption);
                    Logger.Error($"Failed to import JSON config from {path}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import JSON config from {path}.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        ///<inheritdoc/>
        public async Task CopyPid(Service service)
        {
            try
            {
                if (service.Pid == null) return;
                Clipboard.SetText(service.Pid.ToString());
                await _messageBoxService.ShowInfoAsync(Strings.Msg_PidCopied, AppConfig.Caption);
                Logger.Info($"PID {service.Pid} of service {service.Name} copied to clipboard.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to copy PID to clipboard.", ex);
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Retrieves the domain representation of a service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <returns>The domain service if found; otherwise, <c>null</c>.</returns>
        private async Task<Core.Domain.Service> GetServiceDomain(string serviceName)
        {
            var serviceDto = await _serviceRepository.GetByNameAsync(serviceName, decrypt: true);
            var service = Core.Mappers.ServiceMapper.ToDomain(_serviceManager, serviceDto);
            return service;
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

        #endregion

    }
}
