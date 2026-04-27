using Servy.Core.Data;
using Servy.Core.Domain;
using Servy.Core.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
#if !DEBUG
using Servy.Core.Config;
#endif

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for managing and extracting embedded resources
    /// from the assembly, such as the Servy service executable and related files.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ResourceHelper
    {
        /// <summary>
        /// The maximum age in minutes an extracted resource can be before it is considered stale.
        /// </summary>
        private const int ResourceStalenessThresholdMinutes = 20; // Time delta in minutes to consider an embedded resource as "newer" than an existing file

        private readonly ServiceHelper _serviceHelper;
        private readonly IProcessKiller _processKiller;

        /// <summary>
        /// Initializes a new instance of the ResourceHelper class using the specified service repository.
        /// </summary>
        /// <param name="serviceRepository">The service repository used to access and manage service-related resources. Cannot be null.</param>
        /// <param name="processKiller">The process killer used to terminate processes. Cannot be null.</param>
        public ResourceHelper(
            IServiceRepository serviceRepository,
            IProcessKiller processKiller)
        {
            _serviceHelper = new ServiceHelper(serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository)));
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
            try
            {
                if (!ShouldCopyResource(assembly, resourceNamespace, fileName, extension, out var targetPath, out var targetFileName, out var resourceName))
                    return true;

                // Get running services
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

                    if (!TerminateBlockingProcesses(extension, targetFileName, targetPath))
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
                }
                finally
                {
                    if (stopServices && runningServices.Count > 0)
                    {
                        Logger.Info($"Starting stopped services after copying resource '{resourceName}': {string.Join(", ", runningServices)}");
                        await _serviceHelper.StartServices(runningServices);
                    }
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
        /// Copies an embedded resource from the assembly to disk.
        /// </summary>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <param name="resourceNamespace">Namespace of the embedded resource.</param>
        /// <param name="fileName">The filename of the resource without extension.</param>
        /// <param name="extension">The file extension (e.g., "exe" or "dll").</param>
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public bool CopyEmbeddedResourceSync(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension)
        {
            try
            {
                if (!ShouldCopyResource(assembly, resourceNamespace, fileName, extension, out var targetPath, out var targetFileName, out var resourceName))
                    return true;

                if (!TerminateBlockingProcesses(extension, targetFileName, targetPath))
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
        /// Retrieves the last write time of the assembly that contains the embedded resource.
        /// </summary>
        public DateTime GetEmbeddedResourceLastWriteTimeUTC()
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
                    var exePath = Path.Combine(AppContext.BaseDirectory, exeName);
                    if (File.Exists(exePath))
                    {
                        return File.GetLastWriteTimeUtc(exePath);
                    }
                }
                catch (Exception innerEx)
                {
                    // Ignore exceptions and fallback to UtcNow
                    Logger.Debug("AppDomain fallback also failed, using UtcNow.", innerEx);
                }
            }

            return DateTime.UtcNow;
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
        /// <param name="targetFileName">Output parameter containing the combined filename and extension.</param>
        /// <param name="resourceName">Output parameter containing the full manifest resource name used for extraction.</param>
        /// <returns>True if the resource needs to be copied; false if the existing file is up to date.</returns>
        private bool ShouldCopyResource(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            out string targetPath,
            out string targetFileName,
            out string resourceName)
        {
            targetFileName = fileName + "." + extension;
#if DEBUG
            var dir = Path.GetDirectoryName(assembly.Location);
            targetPath = Path.Combine(dir!, targetFileName);
#else
            targetPath = Path.Combine(AppConfig.ProgramDataPath, targetFileName);
#endif

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
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTimeUTC();

                Logger.Debug($"Existing file '{targetPath}' last write time: {existingFileTime.ToLocalTime():G}");
                Logger.Debug($"Embedded resource '{resourceName}' last write time: {embeddedResourceTime.ToLocalTime():G}");

                // Only copy if the embedded resource is newer by more than DeltaMinutes
                bool shouldCopy = embeddedResourceTime > existingFileTime.AddMinutes(ResourceStalenessThresholdMinutes);

                if (!shouldCopy && embeddedResourceTime > existingFileTime)
                {
                    Logger.Debug($"Embedded resource '{resourceName}' is newer, but within the {ResourceStalenessThresholdMinutes}-minute delta. Skipping copy.");
                }

                return shouldCopy;
            }

            return true;
        }

        /// <summary>
        /// Safely terminates any processes holding locks on the target file by identifying them by path.
        /// This prevents collateral damage to other service instances using the same utility names.
        /// </summary>
        /// <param name="extension">The file extension (no longer strictly needed but kept for signature compatibility).</param>
        /// <param name="targetFileName">The bare filename (no longer used for killing to avoid broad matches).</param>
        /// <param name="targetPath">The full path to the file to check for active file handles.</param>
        /// <returns>True if the file was successfully cleared of blocking processes; false if termination failed.</returns>
        private bool TerminateBlockingProcesses(string extension, string targetFileName, string targetPath)
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