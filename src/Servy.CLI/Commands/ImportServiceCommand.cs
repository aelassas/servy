using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to restart an existing Windows service.
    /// </summary>
    public class ImportServiceCommand : BaseCommand
    {
        private readonly IServiceRepository _serviceRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceRepository">Service repository.</param>
        public ImportServiceCommand(IServiceRepository serviceRepository)
        {
            _serviceRepository = serviceRepository;
        }

        /// <summary>
        /// Executes the restart of the service with the specified options.
        /// </summary>
        /// <param name="opts">Import service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> Execute(ImportServiceOptions opts)
        {
            return await ExecuteWithHandlingAsync(async () =>
            {
                try
                {
                    ConfigFileType configFileType;
                    if (string.IsNullOrWhiteSpace(opts.ConfigFileType) || !Enum.TryParse(opts.ConfigFileType, true, out configFileType))
                        return CommandResult.Fail("Configuration output file type is required (xml or json).");

                    if (string.IsNullOrWhiteSpace(opts.Path))
                        return CommandResult.Fail("File path is required.");

                    if (!File.Exists(opts.Path))
                        return CommandResult.Fail($"File not found: {opts.Path}");

                    switch (configFileType)
                    {
                        case ConfigFileType.Xml:
                            var xml = File.ReadAllText(opts.Path);
                            string xmlErrorMsg;
                            var xmlValid = XmlServiceValidator.TryValidate(xml, out xmlErrorMsg);
                            if (!xmlValid)
                                return CommandResult.Fail($"XML file not valid: ${xmlErrorMsg}");
                            await _serviceRepository.ImportXML(xml);
                            return CommandResult.Ok($"XML configuration saved successfully.");
                        case ConfigFileType.Json:
                            var json = File.ReadAllText(opts.Path);
                            string jsonErrorMsg;
                            var jsonValid = JsonServiceValidator.TryValidate(json, out jsonErrorMsg);
                            if (!jsonValid)
                                return CommandResult.Fail($"JSON file not valid: ${jsonErrorMsg}");
                            await _serviceRepository.ImportJSON(json);
                            return CommandResult.Ok($"JSON configuration saved successfully.");
                    }

                    return CommandResult.Ok();
                }
                catch (Exception ex)
                {
                    return CommandResult.Fail($"An unhandled error occured: {ex.Message}");
                }
            });
        }

    }
}
