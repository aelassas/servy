using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Security;
using System.Security;
using System.Text.RegularExpressions;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to export an existing Windows service.
    /// </summary>
    public class ExportServiceCommand : BaseCommand
    {
        #region Export Security Constants

        /// <summary>
        /// A collection of legacy Windows reserved device names that cannot be used as filenames.
        /// </summary>
        private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL"
        };

        /// <summary>
        /// Matches legacy Windows serial (COM) and parallel (LPT) port names (1-9).
        /// Includes a 200ms timeout to mitigate ReDoS.
        /// </summary>
        private static readonly Regex ReservedPortRegex = new Regex(
            @"^(COM|LPT)[1-9]$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            AppConfig.InputRegexTimeout);

        #endregion

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
        /// Executes the export of the service with the specified options.
        /// </summary>
        /// <param name="opts">Export service options.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> Execute(ExportServiceOptions opts)
        {
            var action = $"export configuration for service '{opts.ServiceName}'";
            var suggestion = "Ensure the service exists in the database and you have write permissions to the destination path.";

            return await ExecuteWithHandlingAsync("export", action, suggestion, async () =>
            {
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

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
        /// Validates that the target is a supported file type, not a UNC path, 
        /// not a reserved device name, and not a protected system location.
        /// Resolves NTFS junctions and symlinks to prevent path traversal bypasses.
        /// </summary>
        /// <param name="userPath">The target file path provided via the CLI.</param>
        /// <param name="content">The serialized configuration string.</param>
        /// <exception cref="SecurityException">Thrown if the path targets a protected system directory or a UNC path.</exception>
        /// <exception cref="ArgumentException">Thrown if the path is a reserved device name or an unsupported file type.</exception>
        private void SaveFile(string userPath, string content)
        {
            // 1. Canonicalize: Resolve ".." and relative paths to a strictly absolute path.
            // This is the foundation for all subsequent security checks.
            string fullPath = Path.GetFullPath(userPath);

            // 2. Extension Validation
            string extension = Path.GetExtension(fullPath);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Security Alert: Only .json and .xml exports are permitted.");
            }

            // 3. UNC Path Block (Exfiltration Guard)
            // Prevents writing sensitive config data (including encrypted passwords) to remote shares.
            bool isUncUri = Uri.TryCreate(fullPath, UriKind.Absolute, out var uri) && uri.IsUnc;
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || isUncUri)
            {
                throw new SecurityException("Security Alert: Exporting to UNC paths is prohibited to prevent data exfiltration.");
            }

            // 4. Link/Junction Resolution
            // We resolve the final path to ensure that NTFS redirection (junctions/symlinks) 
            // isn't being used to hide a destination inside a protected system folder.
            string finalResolvedPath = fullPath;
            string? directoryPath = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
            {
                var dirInfo = new DirectoryInfo(directoryPath);

                // Iteratively resolve link targets to handle chained junctions
                while (!string.IsNullOrEmpty(dirInfo.LinkTarget))
                {
                    var target = dirInfo.ResolveLinkTarget(true);
                    if (target is DirectoryInfo resolvedDir)
                    {
                        dirInfo = resolvedDir;
                    }
                    else
                    {
                        break;
                    }
                }

                finalResolvedPath = Path.Combine(dirInfo.FullName, Path.GetFileName(fullPath));
            }

            // 5. Reserved Device Name Block (DOS/Data Loss Guard)
            // Prevents writing to CON, NUL, COM1, etc., which can hang the process or discard data.
            string fileName = Path.GetFileNameWithoutExtension(finalResolvedPath);
            try
            {
                if (ReservedDeviceNames.Contains(fileName) || ReservedPortRegex.IsMatch(fileName))
                {
                    throw new ArgumentException($"Security Alert: '{fileName}' is a reserved Windows device name and cannot be used.");
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Fallback: If regex fails to validate the filename in time, block the write.
                throw new SecurityException("Security Alert: Filename validation timed out. Export aborted for safety.");
            }

            // 6. System Protection: Block writing to critical Windows directories
            string[] protectedFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            // We use finalResolvedPath here to catch junction-based bypasses.
            var violatedFolder = protectedFolders
                .FirstOrDefault(folder => !string.IsNullOrEmpty(folder) &&
                                          finalResolvedPath.StartsWith(
                                              folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                                              StringComparison.OrdinalIgnoreCase));

            if (violatedFolder != null)
            {
                throw new SecurityException($"Access Denied: The resolved path targets protected system directory '{violatedFolder}'. Export prohibited.");
            }

            // 7. Directory Creation
            string? parentDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // 8. Final Atomic Write
            File.WriteAllText(fullPath, content);
        }
    }
}