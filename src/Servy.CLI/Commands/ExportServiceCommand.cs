using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.Logging;
using System;
using System.IO;
using System.Linq;
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
            var action = $"export configuration for service '{opts.ServiceName}'";
            var suggestion = "Ensure the service exists in the database and you have write permissions to the destination path.";

            return await ExecuteWithHandlingAsync(action, suggestion, async () =>
            {
                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail(Strings.Msg_ServiceNameRequired);

                ConfigFileType configFileType;
                if (string.IsNullOrWhiteSpace(opts.ConfigFileType) || !Enum.TryParse(opts.ConfigFileType, true, out configFileType))
                    return CommandResult.Fail(Strings.Msg_InvalidConfigFileType);

                if (string.IsNullOrWhiteSpace(opts.Path))
                    return CommandResult.Fail(Strings.Msg_PathRequired);

                var exists = await _serviceRepository.GetByNameAsync(opts.ServiceName);

                if (exists == null)
                    return CommandResult.Fail(Strings.Msg_ServiceNotFound);

                string content;
                string typeLabel = configFileType.ToString().ToUpper();

                // 1. Perform Export based on type using standard switch syntax
                switch (configFileType)
                {
                    case ConfigFileType.Xml:
                        content = await _serviceRepository.ExportXmlAsync(opts.ServiceName);
                        break;

                    case ConfigFileType.Json:
                        content = await _serviceRepository.ExportJsonAsync(opts.ServiceName);
                        break;

                    default:
                        // Providing a specific failure if an unsupported type is somehow passed
                        return CommandResult.Fail(string.Format(Strings.Msg_UnsupportedFileType, configFileType));
                }

                // 2. Save the file (Logic extracted from the switch to avoid duplication)
                SaveFile(opts.Path, content);

                // 3. Centralized Localized Logging and Response
                var successMessage = string.Format(Strings.Msg_ExportSuccess, typeLabel, opts.Path);

                Logger.Info(successMessage);
                return CommandResult.Ok(successMessage);
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

            // Find the first protected folder that matches the start of the fullPath
            var violatedFolder = protectedFolders
                .FirstOrDefault(folder => !string.IsNullOrEmpty(folder) &&
                                          fullPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase));

            if (violatedFolder != null)
            {
                throw new SecurityException($"Access Denied: Exporting to protected system directory '{violatedFolder}' is prohibited.");
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
