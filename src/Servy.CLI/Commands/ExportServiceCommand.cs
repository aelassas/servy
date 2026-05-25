using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Security;
using Servy.Core.Validators;
using System.Security;
using System.Text;

namespace Servy.CLI.Commands
{
    /// <summary>
    /// Command to export an existing Windows service.
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
        /// Executes the export of the service with the specified options.
        /// </summary>
        /// <param name="opts">Export service options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success or failure.</returns>
        public async Task<CommandResult> ExecuteAsync(ExportServiceOptions opts, CancellationToken cancellationToken = default)
        {
            var action = $"export configuration for service '{opts.ServiceName}'";
            var suggestion = "Ensure the service exists in the database and you have write permissions to the destination path.";

            return await ExecuteWithHandlingAsync("export", action, suggestion, async () =>
            {
                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail(Strings.Msg_ServiceNameRequired);

                // Validate configuration file type
                if (!Helpers.Helper.TryParseFileType(opts.ConfigFileType, out var configFileType, out var parseError))
                    return CommandResult.Fail(parseError);

                if (string.IsNullOrWhiteSpace(opts.Path))
                    return CommandResult.Fail(Strings.Msg_PathRequired);

                var exists = await _serviceRepository.GetByNameAsync(opts.ServiceName, cancellationToken: cancellationToken);

                if (exists == null)
                    return CommandResult.Fail(Strings.Msg_ServiceNotFound);

                string content;
                string typeLabel = configFileType.ToString().ToUpperInvariant();

                // 1. Perform Export based on type using standard switch syntax
                switch (configFileType)
                {
                    case ConfigFileType.Xml:
                        content = await _serviceRepository.ExportXmlAsync(opts.ServiceName, cancellationToken: cancellationToken);
                        break;

                    case ConfigFileType.Json:
                        content = await _serviceRepository.ExportJsonAsync(opts.ServiceName, cancellationToken: cancellationToken);
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

            // Mapped Network Drive Guard
            // Catches network paths mapped to local drive letters (e.g. Z:\config.xml -> \\attacker\share)
            try
            {
                string? root = Path.GetPathRoot(fullPath);
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.DriveType == DriveType.Network)
                    {
                        throw new SecurityException("Security Alert: Exporting to network drives (including mapped UNC shares) is prohibited.");
                    }
                }
            }
            catch (ArgumentException)
            {
                /* Invalid or unprobed drive root - fall through to subsequent security blocks */
            }

            // 4. Reparse Point Guard (Directory and File Level)
            // Hardening: We explicitly block reparse points (symlinks/junctions) at both the directory 
            // and file level to prevent path redirection attacks and UNC bypasses.
            if (Helper.HasAncestorReparsePoint(fullPath))
            {
                throw new SecurityException("Security Alert: Exporting configurations through directory junctions or symlinks is prohibited to prevent path redirection attacks.");
            }

            // Guard against file-level symbolic links
            var fileLinkInfo = new FileInfo(fullPath);
            fileLinkInfo.Refresh();
            if (!string.IsNullOrEmpty(fileLinkInfo.LinkTarget) || (fileLinkInfo.Exists && (fileLinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint))
            {
                throw new SecurityException("Security Alert: Exporting configurations through file symbolic links or junctions is prohibited to prevent path redirection attacks.");
            }

            // 5. Reserved Device Name Block (DOS/Data Loss Guard)
            string fileName = Path.GetFileName(fullPath);
            int firstDotIndex = fileName.IndexOf('.');
            string firstSegment = firstDotIndex >= 0 ? fileName.Substring(0, firstDotIndex) : fileName;

            if (ReservedNames.ReservedDeviceNames.Contains(firstSegment))
            {
                throw new ArgumentException($"Security Alert: '{firstSegment}' is a reserved Windows device name and cannot be used.");
            }

            // 6. System Protection: Block writing to critical Windows directories
            string[] protectedFolders =
            {
                 Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                 Environment.GetFolderPath(Environment.SpecialFolder.System),
                 Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            var violatedFolder = protectedFolders
                .FirstOrDefault(folder => !string.IsNullOrEmpty(folder) &&
                                          fullPath.StartsWith(
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

            // 8. Handle Resolution & Guarded Write
            // Belt-and-braces: Open the file exclusively, verify the underlying volume path, and ONLY THEN write.
            // This intercepts DFS referrals, iSCSI mounts, and complex subst mappings before data leaves the box.
            bool createdByUs = !File.Exists(fullPath);
            try
            {
                // Open with OpenOrCreate so the file content is NOT truncated or modified upon opening.
                // This ensures no metadata write commands cross the network stack before verification.
                using (var fileStream = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var safeHandle = fileStream.SafeFileHandle;

                    if (!safeHandle.IsInvalid)
                    {
                        // Determine buffer size required
                        uint requiredSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, null!, 0, NativeMethods.VOLUME_NAME_DOS);

                        if (requiredSize > 0)
                        {
                            // Allocate buffer and retrieve the final physical path representation
                            var pathBuilder = new StringBuilder((int)requiredSize);
                            uint resultSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, pathBuilder, requiredSize, NativeMethods.VOLUME_NAME_DOS);

                            if (resultSize > 0)
                            {
                                string finalPathName = pathBuilder.ToString();

                                // NORMALIZE: Clean up extended device namespaces safely to track backends accurately
                                string normalizedPath = finalPathName;
                                bool unwrappedUnc = false;

                                if (normalizedPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Reconstruct the explicit double-backslash structural layout for accurate evaluation
                                    normalizedPath = @"\\" + normalizedPath.Substring(@"\\?\UNC\".Length);
                                    unwrappedUnc = true;
                                }
                                else if (normalizedPath.StartsWith(@"\\?\", StringComparison.Ordinal))
                                {
                                    // Strip standard local volume namespace tags (e.g. \\?\C:\... -> C:\...)
                                    normalizedPath = normalizedPath.Substring(4);
                                }

                                bool finalIsUnc = unwrappedUnc || (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var finalUri) && finalUri.IsUnc);

                                // Now check the normalized path
                                if (unwrappedUnc ||
                                    normalizedPath.StartsWith(@"\\", StringComparison.Ordinal) ||
                                    finalIsUnc)
                                {
                                    throw new SecurityException("Security Alert: Resolved file target points directly to a UNC destination. Export aborted.");
                                }

                                // NEW: re-check protected folders against the RESOLVED path
                                var resolvedViolation = protectedFolders.FirstOrDefault(folder =>
                                    !string.IsNullOrEmpty(folder) &&
                                    normalizedPath.StartsWith(
                                        folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                                        StringComparison.OrdinalIgnoreCase));

                                if (resolvedViolation != null)
                                {
                                    throw new SecurityException(
                                        $"Access Denied: Resolved file target points into protected system directory '{resolvedViolation}'. Export aborted."
                                        );
                                }
                            }
                        }
                    }

                    // 9. Final Materialization
                    // The volume handle is verified as local. It is now cryptographically safe to push sensitive data to the disk.
                    using (var sw = new StreamWriter(fileStream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
                    {
                        sw.Write(content);
                        sw.Flush();
                        fileStream.SetLength(fileStream.Position); // truncate any stale tail
                    }
                }
            }
            catch (SecurityException)
            {
                // Already a precise, intentionally-thrown guard message - let it propagate.
                throw;
            }
            catch (Exception ex)
            {
                // The stream is inherently closed upon exception due to the using block unwinding.
                // We wipe the 0-byte empty file footprint off the disk to ensure no artifacts are left on unauthorized volumes.
                if (createdByUs)
                {
                    try { File.Delete(fullPath); } catch { /* ignored */ }
                }

                throw new SecurityException(
                    $"Security Guard Failure: Target file handle validation rejected. {ex.Message}",
                    ex);   // preserve the inner exception for diagnostics
            }
        }
    }
}