using Servy.CLI.Enums;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Validation;
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
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceRepository"/> is <c>null</c>.
        /// </exception>
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
            var action = string.Format(Strings.Msg_ExportServiceAction, opts.ServiceName);
            var suggestion = Strings.Msg_ExportServiceSuggestion;

            return await ExecuteWithHandlingAsync("export", action, suggestion, async () =>
            {

                if (string.IsNullOrWhiteSpace(opts.ServiceName))
                    return CommandResult.Fail(Core.Resources.Strings.Msg_ServiceNameRequired);

                // Validate configuration file type
                if (!Helpers.Helper.TryParseFileType(opts.ConfigFileType, out var configFileType, out var parseError))
                    return CommandResult.Fail(parseError);

                if (string.IsNullOrWhiteSpace(opts.Path))
                    return CommandResult.Fail(Strings.Msg_PathRequired);

                // Pre-flight elevation check
                SecurityHelper.EnsureAdministrator();

                var exists = await _serviceRepository.GetByNameAsync(opts.ServiceName, cancellationToken: cancellationToken);

                if (exists == null)
                    return CommandResult.Fail(Core.Resources.Strings.Msg_ServiceNotFound);

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
        /// Resolves NTFS junctions and symlinks to prevent path redirection bypasses.
        /// </summary>
        /// <param name="userPath">The target file path provided via the CLI.</param>
        /// <param name="content">The serialized configuration string.</param>
        /// <exception cref="SecurityException">Thrown when validation fails with a "Security Alert" or "Access Denied" message:
        /// UNC path, reserved device name, unsupported file type, or a protected system directory.</exception>
        /// <exception cref="ArgumentException">Thrown for any other validation failure (e.g. handle validation rejected).</exception>
        /// <exception cref="IOException">Thrown if the directory chain cannot be created or the file cannot be written.</exception>
        private void SaveFile(string userPath, string content)
        {
            string fullPath = Path.GetFullPath(userPath);
            string? parentDir = Path.GetDirectoryName(fullPath);

            // Track directories created in this invocation; cleanup is best-effort (only directories we created, only if empty)
            var directoriesCreatedByUs = new List<string>();

            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                string? currentPath = parentDir;
                var missingChain = new Stack<string>();

                while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
                {
                    missingChain.Push(currentPath);
                    currentPath = Path.GetDirectoryName(currentPath);
                }

                while (missingChain.Count > 0)
                {
                    string targetDir = missingChain.Pop();
                    try
                    {
                        Directory.CreateDirectory(targetDir);
                        directoriesCreatedByUs.Add(targetDir);
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Failed to create directory structure chain for path '{targetDir}': {ex.Message}", ex);
                    }
                }
            }

            bool committed = false;
            bool existedBefore = File.Exists(fullPath);
            bool createdByUs = !existedBefore;

            var validationResult = PathSecurityGuard.ValidatePath(
                userPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                out var fileStream);

            if (!validationResult.IsValid || fileStream == null)
            {
                // Clean up any empty directories created before the path security failure
                RollbackCreatedDirectories(directoriesCreatedByUs);

                string error = validationResult.ErrorMessage ?? "Security Guard Failure: Target file handle validation rejected.";

                if (validationResult.FailureKind == PathSecurityFailureKind.Security)
                {
                    throw new SecurityException(error);
                }

                throw new ArgumentException(error);
            }

            try
            {
                using (fileStream)
                {
                    using (var sw = new StreamWriter(fileStream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
                    {
                        sw.Write(content);
                        sw.Flush();
                        fileStream.SetLength(fileStream.Position); // Truncate existing content if file was larger
                        fileStream.Flush(true); // Force intermediate buffer flush to disk before disposing handle
                    }
                }

                // Mark committed strictly AFTER stream handle dispose and flush completes successfully
                committed = true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new IOException($"Failed to write export file '{fullPath}': {ex.Message}", ex);
            }
            finally
            {
                if (!committed)
                {
                    if (createdByUs)
                    {
                        try { File.Delete(fullPath); } catch { /* ignored */ }
                    }

                    // Best-effort cleanup of empty directories created during write failure
                    RollbackCreatedDirectories(directoriesCreatedByUs);
                }
            }
        }

        /// <summary>
        /// Performs a best-effort reverse-order cleanup of directories created during file saving,
        /// removing only directories that are currently empty.
        /// </summary>
        /// <param name="directories">The list of directory paths created during execution.</param>
        private static void RollbackCreatedDirectories(List<string> directories)
        {
            for (int i = directories.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (Directory.Exists(directories[i]) && Directory.GetFileSystemEntries(directories[i]).Length == 0)
                    {
                        Directory.Delete(directories[i]);
                    }
                }
                catch { /* Best-effort cleanup: suppress exceptions to preserve original error context */ }
            }
        }
    }
}
