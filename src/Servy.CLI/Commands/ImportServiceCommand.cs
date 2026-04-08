using Newtonsoft.Json;
using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
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
        private readonly IServiceManager _serviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceRepository">The service repository for persisting configurations.</param>
        /// <param name="xmlServiceSerializer">Serializer for XML service configurations.</param>
        /// <param name="serviceManager">Manager to control Windows services.</param>
        public ImportServiceCommand(IServiceRepository serviceRepository, IXmlServiceSerializer xmlServiceSerializer, IServiceManager serviceManager)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _xmlServiceSerializer = xmlServiceSerializer ?? throw new ArgumentNullException(nameof(xmlServiceSerializer));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
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

            return await ExecuteWithHandlingAsync(action, suggestion, async () =>
            {
                // Validate configuration file type
                if (!TryParseFileType(opts.ConfigFileType, out var configFileType, out var parseError))
                    return CommandResult.Fail(parseError);

                // Validate file path
                if (string.IsNullOrWhiteSpace(opts.Path))
                    return CommandResult.Fail("File path is required.");

                // Canonicalize the path to resolve ".." and relative segments
                string fullPath = Path.GetFullPath(opts.Path);

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
                if (!File.Exists(fullPath))
                {
                    var errorMsg = $"[Import{configFileType}] File not found: {fullPath}";
                    Logger.Error(errorMsg);
                    return CommandResult.Fail(errorMsg);
                }

                // Process file based on its type
                CommandResult result;

                switch (configFileType)
                {
                    case ConfigFileType.Xml:
                        result = await ProcessXmlAsync(opts);
                        if (result.Success)
                        {
                            Logger.Info($"Successfully imported XML configuration from {opts.Path}.");
                        }
                        else
                        {
                            Logger.Error($"Failed to import XML configuration from {opts.Path}. Error: {result.Message}");
                        }
                        break;
                    case ConfigFileType.Json:
                        result = await ProcessJsonAsync(opts);
                        if (result.Success)
                        {
                            Logger.Info($"Successfully imported JSON configuration from {opts.Path}.");
                        }
                        else
                        {
                            Logger.Error($"Failed to import JSON configuration from {opts.Path}. Error: {result.Message}");
                        }
                        break;
                    default:
                        result = CommandResult.Fail("Unsupported configuration file type.");
                        Logger.Error($"Unsupported configuration file type: {opts.ConfigFileType}");
                        break;
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
        private Task<CommandResult> ProcessXmlAsync(ImportServiceOptions opts)
        {
            return ProcessImportInternalAsync(
                opts,
                "XML",
                content => XmlServiceValidator.TryValidate(content, out var err) ? (true, null) : (false, err),
                content => _serviceRepository.ImportXmlAsync(content),
                content => _xmlServiceSerializer.Deserialize(content));
        }

        /// <summary>
        /// Validates and imports a JSON service configuration file.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        [SuppressMessage("Trimming", "IL2026", Justification = "Awaiting full trimming support")]
        private Task<CommandResult> ProcessJsonAsync(ImportServiceOptions opts)
        {
            return ProcessImportInternalAsync(
                opts,
                "JSON",
                content => JsonServiceValidator.TryValidate(content, out var err) ? (true, null) : (false, err),
                content => _serviceRepository.ImportJsonAsync(content),
                content => JsonConvert.DeserializeObject<ServiceDto>(content, JsonSecurity.UntrustedDataSettings));
        }

        /// <summary>
        /// Core logic for processing service imports across different formats.
        /// </summary>
        private async Task<CommandResult> ProcessImportInternalAsync(
            ImportServiceOptions opts,
            string formatName,
            Func<string, (bool Valid, string? Error)> validator,
            Func<string, Task<bool>> repoImporter,
            Func<string, ServiceDto?> deserializer)
        {
            var content = await File.ReadAllTextAsync(opts.Path);

            // 1. Format Validation
            var (isValid, error) = validator(content);
            if (!isValid)
                return CommandResult.Fail($"{formatName} file not valid: {error}");

            // 2. Repository Import
            if (!await repoImporter(content))
                return CommandResult.Fail($"Failed to import {formatName} configuration.");

            if (!opts.InstallService)
                return CommandResult.Ok($"{formatName} configuration imported successfully.");

            // 3. Deserialization
            var service = deserializer(content);
            if (service == null)
                return CommandResult.Fail($"Service imported but failed to deserialize for installation.");

            // 4. Exhaustive Path Validation
            var pathValidation = ValidateServicePaths(service);
            if (!pathValidation.Success)
                return pathValidation;

            // 5. Installation
            return await TryInstallServiceAsync(service.Name, formatName);
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
                return CommandResult.Fail("Invalid executable path in configuration.");

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
                    return CommandResult.Fail($"Invalid {label} in configuration.");
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
            // Retrieve the service domain object
            var serviceDomain = await _serviceRepository.GetDomainServiceByNameAsync(_serviceManager, serviceName);
            if (serviceDomain == null)
            {
                Logger.Error($"Service imported but failed to find the service for installation. Service name: {serviceName}");
                return CommandResult.Fail($"Service imported but failed to find the service for installation.");
            }

            try
            {
                // Attempt service installation
                var res = await serviceDomain.Install(isCLI: true);
                if (res.IsSuccess)
                {
                    Logger.Info($"Service imported and installed successfully. Service name: {serviceName}");
                    return CommandResult.Ok($"{format} configuration saved and service installed successfully.");
                }
                else
                {
                    Logger.Error(res.ErrorMessage);
                    return CommandResult.Fail(res.ErrorMessage!);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Service imported but failed to install the service. Error: {ex}");
                return CommandResult.Fail($"Service imported but failed to install the service. Error: {ex.Message}");
            }
        }

    }
}
