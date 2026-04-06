using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.Logging;
using System;
using System.IO;
using System.Security;
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
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
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
                        var xml = await _serviceRepository.ExportXmlAsync(opts.ServiceName);
                        SaveFile(opts.Path, xml);
                        Logger.Info($"XML configuration file exported successfully to: {opts.Path}");
                        return CommandResult.Ok($"XML configuration exported saved successfully to: {opts.Path}");
                    case ConfigFileType.Json:
                        var json = await _serviceRepository.ExportJsonAsync(opts.ServiceName);
                        SaveFile(opts.Path, json);
                        Logger.Info($"JSON configuration file exported successfully to: {opts.Path}");
                        return CommandResult.Ok($"JSON configuration exported saved successfully to: {opts.Path}");
                }

                return CommandResult.Ok();
            });
        }

        /// <summary>
        /// Safely persists the exported service configuration to a user-defined file path.
        /// Validates that the target is a supported file type and not a protected system location.
        /// </summary>
        /// <param name="userPath">The target file path provided via the CLI.</param>
        /// <param name="content">The serialized configuration string.</param>
        /// <exception cref="SecurityException">Thrown if the path targets a protected system directory.</exception>
        /// <exception cref="ArgumentException">Thrown if the file extension is not .json or .xml.</exception>
        private void SaveFile(string userPath, string content)
        {
            // 1. Canonicalize: Resolve ".." and relative paths to an absolute path
            string fullPath = Path.GetFullPath(userPath);

            // 2. Extension Validation: Ensure we are only writing configuration files
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension != ".json" && extension != ".xml")
            {
                throw new ArgumentException("Only .json and .xml exports are permitted.");
            }

            // 3. System Protection: Block writing to critical Windows directories
            // This prevents overwriting DLLs, drivers, or the SAM database.
            string[] protectedFolders = {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var folder in protectedFolders)
            {
                if (fullPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityException($"Access Denied: Exporting to protected system directory '{folder}' is prohibited.");
                }
            }

            // 4. Directory Creation
            string parentDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // 5. Atomic-style Write
            File.WriteAllText(fullPath, content);
        }
    }
}
