using Servy.Core.Data;
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
        private const int DeltaMinutes = 20; // Time delta in minutes to consider an embedded resource as "newer" than an existing file

        private readonly ServiceHelper _serviceHelper;

        /// <summary>
        /// Initializes a new instance of the ResourceHelper class using the specified service repository.
        /// </summary>
        /// <param name="serviceRepository">The service repository used to access and manage service-related resources. Cannot be null.</param>
        public ResourceHelper(IServiceRepository serviceRepository)
        {
            _serviceHelper = new ServiceHelper(serviceRepository);
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
        public async Task<bool> CopyEmbeddedResource(Assembly assembly, string resourceNamespace, string fileName, string extension, bool stopServices = true, bool isCli = false)
        {
            try
            {
                var targetFileName = fileName + "." + extension;
#if DEBUG
                var dir = Path.GetDirectoryName(assembly.Location);
                var targetPath = Path.Combine(dir!, targetFileName);
#else
                var targetPath = Path.Combine(AppConfig.ProgramDataPath, targetFileName);
#endif

                var targetPathDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(targetPathDir))
                {
                    Directory.CreateDirectory(targetPathDir!);
                }

                var resourceName = resourceNamespace + "." + fileName + "." + extension;

                var shouldCopy = true;
                if (File.Exists(targetPath))
                {
                    DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                    DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(assembly);

                    Logger.Debug($"Existing file '{targetPath}' last write time: {existingFileTime.ToLocalTime():G}");
                    Logger.Debug($"Embedded resource '{resourceName}' last write time: {embeddedResourceTime.ToLocalTime():G}");

                    // Only copy if the embedded resource is newer by more than DeltaMinutes
                    shouldCopy = embeddedResourceTime > existingFileTime.AddMinutes(DeltaMinutes);

                    if (!shouldCopy && embeddedResourceTime > existingFileTime)
                    {
                        Logger.Debug($"Embedded resource '{resourceName}' is newer, but within the {DeltaMinutes}-minute delta. Skipping copy.");
                    }
                }

                if (!shouldCopy)
                    return true;

                var isExe = extension.Equals("exe", StringComparison.OrdinalIgnoreCase);
                var isDll = extension.Equals("dll", StringComparison.OrdinalIgnoreCase);

                // Get running services
                var runningServices = new List<string>();
                if (stopServices)
                {
                    //runningServices = ServiceHelper.GetRunningServyServices();
                    runningServices = isCli ? _serviceHelper.GetRunningServyCLIServices() : _serviceHelper.GetRunningServyUIServices();
                }

                try
                {
                    if (stopServices)
                    {
                        if (runningServices.Count > 0)
                            Logger.Info($"Stopping services before copying resource '{resourceName}': {string.Join(", ", runningServices)}");
                        await _serviceHelper.StopServices(runningServices);
                    }

                    if (isExe && !ProcessKiller.KillProcessTreeAndParents(targetFileName))
                        return false;

                    if (isDll && !ProcessKiller.KillProcessesUsingFile(targetPath))
                        return false;

                    Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream == null)
                    {
                        Logger.Error($"Embedded resource not found: {resourceName}");
                        return false;
                    }

                    using (resourceStream)
                    {
                        using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            await resourceStream.CopyToAsync(fileStream);
                        }
                    }
                }
                finally
                {
                    if (stopServices)
                    {
                        if (runningServices.Count > 0)
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
        /// Retrieves the last write time of the assembly that contains the embedded resource.
        /// This timestamp is used to determine whether the resource should be updated on disk.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded resource.
        /// </param>
        /// <returns>
        /// The UTC <see cref="DateTime"/> representing the assembly's last write time,
        /// or <see cref="DateTime.UtcNow"/> if it cannot be determined.
        /// </returns>
        public DateTime GetEmbeddedResourceLastWriteTime(Assembly assembly)
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
            catch
            {
                // Fallback to AppContext if Process access is restricted (rare on Windows)
                try
                {
                    var exeName = AppDomain.CurrentDomain.FriendlyName;
                    var exePath = Path.Combine(AppContext.BaseDirectory, exeName);
                    if (File.Exists(exePath))
                    {
                        return File.GetLastWriteTimeUtc(exePath);
                    }
                }
                catch
                {
                    // Ignore exceptions and fallback to UtcNow
                }
            }

            return DateTime.UtcNow;
        }

    }
}
