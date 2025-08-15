using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.Core.Data;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to restart an existing Windows service.
    /// </summary>
    public class ExportServiceCommand : BaseCommand
    {
        private readonly IServiceRepository _serviceRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportServiceCommand"/> class.
        /// </summary>
        /// <param name="serviceRepository">Service repository.</param>
        public ExportServiceCommand(IServiceRepository serviceRepository)
        {
            _serviceRepository = serviceRepository;
        }

        /// <summary>
        /// Executes the restart of the service with the specified options.
        /// </summary>
        /// <param name="opts">Export service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> Execute(ExportServiceOptions opts)
        {
            return await ExecuteWithHandlingAsync(async () =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(opts.ServiceName))
                        return CommandResult.Fail("Service name is required.");

                    ConfigFileType configFileType;
                    if (string.IsNullOrWhiteSpace(opts.ConfigFileType) || !Enum.TryParse(opts.ConfigFileType, true, out configFileType))
                        return CommandResult.Fail("Configuration output file type is required (xml or json).");

                    if (string.IsNullOrWhiteSpace(opts.Path))
                        return CommandResult.Fail("Output file path is required.");

                    var exists = await _serviceRepository.GetByNameAsync(opts.ServiceName);

                    if (exists == null)
                        return CommandResult.Fail("Service not found.");

                    switch (configFileType)
                    {
                        case ConfigFileType.Xml:
                            var xml = await _serviceRepository.ExportXML(opts.ServiceName);
                            SaveFile(opts.Path, xml);
                            return CommandResult.Ok($"XML configuration file saved successfully to: {opts.Path}");
                        case ConfigFileType.Json:
                            var json = await _serviceRepository.ExportJSON(opts.ServiceName);
                            SaveFile(opts.Path, json);
                            return CommandResult.Ok($"JSON configuration file saved successfully to: {opts.Path}");
                    }

                    return CommandResult.Ok();
                }
                catch (Exception ex)
                {
                    return CommandResult.Fail($"An unhandled error occured: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Saves content to file and creates parent directory if it does not exist.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="content">Content.</param>
        private void SaveFile(string path, string content)
        {
            var parentDir = Path.GetDirectoryName(path);
            if (!Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);
            File.WriteAllText(path, content);

        }
    }
}
;