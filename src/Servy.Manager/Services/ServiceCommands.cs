using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
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
        private readonly ServiceManager _serviceManager;
        private readonly IServiceRepository _serviceRepository;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IFileDialogService _fileDialogService;
        private readonly ILogger _logger;
        private readonly Action<string> _removeServiceCallback;
        private readonly Func<Task> _refreshCallback;
        private readonly IServiceConfigurationValidator _serviceConfigurationValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCommands"/> class.
        /// </summary>
        /// <param name="serviceManager">The <see cref="ServiceManager"/> used to manage Windows services.</param>
        /// <param name="serviceRepository">The repository interface for accessing service data.</param>
        /// <param name="messageBoxService">The service used to show message boxes to the user.</param>
        /// <param name="logger">The logger used for logging warnings and errors.</param>
        /// <param name="fileDialogService">The service used to show file dialogs.</param>
        /// <param name="removeServiceCallback">A callback invoked when a service should be removed from the UI or collection.</param>
        /// <param name="refreshCallback">A callback invoked when a services list should be refreshed.</param>
        public ServiceCommands(
            ServiceManager serviceManager,
            IServiceRepository serviceRepository,
            IMessageBoxService messageBoxService,
            ILogger logger,
            IFileDialogService fileDialogService,
            Action<string> removeServiceCallback,
            Func<Task> refreshCallback,
            IServiceConfigurationValidator serviceConfigurationValidator
        )
        {
            _serviceManager = serviceManager;
            _serviceRepository = serviceRepository;
            _messageBoxService = messageBoxService;
            _logger = logger;
            _fileDialogService = fileDialogService;
            _removeServiceCallback = removeServiceCallback;
            _refreshCallback = refreshCallback;
            _serviceConfigurationValidator = serviceConfigurationValidator;
        }

        /// <inheritdoc />
        public async Task<List<Service>> SearchServicesAsync(string searchText, bool calculatePerf, CancellationToken cancellationToken = default)
        {
            var results = await _serviceRepository.SearchDomainServicesAsync(
                _serviceManager, searchText ?? string.Empty, cancellationToken);

            // Map all domain services to Service models in parallel
            var tasks = results.Select(r => ServiceMapper.ToModelAsync(r, calculatePerf));
            var services = await Task.WhenAll(tasks);

            return services.ToList();
        }

        /// <inheritdoc />
        public async Task<bool> StartServiceAsync(Service service, bool showMessageBox = true)
        {
            if (service == null) return false;

            try
            {
                var startupType = _serviceManager.GetServiceStartupType(service.Name);

                if (startupType == ServiceStartType.Disabled)
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceDisabledError, AppConfig.Caption);
                    return false;
                }

                var serviceDomain = await GetServiceDomain(service.Name);
                if (serviceDomain == null)
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                    return false;
                }

                var res = await Task.Run(() => serviceDomain.Start());
                if (res)
                {
                    service.Status = ServiceStatus.Running;
                    if (showMessageBox) await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceStarted, AppConfig.Caption);
                }
                else
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                }
                return res;
            }
            catch (Exception ex)
            {
                if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to start {service.Name}: {ex}");
                return false;
            }
        }

        public async Task<bool> StopServiceAsync(Service service, bool showMessageBox = true)
        {
            if (service == null) return false;

            try
            {
                var serviceDomain = await GetServiceDomain(service.Name);
                if (serviceDomain == null)
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                    return false;
                }

                var res = await Task.Run(() => serviceDomain.Stop());
                if (res)
                {
                    service.Status = ServiceStatus.Stopped;
                    if (showMessageBox) await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceStopped, AppConfig.Caption);
                }
                else
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                }
                return res;
            }
            catch (Exception ex)
            {
                if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to stop {service.Name}: {ex}");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RestartServiceAsync(Service service, bool showMessageBox = true)
        {
            if (service == null) return false;

            try
            {
                var startupType = _serviceManager.GetServiceStartupType(service.Name);

                if (startupType == ServiceStartType.Disabled)
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceDisabledError, AppConfig.Caption);
                    return false;
                }

                var serviceDomain = await GetServiceDomain(service.Name);
                if (serviceDomain == null)
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_ServiceNotFound, AppConfig.Caption);
                    return false;
                }

                var res = await Task.Run(() => serviceDomain.Restart());
                if (res)
                {
                    service.Status = ServiceStatus.Running;
                    if (showMessageBox) await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceRestarted, AppConfig.Caption);
                }
                else
                {
                    if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                }
                return res;
            }
            catch (Exception ex)
            {
                if (showMessageBox) await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to restart {service.Name}: {ex}");
                return false;
            }
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

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = app.ConfigurationAppPublishPath,
                        Arguments = "\"false\"", // Pass false to skip splash screen
                        UseShellExecute = true,
                    }
                };

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

                process.StartInfo.Arguments = $"\"false\" \"{service.Name}\""; // Pass false to skip splash screen

                process.Start();
            }
            catch (Exception ex)
            {
                string serviceName = service?.Name ?? "<unknown>";
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to configure {serviceName}: {ex}");
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
                    wrapperExeDir = System.IO.Path.GetFullPath(Core.Config.AppConfig.ServyServiceManagerDebugFolder);
                }
                if (!Directory.Exists(wrapperExeDir))
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_InvalidWrapperExePath, AppConfig.Caption);
                    return false;
                }
#endif
                var res = await Task.Run(() => serviceDomain.Install(wrapperExeDir));
                if (res)
                {
                    service.IsInstalled = true;
                    await _messageBoxService.ShowInfoAsync(Strings.Msg_ServiceInstalled, AppConfig.Caption);
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                }

                return res;
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to install {service.Name}: {ex}");
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
                if (res) RemoveService(service);

                return res;
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to uninstall {service.Name}: {ex}");
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

                return res > 0;
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to remove {service.Name}: {ex}");
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
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to export XML of {service.Name}: {ex}");
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
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to export JSON of {service.Name}: {ex}");
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

                if (!(await _serviceConfigurationValidator.Validate(dto))) return;

                var res = await _serviceRepository.UpsertAsync(dto);
                if (res > 0)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.ImportXml_Success, AppConfig.Caption);
                    RefreshServices();
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.ImportXml_Error, AppConfig.Caption);
                }
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to import XML config from {path}: {ex}");
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

                var dto = JsonConvert.DeserializeObject<ServiceDto>(json);
                if (dto == null)
                {
                    await _messageBoxService.ShowErrorAsync(Strings.Msg_FailedToLoadJson, AppConfig.Caption);
                    return;
                }

                if (!(await _serviceConfigurationValidator.Validate(dto))) return;

                var res = await _serviceRepository.UpsertAsync(dto);
                if (res > 0)
                {
                    await _messageBoxService.ShowInfoAsync(Strings.ImportJson_Success, AppConfig.Caption);
                    RefreshServices();
                }
                else
                {
                    await _messageBoxService.ShowErrorAsync(Strings.ImportJson_Error, AppConfig.Caption);
                }
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to import JSON config from {path}: {ex}");
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
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UnexpectedError, AppConfig.Caption);
                _logger.Warning($"Failed to copy PID to clipboard: {ex}");
            }
        }

        #region Helpers

        /// <summary>
        /// Retrieves the domain representation of a service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <returns>The domain service if found; otherwise, <c>null</c>.</returns>
        private async Task<Core.Domain.Service> GetServiceDomain(string serviceName)
        {
            var results = await _serviceRepository.SearchDomainServicesAsync(_serviceManager, serviceName);
            return results.FirstOrDefault();
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
        private void RefreshServices()
        {
            _refreshCallback?.Invoke();
        }

        #endregion
    }
}
