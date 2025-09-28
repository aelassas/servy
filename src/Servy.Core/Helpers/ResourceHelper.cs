using Servy.Core.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

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
        /// Copies an embedded resource from the assembly to disk, stopping and restarting services if necessary.
        /// </summary>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <param name="resourceNamespace">Namespace of the embedded resource.</param>
        /// <param name="fileName">The filename of the resource without extension.</param>
        /// <param name="extension">The file extension (e.g., "exe" or "dll").</param>
        /// <param name="stopServices">Whether to stop services before copying the resource.</param>
        /// <param name="subfolder">Optional subfolder within the target directory.</param>
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public static bool CopyEmbeddedResource(Assembly assembly, string resourceNamespace, string fileName, string extension, bool stopServices = true, string subfolder = null)
        {
            try
            {
                var targetFileName = fileName + "." + extension;
#if DEBUG
                var dir = Path.GetDirectoryName(assembly.Location);
                var targetPath = string.IsNullOrEmpty(subfolder)
                    ? Path.Combine(dir, targetFileName)
                    : Path.Combine(dir, subfolder, targetFileName);
#else
                var targetPath = string.IsNullOrEmpty(subfolder)
                    ? Path.Combine(AppConfig.ProgramDataPath, targetFileName)
                    : Path.Combine(AppConfig.ProgramDataPath, subfolder, targetFileName);
#endif

                var resourceName = string.IsNullOrEmpty(subfolder)
                    ? $"{resourceNamespace}.{fileName}.{extension}"
                    : $"{resourceNamespace}.{subfolder}.{fileName}.{extension}";

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
                    runningServices = ServiceHelper.GetRunningServyServices();
                }

                try
                {
                    if (stopServices)
                        ServiceHelper.StopServices(runningServices);

                    if (isExe && !ProcessKiller.KillProcessTreeAndParents(targetFileName))
                        return false;

                    if (isDll && !ProcessKiller.KillProcessesUsingFile(targetPath))
                        return false;

                    Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream == null)
                    {
                        Debug.WriteLine("Embedded resource not found: " + resourceName);
                        return false;
                    }

                    var dirPath = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }

                    using (resourceStream)
                    {
                        using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            resourceStream.CopyTo(fileStream);
                        }
                    }
                }
                finally
                {
                    if (stopServices)
                    {
                        ServiceHelper.StartServices(runningServices);
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
        /// Copies embedded DLL resources from the specified <see cref="Assembly"/> to the file system.  
        /// If the target DLL files already exist, they are only overwritten if the embedded resource  
        /// has a more recent last write time than the existing file. Optionally, running Servy services  
        /// are stopped before copying and restarted afterward to prevent file lock issues.
        /// </summary>
        /// <param name="assembly">
        /// The <see cref="Assembly"/> that contains the embedded DLL resources.
        /// </param>
        /// <param name="resourceNamespace">
        /// The root namespace where the embedded DLL resources are located.
        /// </param>
        /// <param name="dllResources">
        /// A list of <see cref="DllResource"/> objects representing the DLLs to extract and copy.
        /// Each entry will be updated with its resolved target path, file name, resource name, and copy status.
        /// </param>
        /// <param name="stopServices">
        /// Indicates whether running Servy services should be stopped before copying and restarted afterward.  
        /// Default is <c>true</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if all DLL resources were successfully copied or no copy was needed;  
        /// <c>false</c> if copying fails or an embedded resource cannot be found.
        /// </returns>
        /// <remarks>
        /// - Existing files are compared against the embedded resource's last write time to determine if they should be replaced.  
        /// - When <paramref name="stopServices"/> is <c>true</c>, all running Servy services are stopped before copying and restarted after.  
        /// - Directory structures are created automatically if they do not exist.  
        /// - Errors are logged via <see cref="Debug.WriteLine"/> but not thrown.  
        /// </remarks>
        public static bool CopyDLLResources(Assembly assembly, string resourceNamespace, List<DllResource> dllResources, bool stopServices = true)
        {
            try
            {
                var extension = DllResource.Extension;

                foreach (var dllResource in dllResources)
                {
                    var fileName = dllResource.FileNameWithoutExtension;
                    var subfolder = dllResource.Subfolder;
                    var targetFileName = dllResource.FileNameWithoutExtension + "." + extension;
                    dllResource.TagetFileName = targetFileName;

#if DEBUG
                    var dir = Path.GetDirectoryName(assembly.Location);
                    var targetPath = string.IsNullOrEmpty(subfolder)
                        ? Path.Combine(dir, targetFileName)
                        : Path.Combine(dir, subfolder, targetFileName);
#else
                    var targetPath = string.IsNullOrEmpty(subfolder)
                        ? Path.Combine(AppConfig.ProgramDataPath, targetFileName)
                        : Path.Combine(AppConfig.ProgramDataPath, subfolder, targetFileName);
#endif

                    dllResource.TagetPath = targetPath;

                    var resourceName = string.IsNullOrEmpty(subfolder)
                        ? $"{resourceNamespace}.{fileName}.{extension}"
                        : $"{resourceNamespace}.{subfolder}.{fileName}.{extension}";
                    dllResource.ResourceName = resourceName;

                    dllResource.ShouldCopy = !File.Exists(targetPath);

                    if (File.Exists(targetPath))
                    {
                        DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                        DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(assembly);
                        dllResource.ShouldCopy = embeddedResourceTime > existingFileTime;
                    }
                }

                if (dllResources.All(r => !r.ShouldCopy))
                    return true;

                // Get running services
                var runningServices = new List<string>();
                if (stopServices)
                {
                    runningServices = ServiceHelper.GetRunningServyServices();
                }

                try
                {
                    if (stopServices)
                        ServiceHelper.StopServices(runningServices);

                    //if (!ProcessKiller.KillProcessesUsingFile(targetPath))
                    //    return false;


                    foreach (var dllResource in dllResources.Where(r => r.ShouldCopy))
                    {
                        Stream resourceStream = assembly.GetManifestResourceStream(dllResource.ResourceName);
                        if (resourceStream == null)
                        {
                            Debug.WriteLine("Embedded resource not found: " + dllResource.ResourceName);
                            return false;
                        }

                        var dirPath = Path.GetDirectoryName(dllResource.TagetPath);
                        if (!Directory.Exists(dirPath))
                        {
                            Directory.CreateDirectory(dirPath);
                        }

                        using (resourceStream)
                        {
                            using (FileStream fileStream = new FileStream(dllResource.TagetPath, FileMode.Create, FileAccess.Write))
                            {
                                resourceStream.CopyTo(fileStream);
                            }
                        }
                    }


                }
                finally
                {
                    if (stopServices)
                    {
                        ServiceHelper.StartServices(runningServices);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to copy embedded DLL resources: " + ex);
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
        public static DateTime GetEmbeddedResourceLastWriteTime(Assembly assembly)
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
