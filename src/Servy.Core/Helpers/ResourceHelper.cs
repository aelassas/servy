﻿using Servy.Core.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
        /// <returns>True if the copy succeeded or was not needed, false if it failed.</returns>
        public static bool CopyEmbeddedResource(Assembly assembly, string resourceNamespace, string fileName, string extension, bool stopServices = true)
        {
            try
            {
                var targetFileName = fileName + "." + extension;
#if DEBUG
                var dir = Path.GetDirectoryName(assembly.Location);
                var targetPath = Path.Combine(dir, targetFileName);
#else
                var targetPath = Path.Combine(AppConfig.ProgramDataPath, targetFileName);
#endif

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
