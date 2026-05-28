using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Mappers;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.Validators;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to import a service configuration file (XML or JSON) and optionally install the service.
    /// </summary>
    public class ImportServiceCommand : BaseCommand
    {
        private readonly IServiceRepository _serviceRepository;
        private readonly IXmlServiceSerializer _xmlServiceSerializer;
        private readonly IJsonServiceSerializer _jsonServiceSerializer;
        private readonly IServiceManager _serviceManager;
        private readonly IXmlServiceValidator _xmlServiceValidator;
        private readonly IJsonServiceValidator _jsonServiceValidator;
        private readonly IProcessHelper _processHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceRepository">The service repository for persisting configurations.</param>
        /// <param name="xmlServiceSerializer">Serializer for XML service configurations.</param>
        /// <param name="jsonServiceSerializer">Serializer for JSON service configurations.</param>
        /// <param name="serviceManager">Manager to control Windows services.</param>
        /// <param name="xmlServiceValidator">Validator for XML service configurations.</param>
        /// <param name="jsonServiceValidator">Validator for JSON service configurations.</param>
        /// <param name="processHelper">Helper for process related formatting and parsing.</param>
        public ImportServiceCommand(
            IServiceRepository serviceRepository,
            IXmlServiceSerializer xmlServiceSerializer,
            IJsonServiceSerializer jsonServiceSerializer,
            IServiceManager serviceManager,
            IXmlServiceValidator xmlServiceValidator,
            IJsonServiceValidator jsonServiceValidator,
            IProcessHelper processHelper)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _xmlServiceValidator = xmlServiceValidator ?? throw new ArgumentNullException(nameof(xmlServiceValidator));
            _jsonServiceValidator = jsonServiceValidator ?? throw new ArgumentNullException(nameof(jsonServiceValidator));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _xmlServiceSerializer = xmlServiceSerializer ?? throw new ArgumentNullException(nameof(xmlServiceSerializer));
            _jsonServiceSerializer = jsonServiceSerializer ?? throw new ArgumentNullException(nameof(jsonServiceSerializer));
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        }

        /// <summary>
        /// Executes the import of a service configuration file.
        /// Validates the file, imports it, and optionally installs the service.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "linker.xml")]
        public async Task<CommandResult> ExecuteAsync(ImportServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"import configuration from '{opts.Path}'";
            var suggestion = "Check that the file path is correct, the file format is valid JSON or XML, and you have read permissions.";

            return await ExecuteWithHandlingAsync("import", action, suggestion, async () =>
            {
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

                // Validate configuration file type
                if (!Helpers.Helper.TryParseFileType(opts.ConfigFileType, out var configFileType, out var parseError))
                    return CommandResult.Fail(parseError);

                // Validate file path presence
                if (string.IsNullOrWhiteSpace(opts.Path))
                    return CommandResult.Fail(Strings.Msg_PathRequired);

                // ROBUSTNESS: Delegate the complex path canonicalization, UNC blocking, and 
                // defense-in-depth symlink/junction guard checks to the centralized ImportGuard.
                var securityResult = ImportGuard.ValidatePathSecurityAndSize(opts.Path, out string? content);
                if (!securityResult.IsValid || content == null)
                {
                    Logger.Error(securityResult.ErrorMessage!);
                    return CommandResult.Fail(securityResult.ErrorMessage!);
                }

                // Extract the fully resolved, safe path token
                string fullPath = securityResult.ValidPath!.ResolvedPath;

                // Process file based on its type using the validated fullPath
                CommandResult result;

                switch (configFileType)
                {
                    case ConfigFileType.Xml:
                        result = await ProcessXmlAsync(opts, content, cancellationToken: cancellationToken);
                        break;
                    case ConfigFileType.Json:
                        result = await ProcessJsonAsync(opts, content, cancellationToken: cancellationToken);
                        break;
                    default:
                        result = CommandResult.Fail(string.Format(Strings.Msg_UnsupportedFileType, configFileType));
                        Logger.Error($"Unsupported configuration file type: {opts.ConfigFileType}");
                        break;
                }

                if (result.Success)
                {
                    Logger.Info($"Successfully imported {configFileType} configuration from {fullPath}.");
                }
                else
                {
                    Logger.Error($"Failed to import {configFileType} configuration from {fullPath}. Error: {result.Message}");
                }

                return result;
            });
        }

        /// <summary>
        /// Validates and imports an XML service configuration file.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <param name="content">The content of the XML configuration file.</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        private Task<CommandResult> ProcessXmlAsync(ImportServiceOptions opts, string content, CancellationToken cancellationToken = default)
        {
            return ProcessImportInternalAsync(
                opts,
                content,
                "XML",
                xmlContent => _xmlServiceValidator.TryValidate(xmlContent, out var err) ? (true, null) : (false, err),
                dto => _serviceRepository.UpsertAsync(
                        dto, 
                        preserveExistingRuntimeState: true, 
                        preserveExistingCredentials: true,
                        cancellationToken: cancellationToken
                        ),
                _xmlServiceSerializer.Deserialize,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Validates and imports a JSON service configuration file.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <param name="content">The content of the JSON configuration file.</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        [SuppressMessage("Trimming", "IL2026", Justification = "Awaiting full trimming support")]
        private Task<CommandResult> ProcessJsonAsync(ImportServiceOptions opts, string content, CancellationToken cancellationToken = default)
        {
            return ProcessImportInternalAsync(
                opts,
                content,
                "JSON",
                jsonContent => _jsonServiceValidator.TryValidate(jsonContent, out var err) ? (true, null) : (false, err),
                dto => _serviceRepository.UpsertAsync(
                        dto,
                        preserveExistingRuntimeState: true,
                        preserveExistingCredentials: true,
                        cancellationToken: cancellationToken
                        ),
                _jsonServiceSerializer.Deserialize,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Core logic for processing service imports across different formats.
        /// </summary>
        private async Task<CommandResult> ProcessImportInternalAsync(
             ImportServiceOptions opts,
             string content,
             string formatName,
             Func<string, (bool Valid, string? Error)> validator,
             Func<ServiceDto, Task<int>> repoImporter,
             Func<string, ServiceDto?> deserializer,
             CancellationToken cancellationToken = default)
        {
            // 1. Format Validation
            var (isValid, error) = validator(content);
            if (!isValid)
                return CommandResult.Fail(string.Format(Strings.Msg_ImportFormatInvalid, formatName, error));

            // 2. Deserialization
            var dto = deserializer(content);
            if (dto == null)
                return CommandResult.Fail(Strings.Msg_ImportDeserializationFailure);

            // 3. Path validation
            var pathValidation = ValidateServicePaths(dto);
            if (!pathValidation.Success)
                return pathValidation;

            // 4. Repository Import (only after validation passes)
            var affected = await repoImporter(dto);
            if (affected <= 0)
            {
                Logger.Error($"Repository upsert for service '{dto.Name}' reported 0 affected rows.");
                return CommandResult.Fail(string.Format(Strings.Msg_ImportRepoFailure, formatName));
            }

            if (!opts.InstallService)
                return CommandResult.Ok(string.Format(Strings.Msg_ImportSuccessNoInstall, formatName));

            // 5. Installation
            return await TryInstallServiceAsync(dto.Name ?? string.Empty, formatName, cancellationToken);
        }

        /// <summary>
        /// Validates all system paths defined in the service DTO using reflection-based attribute discovery.
        /// </summary>
        /// <param name="service">The service configuration DTO to validate.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or the specific validation failure.</returns>
        private CommandResult ValidateServicePaths(ServiceDto service)
        {
            // Use an anonymous type and standard null checks for C# 7.3 compatibility
            var pathFields = typeof(ServiceDto).GetProperties()
                .Select(p => new
                {
                    Property = p,
                    Attr = p.GetCustomAttribute<ServicePathAttribute>()
                })
                .Where(x => x.Attr != null);

            foreach (var field in pathFields)
            {
                var property = field.Property;
                var attr = field.Attr;

                // Extract the value from the DTO property
                var pathValue = property.GetValue(service) as string;
                bool isPathEmpty = string.IsNullOrWhiteSpace(pathValue);

                // 1. Mandatory Presence Check
                // attr.Required is safe here as it was filtered by the .Where() clause
                if (attr!.Required && isPathEmpty)
                {
                    // Specifically handle ExecutablePath using its dedicated error message
                    if (property.Name == nameof(ServiceDto.ExecutablePath))
                        return CommandResult.Fail(string.Format(Strings.Msg_InvalidExecutablePath, string.Empty));

                    return CommandResult.Fail(string.Format(Strings.Msg_InvalidPathInConfig, attr.Label));
                }

                // 2. Path Validity Check
                if (!isPathEmpty && !_processHelper.ValidatePath(pathValue, attr.IsFile))
                {
                    if (property.Name == nameof(ServiceDto.ExecutablePath))
                        return CommandResult.Fail(string.Format(Strings.Msg_InvalidExecutablePath, pathValue));

                    return CommandResult.Fail(string.Format(Strings.Msg_InvalidPathInConfig, attr.Label));
                }
            }

            return CommandResult.Ok();
        }

        /// <summary>
        /// Attempts to install a service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the service to install.</param>
        /// <param name="format">The configuration file format (XML or JSON).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure of installation.</returns>
        private async Task<CommandResult> TryInstallServiceAsync(string serviceName, string format, CancellationToken cancellationToken = default)
        {
            // 1. Retrieve the service domain object
            var serviceDto = await _serviceRepository.GetByNameAsync(serviceName, cancellationToken: cancellationToken);

            if (serviceDto == null)
            {
                Logger.Error($"Service lookup failed after import. Service: {serviceName}");
                return CommandResult.Fail(string.Format(Strings.Msg_ImportInstallLookupFailure, serviceName));
            }

            var serviceDomain = ServiceMapper.ToDomain(_serviceManager, serviceDto);

            // 2. Attempt service installation
            var res = await serviceDomain.Install(isCLI: true, cancellationToken: cancellationToken);

            if (res.IsSuccess)
            {
                Logger.Info($"Service imported and installed successfully: {serviceName}");
                return CommandResult.Ok(string.Format(Strings.Msg_ImportInstallSuccess, format, serviceName));
            }

            // Log the domain-specific error message
            Logger.Error($"Installation failed for {serviceName}: {res.ErrorMessage}");

            // Return the specific error from the domain logic, or a localized general failure
            return CommandResult.Fail(res.ErrorMessage ?? string.Format(Strings.Msg_ImportInstallGeneralFailure, serviceName));
        }

    }
}