using Servy.Core.Data;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private readonly IProcessHelper _processHelper;
        private readonly IProcessKiller _processKiller;

        /// <summary>
        /// Initializes a new instance of the ResourceHelper class using the specified service repository.
        /// </summary>
        /// <param name="serviceRepository">The service repository used to access and manage service-related resources. Cannot be null.</param>
        /// <param name="processHelper">The process helper used to manage processes. Cannot be null.</param>
        /// <param name="processKiller">The process killer used to terminate processes. Cannot be null.</param>
        public ResourceHelper(
            IServiceRepository serviceRepository,
            IProcessHelper processHelper,
            IProcessKiller processKiller)
        {
            _serviceHelper = new ServiceHelper(serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository)));
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
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
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public async Task<bool> CopyEmbeddedResource(
            Assembly assembly,
            string resourceNamespace,
            string fileName,
            string extension,
            bool stopServices = true,
            string subfolder = null)
        {
            try
            {
                if (!ShouldCopyResource(assembly, resourceNamespace, fileName, extension, subfolder, out var targetPath, out var targetFileName, out var resourceName))
                    return true;

                // Get running services
                var runningServices = stopServices ? _serviceHelper.GetRunningServyServices() : new List<string>();

                try
                {
                    if (stopServices && runningServices.Count > 0)
                    {
                        Logger.Info($"Stopping services before copying resource '{resourceName}': {string.Join(", ", runningServices)}");
                        await _serviceHelper.StopServices(runningServices);
                    }

                    if (!TerminateBlockingProcesses(_processHelper, extension, targetFileName, targetPath))
                        return false;

                    Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
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
        /// <param name="subfolder">Optional subfolder within the target directory.</param>
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public bool CopyEmbeddedResourceSync(
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

                if (!TerminateBlockingProcesses(_processHelper, extension, targetFileName, targetPath))
                    return false;

                Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
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
                            if (!TerminateBlockingProcesses(_processHelper, resourceItem.Extension, resourceItem.TargetFileName, resourceItem.TargetPath, skipDll: true))
                            {
                                res = false;
                                continue;
                            }

                            Stream resourceStream = assembly.GetManifestResourceStream(resourceItem.ResourceName);
                            if (resourceStream == null)
                            {
                                Logger.Error($"Embedded resource not found: {resourceItem.ResourceName}");
                                res = false;
                                continue;
                            }

                            using (resourceStream)
                            {
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
                        Logger.Info($"Starting stopped services after copying resources: {string.Join(", ", runningServices)}");
                        await _serviceHelper.StartServices(runningServices);
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
        /// Retrieves the last write time of the assembly that contains the embedded resource.
        /// This timestamp is used to determine whether the resource should be updated on disk.
        /// </summary>
        /// <returns>
        /// The UTC <see cref="DateTime"/> representing the assembly's last write time,
        /// or <see cref="DateTime.UtcNow"/> if it cannot be determined.
        /// </returns>
        public DateTime GetEmbeddedResourceLastWriteTimeUTC()
        {
            // Try to get the executable's last write time
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
#if DEBUG
            var dir = Path.GetDirectoryName(assembly.Location);
            targetPath = string.IsNullOrEmpty(subfolder)
                ? Path.Combine(dir, targetFileName)
                : Path.Combine(dir, subfolder, targetFileName);
#else
            targetPath = string.IsNullOrEmpty(subfolder)
                ? Path.Combine(AppConfig.ProgramDataPath, targetFileName)
                : Path.Combine(AppConfig.ProgramDataPath, subfolder, targetFileName);
#endif

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
        /// Evaluates the file extension type and attempts to safely terminate any processes holding locks on the target file.
        /// </summary>
        /// <param name="processHelper">An instance of IProcessHelper used to query running processes.</param>
        /// <param name="extension">The file extension used to determine the termination strategy(e.g., "exe", "dll").</param>
        /// <param name="targetFileName">The name of the file to search for in the process list.</param>
        /// <param name="targetPath">The full path to the file to check for active file handles.</param>
        /// <param name="skipDll">If true, skips termination of processes using the file if it's a DLL. This is used when multiple resources are being copied to avoid redundant process termination attempts.</param>
        /// <returns>True if all blocking processes were terminated or none were found; false if termination failed.</returns>
        private bool TerminateBlockingProcesses(IProcessHelper processHelper, string extension, string targetFileName, string targetPath, bool skipDll = false)
        {
            var isExe = extension.Equals("exe", StringComparison.OrdinalIgnoreCase);
            var isDll = extension.Equals("dll", StringComparison.OrdinalIgnoreCase);

            if (isExe && !_processKiller.KillProcessTreeAndParents(targetFileName))
                return false;

            if (isDll && !skipDll && !_processKiller.KillProcessesUsingFile(processHelper, targetPath))
                return false;

            return true;
        }

        #endregion
    }
}