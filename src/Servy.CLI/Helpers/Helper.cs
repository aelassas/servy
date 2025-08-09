using Servy.CLI.Models;
using System.Diagnostics;
using System.Reflection;

namespace Servy.CLI.Helpers
{
    internal static class Helper
    {
        /// <summary>
        /// Kills all running processes with the name Servy.Service.exe and the entire process tree
        /// This is necessary when replacing the embedded service executable.
        /// </summary>
        public static void KillServyServiceIfRunning()
        {
            const string processName = "Servy.Service";

            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    process.Kill(true);
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                // Handle cases where the process may have already exited or other exceptions
                Console.WriteLine("Failed to terminate running service: " + ex.Message);
            }
        }

        /// <summary>
        /// Copies the embedded executable resource to the application's base directory
        /// if the file does not exist or if the embedded version is newer.
        /// </summary>
        /// <param name="fileName">The name of the embedded resource without extension.</param>
        public static void CopyEmbeddedResource(string fileName)
        {
            string targetFileName = $"{fileName}.exe";
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetFileName);
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = $"Servy.CLI.Resources.{fileName}.exe";

            bool shouldCopy = true;

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(asm);
                shouldCopy = embeddedResourceTime > existingFileTime;
            }

            if (shouldCopy)
            {
                using (Stream? resourceStream = asm.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Console.WriteLine("Embedded resource not found: " + resourceName);
                        return;
                    }

                    using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the last write time of the embedded resource using the assembly's timestamp.
        /// </summary>
        /// <param name="assembly">The assembly containing the resource.</param>
        /// <returns>The DateTime of the assembly's last write time in UTC, or current UTC time if unavailable.</returns>
        public static DateTime GetEmbeddedResourceLastWriteTime(Assembly assembly)
        {
            #pragma warning disable IL3000
            string assemblyPath = assembly.Location;
            #pragma warning restore IL3000

            if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
            {
                return File.GetLastWriteTimeUtc(assemblyPath);
            }

            // Fallback: try to get the executable's last write time using AppContext.BaseDirectory + executable name
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
                // Ignore exceptions, fallback to UtcNow below
            }

            return DateTime.UtcNow;
        }

        /// <summary>
        /// Executes the output handling for a command result by printing its message to the console
        /// and returning its exit code.
        /// </summary>
        /// <param name="result">The <see cref="CommandResult"/> containing the message and exit code from the executed command.</param>
        /// <returns>The integer exit code associated with the command result.</returns>
        public static int PrintAndReturn(CommandResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
                Console.WriteLine(result.Message);

            return result.ExitCode;
        }

    }
}
