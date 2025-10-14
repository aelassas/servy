using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
    public static class ResourceHelper
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

                var targetPathDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(targetPathDir))
                {
                    Directory.CreateDirectory(targetPathDir);
                }

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
        /// Copies embedded resources (such as DLLs and EXEs) from the specified assembly to target paths.
        /// </summary>
        /// <param name="assembly">
        /// The <see cref="Assembly"/> that contains the embedded resources.
        /// </param>
        /// <param name="resourceNamespace">
        /// The root namespace under which the resources are embedded.
        /// </param>
        /// <param name="resourceItems">
        /// A list of <see cref="ResourceItem"/> objects describing the resources to copy, including file names,
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
        public static bool CopyResources(Assembly assembly, string resourceNamespace, List<ResourceItem> resourceItems, bool stopServices = true)
        {
            var res = true;
            try
            {
                foreach (var resourceItem in resourceItems)
                {
                    var fileName = resourceItem.FileNameWithoutExtension;
                    var extension = resourceItem.Extension;
                    var subfolder = resourceItem.Subfolder;
                    var targetFileName = resourceItem.FileNameWithoutExtension + "." + extension;
                    resourceItem.TagetFileName = targetFileName;

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

                    var targetPathDir = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(targetPathDir))
                    {
                        Directory.CreateDirectory(targetPathDir);
                    }

                    resourceItem.TagetPath = targetPath;

                    var resourceName = string.IsNullOrEmpty(subfolder)
                        ? $"{resourceNamespace}.{fileName}.{extension}"
                        : $"{resourceNamespace}.{subfolder}.{fileName}.{extension}";
                    resourceItem.ResourceName = resourceName;

                    resourceItem.ShouldCopy = !File.Exists(targetPath);

                    if (File.Exists(targetPath))
                    {
                        DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                        DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(assembly);
                        resourceItem.ShouldCopy = embeddedResourceTime > existingFileTime;
                    }
                }

                if (resourceItems.All(r => !r.ShouldCopy))
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

                    foreach (var resourceItem in resourceItems.Where(r => r.ShouldCopy))
                    {
                        try
                        {
                            var targetFileName = resourceItem.TagetFileName;
                            var extension = resourceItem.Extension;
                            var targetPath = resourceItem.TagetPath;
                            var isExe = extension.Equals("exe", StringComparison.OrdinalIgnoreCase);
                            var isDll = extension.Equals("dll", StringComparison.OrdinalIgnoreCase);

                            if (isExe && !ProcessKiller.KillProcessTreeAndParents(targetFileName))
                            {
                                res = false;
                                continue;
                            }

                            //if (isDll && !ProcessKiller.KillProcessesUsingFile(targetPath))
                            //{
                            //    res = false;
                            //    continue;
                            //}

                            Stream resourceStream = assembly.GetManifestResourceStream(resourceItem.ResourceName);
                            if (resourceStream == null)
                            {
                                Debug.WriteLine("Embedded resource not found: " + resourceItem.ResourceName);
                                res = false;
                                continue;
                            }

                            var dirPath = Path.GetDirectoryName(resourceItem.TagetPath);
                            if (!Directory.Exists(dirPath))
                            {
                                Directory.CreateDirectory(dirPath);
                            }

                            using (resourceStream)
                            {
                                using (FileStream fileStream = new FileStream(resourceItem.TagetPath, FileMode.Create, FileAccess.Write))
                                {
                                    resourceStream.CopyTo(fileStream);
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine("Failed to copy embedded resource " + resourceItem.FileNameWithoutExtension + ": " + ex);
                            res = false;
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to copy embedded resources: " + ex);
                res= false;
            }

            return res;
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
