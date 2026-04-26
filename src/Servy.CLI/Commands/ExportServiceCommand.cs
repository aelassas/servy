using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Servy.Core.Native.NativeMethods;

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
            string fullPath = Path.GetFullPath(userPath);

            // 1. Extension & UNC Validation
            string extension = Path.GetExtension(fullPath);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Security Alert: Only .json and .xml exports are permitted.");
            }

            bool isUncUri = Uri.TryCreate(fullPath, UriKind.Absolute, out var uri) && uri.IsUnc;
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || isUncUri)
            {
                throw new SecurityException("Security Alert: Exporting to UNC paths is prohibited.");
            }

            // 2. Resolve Final Path (Junction/Symlink Protection)
            string finalResolvedPath = GetFinalPathName(fullPath);

            // 3. Reserved Device Name Block
            string fileName = Path.GetFileNameWithoutExtension(finalResolvedPath);
            if (ReservedDeviceNames.Contains(fileName) || ReservedPortRegex.IsMatch(fileName))
            {
                throw new ArgumentException($"Security Alert: '{fileName}' is a reserved Windows device name.");
            }

            // 4. System Protection Check
            string[] protectedFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var folder in protectedFolders)
            {
                if (string.IsNullOrEmpty(folder)) continue;
                string checkFolder = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                if (finalResolvedPath.StartsWith(checkFolder, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityException($"Access Denied: Path targets protected directory '{folder}'.");
                }
            }

            // 5. Directory Creation & Write
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content);
        }

        /// <summary>
        /// Uses the Windows API to resolve the true physical path of a file or directory,
        /// bypassing any NTFS junctions or symbolic links.
        /// </summary>
        private string GetFinalPathName(string path)
        {
            string directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                return path;

            IntPtr hFile = CreateFile(
                directoryPath,
                0, // No access needed to read metadata
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (hFile == new IntPtr(-1)) return path;

            try
            {
                StringBuilder sb = new StringBuilder(1024);
                uint result = GetFinalPathNameByHandle(hFile, sb, (uint)sb.Capacity, VOLUME_NAME_DOS);

                if (result == 0) return path;

                string resolvedDir = sb.ToString();
                // Windows often prefixes resolved paths with \\?\
                if (resolvedDir.StartsWith(@"\\?\")) resolvedDir = resolvedDir.Substring(4);

                return Path.Combine(resolvedDir, Path.GetFileName(path));
            }
            finally
            {
                CloseHandle(hFile);
            }
        }
    }
}