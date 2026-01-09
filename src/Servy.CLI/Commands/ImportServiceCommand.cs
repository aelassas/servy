using Newtonsoft.Json;
using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Services;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

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
            _serviceRepository = serviceRepository;
            _xmlServiceSerializer = xmlServiceSerializer;
            _serviceManager = serviceManager;
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
            return await ExecuteWithHandlingAsync(async () =>
            {
                // Validate configuration file type
                if (!TryParseFileType(opts.ConfigFileType, out var configFileType, out var parseError))
                    return CommandResult.Fail(parseError);

                // Validate file path
                if (string.IsNullOrWhiteSpace(opts.Path))
                    return CommandResult.Fail("File path is required.");

                if (!File.Exists(opts.Path))
                    return CommandResult.Fail($"File not found: {opts.Path}");

                // Process file based on its type
                CommandResult result;

                switch (configFileType)
                {
                    case ConfigFileType.Xml:
                        result = await ProcessXmlAsync(opts);
                        break;
                    case ConfigFileType.Json:
                        result = await ProcessJsonAsync(opts);
                        break;
                    default:
                        result = CommandResult.Fail("Unsupported configuration file type.");
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
        private static bool TryParseFileType(string input, out ConfigFileType fileType, out string error)
        {
            if (string.IsNullOrWhiteSpace(input) || !Enum.TryParse(input, true, out fileType))
            {
                fileType = default;
                error = "Configuration output file type is required (xml or json).";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates and imports an XML service configuration file.
        /// Optionally installs the service after import.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        private async Task<CommandResult> ProcessXmlAsync(ImportServiceOptions opts)
        {
            var xml = File.ReadAllText(opts.Path);

            // Validate XML format
            if (!XmlServiceValidator.TryValidate(xml, out var validationError))
                return CommandResult.Fail($"XML file not valid: {validationError}");

            // Import XML configuration into repository
            var imported = await _serviceRepository.ImportXML(xml);
            if (!imported)
                return CommandResult.Fail("Failed to import XML configuration.");

            // If installation not requested, return success
            if (!opts.InstallService)
                return CommandResult.Ok("XML configuration saved successfully.");

            // Deserialize service for installation
            var service = _xmlServiceSerializer.Deserialize(xml);
            if (service == null)
                return CommandResult.Fail("Service imported but failed to deserialize for installation.");

            // Attempt installation
            return await TryInstallServiceAsync(service.Name, "XML");
        }

        /// <summary>
        /// Validates and imports a JSON service configuration file.
        /// Optionally installs the service after import.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<En attente>")]
        private async Task<CommandResult> ProcessJsonAsync(ImportServiceOptions opts)
        {
            var json = File.ReadAllText(opts.Path);

            // Validate JSON format
            if (!JsonServiceValidator.TryValidate(json, out var validationError))
                return CommandResult.Fail($"JSON file not valid: {validationError}");

            // Import JSON configuration into repository
            var imported = await _serviceRepository.ImportJSON(json);
            if (!imported)
                return CommandResult.Fail("Failed to import JSON configuration.");

            // If installation not requested, return success
            if (!opts.InstallService)
                return CommandResult.Ok("JSON configuration saved successfully.");

            // Deserialize service for installation
            var service = JsonConvert.DeserializeObject<ServiceDto>(json);
            if (service == null)
                return CommandResult.Fail("Service imported but failed to deserialize for installation.");

            // Attempt installation
            return await TryInstallServiceAsync(service.Name, "JSON");
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
                return CommandResult.Fail($"Service imported but failed to find the service for installation.");

            try
            {
                // Attempt service installation
                var installed = await serviceDomain.Install();
                if (installed)
                    return CommandResult.Ok($"{format} configuration saved and service installed successfully.");
                else
                    return CommandResult.Fail("Service imported but failed to install the service.");
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"Service imported but failed to install the service. Error: {ex.Message}");
            }
        }
    }
}
