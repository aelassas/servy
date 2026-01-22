using Servy.Core.Data;
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
                    shouldCopy = embeddedResourceTime > existingFileTime;
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
                        await _serviceHelper.StopServices(runningServices);

                    if (isExe && !ProcessKiller.KillProcessTreeAndParents(targetFileName))
                        return false;

                    if (isDll && !ProcessKiller.KillProcessesUsingFile(targetPath))
                        return false;

                    Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream == null)
                    {
                        Debug.WriteLine("Embedded resource not found: " + resourceName);
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
                        await _serviceHelper.StartServices(runningServices);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to copy embedded resource " + fileName + ": " + ex);
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
#pragma warning disable IL3000
            var assemblyPath = assembly.Location;
#pragma warning restore IL3000

            if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
            {
                return File.GetLastWriteTimeUtc(assemblyPath);
            }

            // Fallback: try to get the executable's last write time
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

            return DateTime.UtcNow;
        }

    }
}
