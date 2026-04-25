using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Mappers;
using Servy.Core.Security;
using Servy.Core.Services;
using System.Diagnostics.CodeAnalysis;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceRepository">The service repository for persisting configurations.</param>
        /// <param name="xmlServiceSerializer">Serializer for XML service configurations.</param>
        /// <param name="jsonServiceSerializer">Serializer for JSON service configurations.</param>
        /// <param name="serviceManager">Manager to control Windows services.</param>
        /// <param name="xmlServiceValidator">Validator for XML service configurations.</param>
        /// <param name="jsonServiceValidator">Validator for JSON service configurations.</param>
        public ImportServiceCommand(
            IServiceRepository serviceRepository,
            IXmlServiceSerializer xmlServiceSerializer,
            IJsonServiceSerializer jsonServiceSerializer,
            IServiceManager serviceManager,
            IXmlServiceValidator xmlServiceValidator,
            IJsonServiceValidator jsonServiceValidator)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _xmlServiceValidator = xmlServiceValidator ?? throw new ArgumentNullException(nameof(xmlServiceValidator));
            _jsonServiceValidator = jsonServiceValidator ?? throw new ArgumentNullException(nameof(jsonServiceValidator));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _xmlServiceSerializer = xmlServiceSerializer ?? throw new ArgumentNullException(nameof(xmlServiceSerializer));
            _jsonServiceSerializer = jsonServiceSerializer ?? throw new ArgumentNullException(nameof(jsonServiceSerializer));
        }

        /// <summary>
        /// Executes the import of a service configuration file.
        /// Validates the file, imports it, and optionally installs the service.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "linker.xml")]
        public async Task<CommandResult> Execute(ImportServiceOptions opts)
        {
            var action = $"import configuration from '{opts.Path}'";
            var suggestion = "Check that the file path is correct, the file format is valid JSON or XML, and you have read permissions.";

            return await ExecuteWithHandlingAsync("import", action, suggestion, async () =>
            {
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

                // Validate configuration file type
                if (!TryParseFileType(opts.ConfigFileType, out var configFileType, out var parseError))
                    return CommandResult.Fail(parseError);

                // Validate file path
                if (string.IsNullOrWhiteSpace(opts.Path))
                    return CommandResult.Fail(Strings.Msg_PathRequired);

                // Canonicalize the path to resolve ".." and relative segments
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(opts.Path);
                }
                catch (Exception ex)
                {
                    return CommandResult.Fail($"{Strings.Msg_InvalidPath}: {ex.Message}");
                }

                // Extension Validation
                string extension = Path.GetExtension(fullPath).ToLowerInvariant();
                string[] allowedExtensions = { ".json", ".xml" };

                if (!allowedExtensions.Contains(extension))
                {
                    var errorMsg = $"[Import{configFileType}] Security Alert: Invalid file type '{extension}'. Only .json and .xml files are supported.";
                    Logger.Error(errorMsg);
                    return CommandResult.Fail(errorMsg);
                }

                // Existence Check
                var fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists)
                {
                    var errorMsg = $"[Import{configFileType}] File not found: {fullPath}";
                    Logger.Error(errorMsg);
                    return CommandResult.Fail(errorMsg);
                }

                if (fileInfo.Length > (long)AppConfig.MaxConfigFileSizeMB * 1024 * 1024)
                {
                    var errorMsg = string.Format(Strings.Msg_ConfigSizeLimitReached, fullPath);
                    Logger.Error(errorMsg);
                    return CommandResult.Fail(errorMsg);
                }

                // Process file based on its type
                CommandResult result;

                switch (configFileType)
                {
                    case ConfigFileType.Xml:
                        result = await ProcessXmlAsync(opts, fullPath);
                        break;
                    case ConfigFileType.Json:
                        result = await ProcessJsonAsync(opts, fullPath);
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
        /// Tries to parse the string input into a <see cref="ConfigFileType"/>.
        /// </summary>
        /// <param name="input">The input string (xml or json).</param>
        /// <param name="fileType">The parsed <see cref="ConfigFileType"/> if successful.</param>
        /// <param name="error">Error message if parsing fails.</param>
        /// <returns>True if parsing succeeds; otherwise false.</returns>
        private static bool TryParseFileType(string? input, out ConfigFileType fileType, out string error)
        {
            if (string.IsNullOrWhiteSpace(input) || !Enum.TryParse(input, true, out fileType))
            {
                fileType = default;
                error = "Configuration input file type is required (xml or json).";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates and imports an XML service configuration file.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        private Task<CommandResult> ProcessXmlAsync(ImportServiceOptions opts, string fullPath)
        {
            return ProcessImportInternalAsync(
                opts,
                fullPath,
                "XML",
                content => _xmlServiceValidator.TryValidate(content, out var err) ? (true, null) : (false, err),
                content => _serviceRepository.ImportXmlAsync(content),
                _xmlServiceSerializer.Deserialize);
        }

        /// <summary>
        /// Validates and imports a JSON service configuration file.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        [SuppressMessage("Trimming", "IL2026", Justification = "Awaiting full trimming support")]
        private Task<CommandResult> ProcessJsonAsync(ImportServiceOptions opts, string fullPath)
        {
            return ProcessImportInternalAsync(
                opts,
                fullPath,
                "JSON",
                content => _jsonServiceValidator.TryValidate(content, out var err) ? (true, null) : (false, err),
                content => _serviceRepository.ImportJsonAsync(content),
                _jsonServiceSerializer.Deserialize);
        }

        /// <summary>
        /// Core logic for processing service imports across different formats.
        /// </summary>
        private async Task<CommandResult> ProcessImportInternalAsync(
             ImportServiceOptions opts,
             string fullPath,
             string formatName,
             Func<string, (bool Valid, string? Error)> validator,
             Func<string, Task<bool>> repoImporter,
             Func<string, ServiceDto?> deserializer)
        {
            var content = await File.ReadAllTextAsync(fullPath);

            // 1. Format Validation
            var (isValid, error) = validator(content);
            if (!isValid)
                return CommandResult.Fail(string.Format(Strings.Msg_ImportFormatInvalid, formatName, error));

            // 2. Repository Import
            if (!await repoImporter(content))
                return CommandResult.Fail(string.Format(Strings.Msg_ImportRepoFailure, formatName));

            if (!opts.InstallService)
                return CommandResult.Ok(string.Format(Strings.Msg_ImportSuccessNoInstall, formatName));

            // 3. Deserialization
            var dto = deserializer(content);
            if (dto == null)
                return CommandResult.Fail(Strings.Msg_ImportDeserializationFailure);

            // 4. Exhaustive Path Validation
            var pathValidation = ValidateServicePaths(dto);
            if (!pathValidation.Success)
                return pathValidation;

            // 5. Installation
            return await TryInstallServiceAsync(dto.Name, formatName);
        }

        /// <summary>
        /// Validates all executable and directory paths defined in the service configuration.
        /// </summary>
        /// <param name="service">The service data transfer object.</param>
        /// <returns>A <see cref="CommandResult"/> indicating if all paths are valid.</returns>
        private CommandResult ValidateServicePaths(ServiceDto service)
        {
            // Required path
            if (!ProcessHelper.ValidatePath(service.ExecutablePath, isFile: true))
                return CommandResult.Fail(string.Format(Strings.Msg_InvalidExecutablePath, service.ExecutablePath));

            // Optional paths
            var check = new[]
            {
                (service.StartupDirectory, false, "startup directory"),
                (service.FailureProgramPath, true, "failure program executable path"),
                (service.FailureProgramStartupDirectory, false, "failure program startup directory"),
                (service.PreLaunchExecutablePath, true, "pre-launch executable path"),
                (service.PreLaunchStartupDirectory, false, "pre-launch startup directory"),
                (service.PostLaunchExecutablePath, true, "post-launch executable path"),
                (service.PostLaunchStartupDirectory, false, "post-launch startup directory"),
                (service.PreStopExecutablePath, true, "pre-stop executable path"),
                (service.PreStopStartupDirectory, false, "pre-stop startup directory"),
                (service.PostStopExecutablePath, true, "post-stop executable path"),
                (service.PostStopStartupDirectory, false, "post-stop startup directory")
            };

            foreach (var (path, isFile, label) in check)
            {
                if (!string.IsNullOrWhiteSpace(path) && !ProcessHelper.ValidatePath(path, isFile))
                    return CommandResult.Fail(string.Format(Strings.Msg_InvalidPathInConfig, label));
            }

            return CommandResult.Ok();
        }

        /// <summary>
        /// Attempts to install a service by its name.
        /// </summary>
        /// <param name="serviceName">The name of the service to install.</param>
        /// <param name="format">The configuration file format (XML or JSON).</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure of installation.</returns>
        private async Task<CommandResult> TryInstallServiceAsync(string serviceName, string format)
        {
            // 1. Retrieve the service domain object
            var serviceDto = await _serviceRepository.GetByNameAsync(serviceName);

            if (serviceDto == null)
            {
                Logger.Error($"Service lookup failed after import. Service: {serviceName}");
                return CommandResult.Fail(string.Format(Strings.Msg_ImportInstallLookupFailure, serviceName));
            }

            var serviceDomain = ServiceMapper.ToDomain(_serviceManager, serviceDto);

            // 2. Attempt service installation
            var res = await serviceDomain.Install(isCLI: true);

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
