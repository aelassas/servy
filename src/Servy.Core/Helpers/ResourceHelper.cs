using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using System.Diagnostics;
using System.Reflection;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for managing and extracting embedded resources
    /// from the assembly, such as the Servy service executable and related files.
    /// </summary>
    public class ResourceHelper
    {
        private readonly IServiceHelper _serviceHelper;
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
        /// Initializes a new instance of the ResourceHelper class using the specified service helper and process killer.
        /// </summary>
        /// <param name="serviceHelper">The service helper used to access and manage service states. Cannot be null.</param>
        /// <param name="processKiller">The process killer used to terminate processes. Cannot be null.</param>
        public ResourceHelper(
            IServiceHelper serviceHelper,
            IProcessKiller processKiller)
        {
            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _processKiller = processKiller ?? throw new ArgumentNullException(nameof(processKiller));
        }

        /// <summary>
        /// Copies an embedded resource from the assembly to disk, stopping and restarting services if necessary.
        /// </summary>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <param name="resourceNamespace">Namespace of the embedded resource.</param>
        /// <param name="fileName">The filename of the resource without extension.</param>
        /// <param name="extension">The file extension (e.g., "exe" or "dll").</param>
        /// <param name="stopServices">Whether to stop services before copying the resource.</param>
        /// <param name="isCli">Whether we are in CLI or not.</param>
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public async Task<bool> CopyEmbeddedResource(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            bool stopServices = true,
            bool isCli = false)
        {
            bool copyDone = false; // Tracks if the physical file copy succeeded
            string? currentResourceName = null;
            string? currentTargetPath = null;

            try
            {
                if (!ShouldCopyResource(assembly, resourceNamespace, fileName, extension, out var targetPath, out var resourceName))
                    return true;

                currentResourceName = resourceName;
                currentTargetPath = targetPath;

                // Get running services before the inner try block
                var runningServices = new List<string>();
                if (stopServices)
                {
                    runningServices = isCli
                        ? _serviceHelper.GetRunningServyCLIServices()
                        : _serviceHelper.GetRunningServyUIServices();
                }

                try
                {
                    if (stopServices && runningServices.Count > 0)
                    {
                        Logger.Info($"Stopping services before copying resource '{resourceName}': {string.Join(", ", runningServices)}");
                        await _serviceHelper.StopServices(runningServices);
                    }

                    if (!TerminateBlockingProcesses(targetPath))
                        return false;

                    Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream == null)
                    {
                        Logger.Error($"Embedded resource not found: {resourceName}");
                        return false;
                    }

                    using (resourceStream)
                    {
                        await Helper.WriteFileAtomicAsync(targetPath, resourceStream.CopyToAsync);
                    }

                    copyDone = true; // File write succeeded
                }
                finally
                {
                    if (stopServices && runningServices.Count > 0)
                    {
                        try
                        {
                            Logger.Info($"Starting stopped services after copying resource '{resourceName}': {string.Join(", ", runningServices)}");
                            await _serviceHelper.StartServices(runningServices);
                        }
                        catch (Exception startEx)
                        {
                            // Differentiate restart failure from copy failure
                            Logger.Error(
                                $"Embedded resource '{resourceName}' was successfully copied to '{targetPath}', but {runningServices.Count} previously-running services failed to restart.",
                                startEx);
                        }
                    }
                }

                if (copyDone)
                {
                    Logger.Info($"Successfully copied embedded resource '{resourceName}' to '{targetPath}'.");
                }

                return copyDone;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to copy embedded resource '{fileName}'.", ex);
                return false;
            }
        }

        /// <summary>
        /// Copies an embedded resource from the assembly to disk synchronously.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>DANGER:</b> Unlike its asynchronous counterpart, this method forcefully terminates 
        /// any processes holding a lock on the target file WITHOUT performing a graceful service 
        /// shutdown or restart. It completely circumvents the standard service lifecycle.
        /// </para>
        /// <para>
        /// This should <b>only</b> be called by external bootstrapping utilities or during 
        /// installation phases when it is guaranteed that no Servy services are actively running.
        /// </para>
        /// </remarks>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <param name="resourceNamespace">Namespace of the embedded resource.</param>
        /// <param name="fileName">The filename of the resource without extension.</param>
        /// <param name="extension">The file extension (e.g., "exe" or "dll").</param>
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public bool CopyEmbeddedResourceForceSync(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension)
        {
            try
            {
                if (!ShouldCopyResource(assembly, resourceNamespace, fileName, extension, out var targetPath, out var resourceName))
                    return true;

                // Log a warning so operators auditing the logs know a brute-force termination might occur
                Logger.Warn($"Executing synchronous force-copy for '{resourceName}'. Any processes locking this file will be killed without graceful shutdown.");

                if (!TerminateBlockingProcesses(targetPath))
                    return false;

                Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    Logger.Error($"Embedded resource not found: {resourceName}");
                    return false;
                }

                using (resourceStream)
                {
                    Helper.WriteFileAtomic(targetPath, resourceStream.CopyTo);
                }

                Logger.Info($"Successfully forcefully copied embedded resource '{resourceName}' to '{targetPath}'.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to forcefully copy embedded resource '{fileName}'.", ex);
                return false;
            }
        }

        /// <summary>
        /// Retrieves the last write time of the host process executable.
        /// </summary>
        /// <returns>
        /// The <see cref="DateTime"/> (UTC) when the host process (.exe) was last modified, 
        /// or <see cref="DateTime.MinValue"/> if the file cannot be accessed. The sentinel 
        /// value causes <c>ShouldCopyResource</c> to leave any existing extraction untouched 
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
        public DateTime GetHostProcessLastWriteTimeUTC()
        {
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
                Logger.Debug("MainModule.FileName not available, trying AppDomain fallback.", ex);
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
                    // Both probes failed; return DateTime.MinValue so the caller treats the embedded
                    // resource as 'not newer' and leaves the existing extraction in place.
                    Logger.Debug("AppDomain fallback also failed; returning DateTime.MinValue (no re-extraction).", innerEx);
                }
            }

            // Use a sentinel that is older than any plausible existing extraction
            return DateTime.MinValue;
        }

        #region Shared Internal Logic

        /// <summary>
        /// Resolves output paths, creates necessary directories, and determines if a resource extraction is required based on timestamps.
        /// </summary>
        /// <param name="assembly">The assembly containing the embedded resource.</param>
        /// <param name="resourceNamespace">The namespace where the resource is located within the assembly.</param>
        /// <param name="fileName">The base name of the file to extract (without extension).</param>
        /// <param name="extension">The file extension (e.g., "exe", "dll").</param>
        /// <param name="targetPath">Output parameter containing the full destination path on disk.</param>
        /// <param name="resourceName">Output parameter containing the full manifest resource name used for extraction.</param>
        /// <returns>True if the resource needs to be copied; false if the existing file is up to date.</returns>
        private bool ShouldCopyResource(
            Assembly assembly,
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

            if (string.IsNullOrEmpty(targetPathDir))
            {
                throw new IOException($"Could not resolve parent directory for extraction: {targetPath}");
            }

            if (!Directory.Exists(targetPathDir))
            {
                Directory.CreateDirectory(targetPathDir!);
            }

            resourceName = resourceNamespace + "." + fileName + "." + extension;

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime embeddedResourceTime = GetHostProcessLastWriteTimeUTC();

                Logger.Debug($"Existing file '{targetPath}' last write time: {existingFileTime.ToLocalTime():G}");
                Logger.Debug($"Embedded resource '{resourceName}' last write time: {embeddedResourceTime.ToLocalTime():G}");

                // Only copy if the embedded resource is newer by more than DeltaMinutes
                bool shouldCopy = embeddedResourceTime > existingFileTime.AddMinutes(AppConfig.ResourceStalenessThresholdMinutes);

                if (!shouldCopy && embeddedResourceTime > existingFileTime)
                {
                    Logger.Debug($"Embedded resource '{resourceName}' is newer, but within the {AppConfig.ResourceStalenessThresholdMinutes}-minute delta. Skipping copy.");
                }

                return shouldCopy;
            }

            return true;
        }

        /// <summary>
        /// Safely terminates any processes holding locks on the target file by identifying them by path.
        /// This prevents collateral damage to other service instances using the same utility names.
        /// </summary>
        /// <param name="targetPath">The full path to the file to check for active file handles.</param>
        /// <returns>True if the file was successfully cleared of blocking processes; false if termination failed.</returns>
        private bool TerminateBlockingProcesses(string targetPath)
        {
            // Fix for #Warning: Use path-based identification for all resource types.
            // Name-based matching (targetFileName) hits unrelated services on the same host 
            // that happen to be running their own copy of the same executable (e.g., Servy.Restarter.exe).

            // KillProcessesUsingFile surgically finds the PIDs locking THIS specific file
            // and terminates their entire trees, leaving "Service B's" restarter untouched.
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