using Servy.Core.Config;
using Servy.Core.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for managing and extracting embedded resources
    /// from the assembly, such as the Servy service executable and related files.
    /// </summary>
    public class ResourceHelper
    {
        private readonly IProcessKiller _processKiller;

        /// <summary>
        /// Gets or sets the base directory where embedded resources are extracted.
        /// Defaults to the application base directory in DEBUG and the ProgramData vault in RELEASE.
        /// </summary>
        public string BaseExtractionDirectory { get; set; } =
#if DEBUG
            AppDomain.CurrentDomain.BaseDirectory;
#else
            AppConfig.ProgramDataPath;
#endif

        /// <summary>
        /// Gets whether this instance has written at least one embedded resource to disk.
        /// </summary>
        /// <remarks>
        /// A file newly written to the vault carries no grant for the service accounts, so a caller that sees this flag
        /// set re-applies the executable permission hardening for them.
        /// </remarks>
        public bool HasCopiedResources { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ResourceHelper class using the specified process killer.
        /// </summary>
        /// <param name="processKiller">The process killer used to terminate processes. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="processKiller"/> is <see langword="null"/>.</exception>
        public ResourceHelper(IProcessKiller processKiller)
        {
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
        }

        /// <summary>
        /// Determines whether <see cref="CopyEmbeddedResourceAsync"/> would write the resource: the file is missing, or the
        /// running executable is newer than it by more than <see cref="AppConfig.ResourceStalenessThresholdMinutes"/>.
        /// </summary>
        /// <param name="resourceNamespace">Namespace of the embedded resource.</param>
        /// <param name="fileName">The resource's file name without extension.</param>
        /// <param name="extension">The file extension (e.g., "exe" or "dll").</param>
        /// <returns><see langword="true"/> when the file is going to be replaced.</returns>
        /// <remarks>
        /// Lets a caller that replaces several files stop the Servy services once for all of them, instead of once per file.
        /// </remarks>
        public bool IsExtractionNeeded(string resourceNamespace, string fileName, string extension)
            => TryPrepareExtraction(null, resourceNamespace, fileName, extension, out _, out _);

        /// <summary>
        /// Determines whether an embedded resource has to be (re)extracted: the file is missing, or it is older than the
        /// running executable by more than <see cref="AppConfig.ResourceStalenessThresholdMinutes"/> AND its content differs
        /// from the embedded resource. A file identical to the resource is never extracted again, whatever the timestamps say.
        /// </summary>
        /// <param name="assembly">The assembly that embeds the resource.</param>
        /// <param name="resourceNamespace">The resource namespace.</param>
        /// <param name="fileName">The file name, without extension.</param>
        /// <param name="extension">The file extension.</param>
        /// <returns><see langword="true"/> when the file has to be written.</returns>
        public bool IsExtractionNeeded(Assembly assembly, string resourceNamespace, string fileName, string extension)
            => TryPrepareExtraction(assembly, resourceNamespace, fileName, extension, out _, out _);

        /// <summary>
        /// Copies an embedded resource from the assembly to disk.
        /// </summary>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <param name="resourceNamespace">Namespace of the embedded resource.</param>
        /// <param name="fileName">The filename of the resource without extension.</param>
        /// <param name="extension">The file extension (e.g., "exe" or "dll").</param>
        /// <param name="cancellationToken">An optional token to monitor for cancellation requests during execution.</param>
        /// <returns>True if the copy succeeded (or was not needed); otherwise, false.</returns>
        public async Task<bool> CopyEmbeddedResourceAsync(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            CancellationToken cancellationToken = default)
        {
            bool copyDone = false; // Tracks if the physical file copy succeeded

            try
            {
                if (!TryPrepareExtraction(assembly, resourceNamespace, fileName, extension, out var targetPath, out var resourceName))
                    return true;

                // Capture pre-existing explicit ACLs BEFORE killing processes or staging files
                FileSecurity? existingAcl = GetExistingFileSecurity(targetPath);

                // ROBUSTNESS: Validate the embedded resource exists BEFORE side-effecting anything.
                // This prevents killing locking processes if the resource is missing.
                Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    Logger.Error($"Embedded resource not found: {resourceName}");
                    return false;
                }

                using (resourceStream)
                {
                    // Check cancellation boundary right before process execution checks
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TerminateBlockingProcesses(targetPath))
                        return false;

                    // Plumb the token parameter through to the atomic I/O engine
                    await Helper.WriteFileAtomicAsync(targetPath, resourceStream.CopyToAsync, cancellationToken);

                    // Restore pre-existing ACLs on the newly written file
                    RestoreFileSecurity(targetPath, existingAcl);

                    copyDone = true; // File write succeeded natively within the execution path
                    HasCopiedResources = true;
                }

                if (copyDone)
                {
                    Logger.Info($"Successfully copied embedded resource '{resourceName}' to '{targetPath}'.");
                }

                return copyDone;
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Embedded resource copy for '{fileName}' was cancelled by the caller.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to copy embedded resource '{fileName}'.", ex);
                return false;
            }
        }

        /// <summary>
        /// Retrieves the last write time of the host process executable.
        /// </summary>
        /// <returns>
        /// The <see cref="DateTime"/> (UTC) when the host process (.exe) was last modified,
        /// or <see cref="DateTime.MinValue"/> if the file cannot be accessed. The sentinel
        /// value causes <c>TryPrepareExtraction</c> to leave any existing extraction untouched
        /// when the timestamp probe fails.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method uses the main module of the current process as a proxy for the
        /// "deployment timestamp." This is an acceptable proxy in the current single-exe
        /// distribution model of Servy, as it represents the last time the application
        /// artifacts were updated on the host machine.
        /// </para>
        /// <para>
        /// Note: If resources are moved to a separate library assembly in the future,
        /// this method should be updated to query that specific assembly's file path
        /// to ensure accurate re-extraction logic.
        /// </para>
        /// </remarks>
        public DateTime GetHostProcessLastWriteTimeUtc()
        {
            // 1. Primary probe via Process.MainModule
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    var exePath = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        return File.GetLastWriteTimeUtc(exePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"{nameof(GetHostProcessLastWriteTimeUtc)}: MainModule.FileName access threw, falling back to AppDomain probe.", ex);
            }

            // 2. AppDomain fallback (runs for BOTH the exception and the silent-null path)
            try
            {
                var exeName = AppDomain.CurrentDomain.FriendlyName;
                string[] candidates =
                {
                    Path.Combine(AppContext.BaseDirectory, exeName),
                    Path.Combine(AppContext.BaseDirectory, exeName + ".exe"),
                    Path.Combine(AppContext.BaseDirectory, exeName + ".dll"),
                };
                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                        return File.GetLastWriteTimeUtc(path);
                }
            }
            catch (Exception innerEx)
            {
                Logger.Warn($"{nameof(GetHostProcessLastWriteTimeUtc)}: both MainModule and AppDomain probes failed.", innerEx);
            }

            return DateTime.MinValue;
        }

        #region Shared Internal Logic

        /// <summary>
        /// Attempts to retrieve the existing Access Control List (ACL) for a file before replacement,
        /// converting inherited rules into explicit Access Control Entries (ACEs).
        /// </summary>
        /// <param name="filePath">The target file path.</param>
        /// <returns>The <see cref="FileSecurity"/> of the target file if it exists; otherwise, <c>null</c>.</returns>
        private FileSecurity? GetExistingFileSecurity(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    var security = fileInfo.GetAccessControl();

                    // Protect against re-inheriting parent directory permissions upon atomic file replacement
                    security.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
                    return security;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to read existing ACL for file '{filePath}'. Pre-existing security settings may not be preserved upon replacement.", ex);
            }

            return null;
        }

        /// <summary>
        /// Reapplies a previously captured Access Control List (ACL) to a target file after replacement.
        /// </summary>
        /// <param name="filePath">The target file path.</param>
        /// <param name="fileSecurity">The <see cref="FileSecurity"/> settings to apply.</param>
        private void RestoreFileSecurity(string filePath, FileSecurity? fileSecurity)
        {
            if (fileSecurity == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                var fileInfo = new FileInfo(filePath);
                fileInfo.SetAccessControl(fileSecurity);
                Logger.Debug($"Successfully restored explicit pre-existing ACL settings on '{filePath}'.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to restore pre-existing ACL settings on '{filePath}'.", ex);
            }
        }

        /// <summary>
        /// Resolves output paths, creates necessary directories, and determines if a resource extraction is required based on timestamps.
        /// </summary>
        /// <param name="resourceNamespace">The namespace where the resource is located within the assembly.</param>
        /// <param name="fileName">The base name of the file to extract (without extension).</param>
        /// <param name="extension">The file extension (e.g., "exe", "dll").</param>
        /// <param name="targetPath">Output parameter containing the full destination path on disk.</param>
        /// <param name="resourceName">Output parameter containing the full manifest resource name used for extraction.</param>
        /// <returns>True if the resource needs to be copied; false if the existing file is up to date.</returns>
        private bool TryPrepareExtraction(
            Assembly? assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            out string targetPath,
            out string resourceName)
        {
            var targetFileName = fileName + "." + extension;

            // Use the explicit extraction root instead of assembly-relative logic
            targetPath = Path.Combine(BaseExtractionDirectory, targetFileName);

            var targetPathDir = Path.GetDirectoryName(targetPath);

            if (string.IsNullOrWhiteSpace(targetPathDir))
            {
                throw new IOException($"Could not resolve parent directory for extraction: {targetPath}");
            }

            Directory.CreateDirectory(targetPathDir);

            resourceName = resourceNamespace + "." + fileName + "." + extension;

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime hostExeWriteTime = GetHostProcessLastWriteTimeUtc();

                if (hostExeWriteTime == DateTime.MinValue)
                {
                    Logger.Warn("Last write time of the host process executable is equal to DateTime.MinValue, "
                        + $"resource re-extraction will be skipped this session until the existing file '{fileName}' is removed.");
                    return false;
                }

                Logger.Debug($"Existing file '{targetPath}' last write time: {existingFileTime.ToLocalTime():G}");
                Logger.Debug($"Host executable (deployment proxy) for '{resourceName}' last write time: {hostExeWriteTime.ToLocalTime():G}");

                // Only copy if the host executable (deployment proxy) is newer by more than AppConfig.ResourceStalenessThresholdMinutes
                bool shouldCopy = hostExeWriteTime > existingFileTime.AddMinutes(AppConfig.ResourceStalenessThresholdMinutes);

                if (!shouldCopy)
                {
                    if (hostExeWriteTime > existingFileTime)
                    {
                        Logger.Debug($"Embedded resource '{resourceName}' is newer, but within the {AppConfig.ResourceStalenessThresholdMinutes}-minute delta. Skipping copy.");
                    }
                    else if (existingFileTime > hostExeWriteTime.AddMinutes(AppConfig.ResourceStalenessThresholdMinutes))
                    {
                        Logger.Warn($"Extracted resource '{targetFileName}' ({existingFileTime.ToLocalTime():G}) is newer than host executable ({hostExeWriteTime.ToLocalTime():G}). Potential version downgrade detected; existing file will be retained.");
                    }
                }

                // The timestamps say "copy", but they only stand in for "a different build". Installed and extracted
                // times can disagree for reasons unrelated to the content (an installer that stamps files in the build
                // machine's local time puts them hours in the future on a machine in another time zone), and every
                // extraction stops all Servy services. So an identical file is never written again (#7358).
                if (shouldCopy && assembly != null && IsSameAsEmbeddedResource(assembly, resourceName, targetPath))
                {
                    Logger.Debug($"Existing file '{targetPath}' is identical to the embedded resource '{resourceName}'. Skipping copy.");
                    return false;
                }

                return shouldCopy;
            }

            return true;
        }

        /// <summary>
        /// Compares a file with an embedded resource, byte for byte.
        /// </summary>
        /// <param name="assembly">The assembly that embeds the resource.</param>
        /// <param name="resourceName">The full resource name.</param>
        /// <param name="filePath">The file to compare.</param>
        /// <returns><see langword="true"/> only when both exist and are identical; any failure counts as different.</returns>
        private static bool IsSameAsEmbeddedResource(Assembly assembly, string resourceName, string filePath)
        {
            Stream? resource = null;
            try
            {
                resource = assembly.GetManifestResourceStream(resourceName);
                if (resource == null)
                    return false;

                var start = resource.CanSeek ? resource.Position : 0;
                try
                {
                    using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        if (resource.CanSeek && file.Length != resource.Length - start)
                            return false;

                        var a = new byte[81920];
                        var b = new byte[81920];
                        while (true)
                        {
                            var read = ReadFull(resource, a);
                            if (ReadFull(file, b) != read)
                                return false;
                            if (read == 0)
                                return true;
                            for (var i = 0; i < read; i++)
                            {
                                if (a[i] != b[i])
                                    return false;
                            }
                        }
                    }
                }
                finally
                {
                    // A seekable resource stream is rewound and left open, so a caller holding the same instance can
                    // still read it; a forward-only one cannot be reused anyway
                    if (resource.CanSeek)
                    {
                        resource.Position = start;
                        resource = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not compare '{filePath}' with the embedded resource '{resourceName}': {ex.Message}");
                return false;
            }
            finally
            {
                resource?.Dispose();
            }
        }

        /// <summary>
        /// Reads until the buffer is full or the stream ends.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The number of bytes read.</returns>
        private static int ReadFull(Stream stream, byte[] buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;
                total += read;
            }
            return total;
        }

        /// <summary>
        /// Probes whether a file is currently locked by another process by attempting an exclusive write open.
        /// </summary>
        /// <param name="filePath">The full path of the file to test.</param>
        /// <returns>True if the file exists and is locked by another process; false otherwise.</returns>
        internal static bool IsFileLocked(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false; // Successfully acquired exclusive handle; file is not locked
                }
            }
            catch (IOException)
            {
                return true; // File handle collision or lock detected
            }
            catch (UnauthorizedAccessException)
            {
                return true; // File locked or permission restricted
            }
            catch (Exception ex)
            {
                Logger.Debug($"Unexpected exception while testing file lock for '{filePath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safely terminates any processes holding locks on the target file by identifying them by path.
        /// This prevents collateral damage to other service instances using the same utility names.
        /// </summary>
        /// <param name="targetPath">The full path to the file to check for active file handles.</param>
        /// <returns>True if the file was successfully cleared of blocking processes; false if termination failed.</returns>
        internal bool TerminateBlockingProcesses(string targetPath)
        {
            // FAST PROBE: Check if the target file is actually locked before shelling out to handle64.exe.
            // This prevents launching handle64.exe on every routine service start/update pass when no locks exist,
            // eliminating unnecessary process executions and avoiding false-positive EDR/AV heuristic alerts.
            if (!IsFileLocked(targetPath))
            {
                return true;
            }

            Logger.Info($"File lock detected on '{targetPath}'. Identifying and terminating blocking processes...");

            // Identify lock holders by path (not by executable name) so we surgically terminate
            // only the trees locking THIS specific file, leaving unrelated services that run
            // their own copy of the same executable (e.g., Servy.Restarter.exe) untouched.
            if (!_processKiller.KillProcessesUsingFile(targetPath))
            {
                Logger.Error($"Could not clear file locks on '{targetPath}'. Extraction aborted to prevent file corruption.");
                return false;
            }

            return true;
        }

        #endregion
    }
}
