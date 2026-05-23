using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
        /// <param name="subfolder">Optional subfolder within the target directory.</param>
        /// <returns>
        /// True if the copy succeeded (or was not needed) AND all stopped services were successfully restarted; 
        /// otherwise, false.
        /// </returns>
        public async Task<bool> CopyEmbeddedResource(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            bool stopServices = true,
            string subfolder = null)
        {
            bool copyDone = false; // Tracks if the physical file copy succeeded
            bool restartFailed = false; // Tracks if the post-copy service restoration failed
            string targetPath = null;
            string resourceName = null;

            try
            {
                if (!ShouldCopyResource(assembly, resourceNamespace, fileName, extension, subfolder, out targetPath, out var targetFileName, out resourceName))
                    return true;

                // ROBUSTNESS: Validate the embedded resource exists BEFORE side-effecting anything.
                // This prevents stopping services or killing locking processes if the resource is missing.
                Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    Logger.Error($"Embedded resource not found: {resourceName}");
                    return false;
                }

                using (resourceStream)
                {
                    // Get running services
                    var runningServices = stopServices ? _serviceHelper.GetRunningServyServices() : new List<string>();

                    try
                    {
                        if (stopServices && runningServices.Count > 0)
                        {
                            Logger.Info($"Stopping services before copying resource '{resourceName}': {string.Join(", ", runningServices)}");
                            await _serviceHelper.StopServices(runningServices);
                        }

                        if (!TerminateBlockingProcesses(extension, targetFileName, targetPath))
                            return false;

                        await Helper.WriteFileAtomicAsync(targetPath, resourceStream.CopyToAsync);
                        copyDone = true; // File write succeeded natively within the execution path
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
                                // Log restart failure separately to avoid misattribution
                                restartFailed = true;

                                // ROBUSTNESS: Dynamically evaluate the copyDone state inside the finally block.
                                // This guarantees we don't issue false success metrics to the administrator logs if the copy was aborted earlier.
                                var copyDescription = copyDone
                                    ? $"Embedded resource '{resourceName}' was successfully copied to '{targetPath}', but "
                                    : $"Embedded resource '{resourceName}' was NOT copied to '{targetPath}'; additionally, ";

                                Logger.Error(
                                    copyDescription + $"{runningServices.Count} previously-running services failed to restart.",
                                    startEx);
                            }
                        }
                    }
                }

                if (copyDone && !restartFailed)
                {
                    Logger.Info($"Successfully copied embedded resource '{resourceName}' to '{targetPath}'.");
                }

                // ROBUSTNESS: Only return true if both the copy and the service restoration were successful.
                return copyDone && !restartFailed;
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
        /// <param name="subfolder">Optional subfolder within the target directory.</param>
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public bool CopyEmbeddedResourceForceSync(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            string subfolder = null)
        {
            try
            {
                if (!ShouldCopyResource(assembly, resourceNamespace, fileName, extension, subfolder, out var targetPath, out var targetFileName, out var resourceName))
                    return true;

                // ROBUSTNESS: Validate the embedded resource exists BEFORE side-effecting anything.
                Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    Logger.Error($"Embedded resource not found: {resourceName}");
                    return false;
                }

                using (resourceStream)
                {
                    if (!TerminateBlockingProcesses(extension, targetFileName, targetPath))
                        return false;

                    Helper.WriteFileAtomic(targetPath, resourceStream.CopyTo);
                }

                Logger.Info($"Successfully copied embedded resource '{resourceName}' to '{targetPath}'.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to copy embedded resource '{fileName}'.", ex);
                return false;
            }
        }

        /// <summary>
        /// Copies embedded resources (such as DLLs and EXEs) from the specified assembly to target paths.
        /// </summary>
        /// <param name="assembly">
        /// The <see cref="Assembly"/> that contains the embedded resources.
        /// </param>
        /// <param name="resourceNamespace">
        /// The root namespace under which the resources are embedded.
        /// </param>
        /// <param name="resourceItems">
        /// A list of ResourceItem objects describing the resources to copy, including file names,
        /// extensions, subfolders, and metadata used during copying.
        /// </param>
        /// <param name="stopServices">
        /// If <c>true</c>, running Servy services will be stopped before copying and restarted afterward. 
        /// Default is <c>true</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if all resources were copied successfully or did not need copying; 
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Determines the target file path for each resource.</description></item>
        ///   <item><description>Checks if the target file already exists and whether it is up to date.</description></item>
        ///   <item><description>Stops running Servy services if <paramref name="stopServices"/> is <c>true</c>.</description></item>
        ///   <item><description>Kills processes using the target file if it is a DLL or EXE.</description></item>
        ///   <item><description>Copies the embedded resource stream to the target path.</description></item>
        ///   <item><description>Restarts previously stopped services.</description></item>
        /// </list>
        /// <para>
        /// If any copy operation fails, the method logs the error and continues with remaining resources,
        /// returning <c>false</c> at the end.
        /// </para>
        /// </remarks>
        public async Task<bool> CopyResources(
            Assembly assembly,
            string resourceNamespace,
            List<ResourceItem> resourceItems,
            bool stopServices = true)
        {
            var res = true;
            try
            {
                foreach (var resourceItem in resourceItems)
                {
                    resourceItem.ShouldCopy = ShouldCopyResource(
                        assembly,
                        resourceNamespace,
                        resourceItem.FileNameWithoutExtension,
                        resourceItem.Extension,
                        resourceItem.Subfolder,
                        out var targetPath,
                        out var targetFileName,
                        out var resourceName);

                    resourceItem.TargetPath = targetPath;
                    resourceItem.TargetFileName = targetFileName;
                    resourceItem.ResourceName = resourceName;

                    // ROBUSTNESS: Pre-flight existence check.
                    // If a resource requires updating but doesn't exist in the assembly payload,
                    // disqualify it immediately. This prevents the system from triggering a global 
                    // service stop for a file replacement it cannot fulfill.
                    if (resourceItem.ShouldCopy)
                    {
                        using (var testStream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (testStream == null)
                            {
                                Logger.Error($"Embedded resource not found: {resourceName}");
                                resourceItem.ShouldCopy = false;
                                res = false; // Mark overall operation as having a failure
                            }
                        }
                    }
                }

                if (resourceItems.All(r => !r.ShouldCopy))
                    return true;

                // Get running services
                var runningServices = stopServices ? _serviceHelper.GetRunningServyServices() : new List<string>();
                if (runningServices.Count > 0)
                    Logger.Info($"Stopping running services before copying resources: {string.Join(", ", runningServices)}");

                try
                {
                    if (stopServices && runningServices.Count > 0)
                        await _serviceHelper.StopServices(runningServices);

                    foreach (var resourceItem in resourceItems.Where(r => r.ShouldCopy))
                    {
                        try
                        {
                            // ROBUSTNESS: Validate the embedded resource stream is physically accessible 
                            // BEFORE executing any destructive process termination logic.
                            Stream resourceStream = assembly.GetManifestResourceStream(resourceItem.ResourceName);
                            if (resourceStream == null)
                            {
                                Logger.Error($"Embedded resource not found during copy phase: {resourceItem.ResourceName}");
                                res = false;
                                continue;
                            }

                            using (resourceStream)
                            {
                                // Safe to terminate locking processes because the payload bytes are guaranteed ready
                                if (!TerminateBlockingProcesses(resourceItem.Extension, resourceItem.TargetFileName, resourceItem.TargetPath, skipDll: true))
                                {
                                    res = false;
                                    continue;
                                }

                                await Helper.WriteFileAtomicAsync(resourceItem.TargetPath, resourceStream.CopyToAsync);
                                Logger.Info($"Successfully copied embedded resource '{resourceItem.ResourceName}' to '{resourceItem.TargetPath}'.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to copy embedded resource '{resourceItem.ResourceName}' to '{resourceItem.TargetPath}'.", ex);
                            res = false;
                        }
                    }
                }
                finally
                {
                    if (stopServices && runningServices.Count > 0)
                    {
                        try
                        {
                            Logger.Info($"Starting stopped services after copying resources: {string.Join(", ", runningServices)}");
                            await _serviceHelper.StartServices(runningServices);
                        }
                        catch (Exception startEx)
                        {
                            // Log restart failure as a distinct error
                            Logger.Error(
                                $"Target resources were processed, but {runningServices.Count} previously-running services failed to restart.",
                                startEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to copy embedded resources.", ex);
                res = false;
            }

            return res;
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
                Logger.Info("GetHostProcessLastWriteTimeUTC: MainModule.FileName access threw, falling back to AppDomain probe.", ex);
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
                Logger.Warn("GetHostProcessLastWriteTimeUTC: both MainModule and AppDomain probes failed.", innerEx);
            }

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
        /// <param name="subfolder">Optional subfolder within the target directory.</param>
        /// <param name="targetPath">Output parameter containing the full destination path on disk.</param>
        /// <param name="targetFileName">Output parameter containing the combined filename and extension.</param>
        /// <param name="resourceName">Output parameter containing the full manifest resource name used for extraction.</param>
        /// <returns>True if the resource needs to be copied; false if the existing file is up to date.</returns>
        private bool ShouldCopyResource(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            string subfolder,
            out string targetPath,
            out string targetFileName,
            out string resourceName)
        {
            targetFileName = fileName + "." + extension;

            // Use the explicit extraction root instead of assembly-relative logic
            targetPath = string.IsNullOrEmpty(subfolder)
                            ? Path.Combine(BaseExtractionDirectory, targetFileName)
                            : Path.Combine(BaseExtractionDirectory, subfolder, targetFileName);

            var targetPathDir = Path.GetDirectoryName(targetPath);

            if (string.IsNullOrEmpty(targetPathDir))
            {
                throw new IOException($"Could not resolve parent directory for extraction: {targetPath}");
            }

            if (!Directory.Exists(targetPathDir))
            {
                Directory.CreateDirectory(targetPathDir);
            }

            resourceName = string.IsNullOrEmpty(subfolder)
                ? $"{resourceNamespace}.{fileName}.{extension}"
                : $"{resourceNamespace}.{subfolder.Replace(Path.DirectorySeparatorChar, '.')}.{fileName}.{extension}";

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime embeddedResourceTime = GetHostProcessLastWriteTimeUTC();

                if (embeddedResourceTime == DateTime.MinValue)
                {
                    Logger.Warn("Last write time of the host process executable is equal to DateTime.MinValue, "
                        + $"resource re-extraction will be skipped this session until the existing file '{fileName}' is removed.");
                }

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
        /// Evaluates the file extension type and attempts to safely terminate any processes holding locks on the target file.
        /// </summary>
        /// <param name="extension">The file extension used to determine the termination strategy(e.g., "exe", "dll").</param>
        /// <param name="targetFileName">The name of the file to search for in the process list.</param>
        /// <param name="targetPath">The full path to the file to check for active file handles.</param>
        /// <param name="skipDll">If true, skips termination of processes using the file if it's a DLL. This is used when multiple resources are being copied to avoid redundant process termination attempts.</param>
        /// <returns>True if all blocking processes were terminated or none were found; false if termination failed.</returns>
        private bool TerminateBlockingProcesses(string extension, string targetFileName, string targetPath, bool skipDll = false)
        {
            var isExe = extension.Equals("exe", StringComparison.OrdinalIgnoreCase);
            var isDll = extension.Equals("dll", StringComparison.OrdinalIgnoreCase);

            if (isExe && !_processKiller.KillProcessTreeAndParents(targetFileName))
                return false;

            if (isDll && !skipDll && !_processKiller.KillProcessesUsingFile(targetPath))
                return false;

            return true;
        }

        #endregion
    }
}