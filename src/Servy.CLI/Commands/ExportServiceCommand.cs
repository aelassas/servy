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
        /// Resolves NTFS junctions and symlinks to prevent path redirection bypasses.
        /// </summary>
        /// <param name="userPath">The target file path provided via the CLI.</param>
        /// <param name="content">The serialized configuration string.</param>
        /// <exception cref="SecurityException">Thrown if the path targets a protected system directory or a UNC path.</exception>
        /// <exception cref="ArgumentException">Thrown if the path is a reserved device name or an unsupported file type.</exception>
        private void SaveFile(string userPath, string content)
        {
            // Preliminary folder generation validation rule
            string fullPath = Path.GetFullPath(userPath);
            string? parentDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            bool createdByUs = !File.Exists(fullPath);
            bool committed = false;

            // Route execution flow securely through the centralized logic engine
            var validationResult = PathSecurityGuard.ValidatePath(
                userPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                out var fileStream);

            if (!validationResult.IsValid || fileStream == null)
            {
                string error = validationResult.ErrorMessage ?? "Security Guard Failure: Target file handle validation rejected.";

                // Keep structural compatibility with old explicit exceptions
                if (error.Contains("Access Denied") || error.Contains("Security Alert"))
                {
                    throw new SecurityException(error);
                }
                throw new ArgumentException(error);
            }

            try
            {
                using (fileStream)
                {
                    // The handle layout has been cleanly verified; write configuration data safely
                    using (var sw = new StreamWriter(fileStream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
                    {
                        sw.Write(content);
                        sw.Flush();
                        fileStream.SetLength(fileStream.Position); // Truncate out stale bytes cleanly
                        committed = true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new IOException($"Failed to write export file '{fullPath}': {ex.Message}", ex);
            }
            finally
            {
                if (!committed && createdByUs)
                {
                    try { File.Delete(fullPath); } catch { /* ignored */ }
                }
            }
        }
    }
}