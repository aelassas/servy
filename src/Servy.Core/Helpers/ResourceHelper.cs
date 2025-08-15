using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Management;
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
        /// Extracts an embedded resource (e.g., an executable or symbol file) from the assembly
        /// and writes it to the application's base directory.
        /// The resource is only copied if it does not already exist or if the embedded version
        /// has a newer timestamp than the existing file.
        /// </summary>
        /// <param name="assembly">
        /// Executing assembly.
        /// </param>
        /// <param name="resourceNamespace">
        /// The namespace within the assembly where the embedded resource is located.
        /// </param>
        /// <param name="fileName">
        /// The file name of the embedded resource without its extension.
        /// </param>
        /// <param name="extension">
        /// The file extension of the embedded resource (e.g., "exe", "pdb").
        /// </param>
        /// <returns>True on success and False on failure.</returns>
        public static bool CopyEmbeddedResource(Assembly assembly, string resourceNamespace, string fileName, string extension)
        {
            string targetFileName = $"{fileName}.{extension}";
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetFileName);
            string resourceName = $"{resourceNamespace}.{fileName}.{extension}";

            bool shouldCopy = true;

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(assembly);
                shouldCopy = embeddedResourceTime > existingFileTime;
            }

            if (shouldCopy)
            {
                if (extension.Equals("exe", StringComparison.OrdinalIgnoreCase) && !KillServyServiceIfRunning(targetFileName))
                {
                    return false;
                }

                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Console.WriteLine("Embedded resource not found: " + resourceName);
                        return false;
                    }

                    using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }

            return true;
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
            string assemblyPath = assembly.Location;
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

        /// <summary>
        /// Kills all running processes with the name.
        /// This is necessary when replacing the embedded service executable.
        /// </summary>
        /// <param name="servyProcessName">Servy Process Name to kill.</param>
        /// <returns>True on success and False on failure.</returns>
        public static bool KillServyServiceIfRunning(string servyProcessName)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(servyProcessName))
                {
                    KillProcessAndChildren(process.Id);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to terminate running service: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Kills process and the entire process tree.
        /// </summary>
        /// <param name="pid">Process PID to kill.</param>
        private static void KillProcessAndChildren(int parentPid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Process WHERE ParentProcessId=" + parentPid);

            ManagementObjectCollection collection = searcher.Get();

            // Kill all child processes recursively first
            foreach (var mo in collection)
            {
                int childPid = Convert.ToInt32(mo["ProcessId"]);
                KillProcessAndChildren(childPid);
            }

            // Now kill the parent process
            try
            {
                Process parentProcess = Process.GetProcessById(parentPid);
                parentProcess.Kill();
                parentProcess.WaitForExit();
            }
            catch (ArgumentException)
            {
                // Process has already exited, no action needed
            }
            catch (Exception)
            {
                // Handle other exceptions if necessary
            }
        }
    }
}
