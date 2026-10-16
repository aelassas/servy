using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Security;
using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for managing and extracting embedded resources
    /// from the assembly, such as the Servy service executable and related files.
    /// </summary>
    public class ResourceHelper
    {
        /// <summary>
        /// The rights that let a principal change a file's content, its timestamps or its ACL, or replace it.
        /// </summary>
        private const FileSystemRights WriteRights = FileSystemRights.WriteData | FileSystemRights.AppendData
            | FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes | FileSystemRights.Delete
            | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

        private static readonly SecurityIdentifier AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

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
        /// Determines from the timestamps alone whether the resource is due for extraction: the file is missing, or the
        /// running executable is newer than it by more than <see cref="AppConfig.ResourceStalenessThresholdMinutes"/>.
        /// Unlike the overload that takes the assembly, it does not compare the content, so it also returns
        /// <see langword="true"/> for a file that <see cref="CopyEmbeddedResourceAsync"/> leaves alone because it is identical
        /// to the embedded resource (#7358).
        /// </summary>
        /// <param name="resourceNamespace">Namespace of the embedded resource.</param>
        /// <param name="fileName">The resource's file name without extension.</param>
        /// <param name="extension">The file extension (e.g., "exe" or "dll").</param>
        /// <returns><see langword="true"/> when the timestamps call for a new extraction.</returns>
        /// <remarks>
        /// A caller that stops the Servy services once before replacing several files uses the overload that takes the
        /// assembly, so that an identical file does not stop them.
        /// </remarks>
        internal bool IsExtractionNeeded(string resourceNamespace, string fileName, string extension)
            => TryPrepareExtraction(null, resourceNamespace, fileName, extension, out _, out _);

        /// <summary>
        /// Determines whether an embedded resource has to be (re)extracted: the file is missing, or it is older than the
        /// running executable by more than <see cref="AppConfig.ResourceStalenessThresholdMinutes"/> AND its content differs
        /// from the embedded resource. A file identical to the resource is never extracted again, whatever the timestamps say.
        /// A file whose timestamps say "keep" is still compared with the embedded resource, and extracted again when it
        /// differs, if a principal other than Administrators and SYSTEM owns it or may write to it (#7530).
        /// </summary>
        /// <param name="assembly">The assembly that embeds the resource.</param>
        /// <param name="resourceNamespace">The resource namespace.</param>
        /// <param name="fileName">The file name, without extension.</param>
        /// <param name="extension">The file extension.</param>
        /// <returns><see langword="true"/> when the file has to be written.</returns>
        /// <remarks>
        /// Not a pure query: when the existing file is found identical to the embedded resource, its last write time is
        /// set to the host executable's, so that later calls skip the byte comparison (#7367). Like the timestamp-only
        /// overload, it also creates the extraction directory when it is missing.
        /// </remarks>
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
            try
            {
                if (!TryPrepareExtraction(assembly, resourceNamespace, fileName, extension, out var targetPath, out var resourceName))
                    return true;

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

                    if (!TerminateBlockingProcesses(assembly, resourceNamespace, targetPath))
                        return false;

                    // Plumb the token parameter through to the atomic I/O engine
                    await Helper.WriteFileAtomicAsync(targetPath, resourceStream.CopyToAsync, cancellationToken);

                    // Rebuild the DACL from policy rather than restoring the old one, which may carry entries that were
                    // never Servy's (#7530); the service accounts get their access back from the vault hardening
                    ApplyVaultFileSecurity(targetPath);

                    HasCopiedResources = true;
                }

                Logger.Info($"Successfully copied embedded resource '{resourceName}' to '{targetPath}'.");
                return true;
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
        /// Gives a newly written file the vault's own security descriptor instead of the one the replaced file had:
        /// inheritance is broken, the DACL holds Full Control for <c>BUILTIN\Administrators</c> and <c>SYSTEM</c> only
        /// (and for the current user when the process is not elevated, as <see cref="SecurityHelper.ApplySecurityRules"/>
        /// does for the vault itself), and an elevated process makes <c>BUILTIN\Administrators</c> the owner (#7530).
        /// </summary>
        /// <param name="filePath">The file that was just written.</param>
        /// <remarks>
        /// The service accounts lose the Read &amp; Execute grant the replaced file carried. In Release builds the caller
        /// re-applies it right after the extraction pass, for every account found in the database, through
        /// <see cref="ServyExePermissionsHardener.SecureVaultAsync"/>.
        /// </remarks>
        private static void ApplyVaultFileSecurity(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var security = fileInfo.GetAccessControl();

                SecurityIdentifier? currentUserSid;
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    currentUserSid = identity.User;
                }

                SecurityHelper.ApplySecurityRules(security, currentUserSid, breakInheritance: true);

                if (SecurityHelper.IsAdministrator())
                    security.SetOwner(AdministratorsSid);

                fileInfo.SetAccessControl(security);
                Logger.Debug($"Applied the vault's security descriptor to '{filePath}'.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to apply the vault's security descriptor to '{filePath}'.", ex);
            }
        }

        /// <summary>
        /// Determines whether a principal other than <c>BUILTIN\Administrators</c> and <c>SYSTEM</c> owns a file or is
        /// granted a right that changes its content, its timestamps or its ACL, so that its timestamps cannot be trusted
        /// to stand for its content (#7530).
        /// </summary>
        /// <param name="filePath">The existing file.</param>
        /// <returns><see langword="true"/> when another principal owns the file or may write to it, or when its owner and
        /// ACL cannot be read.</returns>
        internal static bool IsWritableByOthers(string filePath)
        {
            try
            {
                var security = new FileInfo(filePath).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);

                if (!IsAdministrativePrincipal(security.GetOwner(typeof(SecurityIdentifier))))
                    return true;

                foreach (FileSystemAccessRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType == AccessControlType.Allow
                        && (rule.FileSystemRights & WriteRights) != 0
                        && !IsAdministrativePrincipal(rule.IdentityReference))
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not read the owner and ACL of '{filePath}', so it is treated as writable by others: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Determines whether a principal is <c>BUILTIN\Administrators</c> or <c>SYSTEM</c>.
        /// </summary>
        /// <param name="identity">The principal, as a security identifier.</param>
        /// <returns><see langword="true"/> for either of the two.</returns>
        private static bool IsAdministrativePrincipal(IdentityReference? identity)
            => identity != null && (AdministratorsSid.Equals(identity) || LocalSystemSid.Equals(identity));

        /// <summary>
        /// Resolves output paths, creates necessary directories, and determines if a resource extraction is required: based on
        /// timestamps, and, when <paramref name="assembly"/> is given, never for a file identical to the embedded resource.
        /// </summary>
        /// <param name="assembly">The assembly that embeds the resource, used to compare the existing file with it; or
        /// <see langword="null"/> to decide from the timestamps alone.</param>
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
                    // Identical: record that, so the next start does not compare the whole file again
                    try { File.SetLastWriteTimeUtc(targetPath, hostExeWriteTime); }
                    catch (Exception ex) { Logger.Debug($"Could not update the write time of '{targetPath}': {ex.Message}"); }

                    Logger.Debug($"Existing file '{targetPath}' is identical to the embedded resource '{resourceName}'. Skipping copy.");
                    return false;
                }

                // The timestamps only stand for the content while nobody else can write the file or set its times. A
                // healthy vault file costs one owner and ACL read here and no comparison; only a file another principal
                // owns or may write to is compared with the embedded resource, and replaced when it differs (#7530).
                if (!shouldCopy && assembly != null && IsWritableByOthers(targetPath) && !IsSameAsEmbeddedResource(assembly, resourceName, targetPath))
                {
                    Logger.Warn($"Existing file '{targetPath}' is owned or writable by a principal other than Administrators and SYSTEM and differs from the embedded resource '{resourceName}'. It will be replaced.");
                    return true;
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
            try
            {
                using (var resource = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resource == null)
                        return false;

                    using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        if (resource.CanSeek && file.Length != resource.Length)
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
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not compare '{filePath}' with the embedded resource '{resourceName}': {ex.Message}");
                return false;
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
        /// <param name="assembly">The assembly that embeds the Sysinternals Handle executable.</param>
        /// <param name="resourceNamespace">The namespace of the embedded resources.</param>
        /// <param name="targetPath">The full path to the file to check for active file handles.</param>
        /// <returns>True if the file was successfully cleared of blocking processes; false if termination failed.</returns>
        /// <remarks>
        /// The lock holders are found by the Sysinternals Handle executable, which runs elevated. It is run only once it
        /// has been verified against the embedded resource, or written from it, in the same call (#7530).
        /// </remarks>
        internal bool TerminateBlockingProcesses(Assembly assembly, string resourceNamespace, string targetPath)
        {
            // FAST PROBE: Check if the target file is actually locked before shelling out to handle64.exe.
            // This prevents launching handle64.exe on every routine service start/update pass when no locks exist,
            // eliminating unnecessary process executions and avoiding false-positive EDR/AV heuristic alerts.
            if (!IsFileLocked(targetPath))
            {
                return true;
            }

            Logger.Info($"File lock detected on '{targetPath}'. Identifying and terminating blocking processes...");

            if (!EnsureHandleExeVerified(assembly, resourceNamespace, targetPath))
            {
                Logger.Error($"The Sysinternals Handle executable could not be verified against the embedded resource, so the locks on '{targetPath}' were not cleared. Extraction aborted.");
                return false;
            }

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

        /// <summary>
        /// Makes sure the Sysinternals Handle executable in <see cref="BaseExtractionDirectory"/> is the embedded one before
        /// it is run: an existing file is compared with the embedded resource, and one that differs or is missing is
        /// written from it first (#7530).
        /// </summary>
        /// <param name="assembly">The assembly that embeds the Handle executable.</param>
        /// <param name="resourceNamespace">The namespace of the embedded resources.</param>
        /// <param name="targetPath">The file whose locks are to be cleared. When it is the Handle executable itself, it
        /// cannot be written first, so it is not verified.</param>
        /// <returns><see langword="true"/> when the Handle executable matches the embedded resource.</returns>
        private bool EnsureHandleExeVerified(Assembly assembly, string resourceNamespace, string targetPath)
        {
            var handleExeFileName = Path.GetFileName(AppConfig.GetHandleExePath());
            var handleExePath = Path.Combine(BaseExtractionDirectory, handleExeFileName);
            var resourceName = resourceNamespace + "." + handleExeFileName;

            if (File.Exists(handleExePath) && IsSameAsEmbeddedResource(assembly, resourceName, handleExePath))
                return true;

            if (string.Equals(Path.GetFullPath(handleExePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Logger.Error($"Embedded resource not found: {resourceName}");
                        return false;
                    }

                    Helper.WriteFileAtomic(handleExePath, resourceStream.CopyTo);
                }

                ApplyVaultFileSecurity(handleExePath);
                HasCopiedResources = true;

                Logger.Info($"Wrote the embedded resource '{resourceName}' to '{handleExePath}' before running it.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to write the embedded resource '{resourceName}' to '{handleExePath}'.", ex);
                return false;
            }
        }

        #endregion
    }
}
