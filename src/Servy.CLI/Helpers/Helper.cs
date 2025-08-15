using CommandLine;
using Servy.CLI.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;

namespace Servy.CLI.Helpers
{
    internal static class Helper
    {
        /// <summary>
        /// Gets the verb name defined by the <see cref="VerbAttribute"/> on the specified options class.
        /// </summary>
        /// <typeparam name="T">The options class decorated with <see cref="VerbAttribute"/>.</typeparam>
        /// <returns>The verb name defined in the <see cref="VerbAttribute"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the class does not have a <see cref="VerbAttribute"/>.</exception>
        public static string GetVerbName<T>()
        {
            var verbAttr = typeof(T).GetCustomAttribute<VerbAttribute>();
            if (verbAttr == null)
                throw new InvalidOperationException($"Class {typeof(T).Name} does not have a VerbAttribute.");

            return verbAttr.Name;
        }

        /// <summary>
        /// Gets all verb names defined by <see cref="VerbAttribute"/> on all types in the current assembly.
        /// </summary>
        /// <returns>An array of verb names.</returns>
        public static string[] GetVerbs()
        {
            var verbs = Assembly.GetExecutingAssembly()
                 .GetTypes()
                 .Where(t => t.GetCustomAttribute<VerbAttribute>() != null)
                 .Select(t => t.GetCustomAttribute<VerbAttribute>().Name.ToLowerInvariant())
                 .ToArray();
            return verbs;
        }

        /// <summary>
        /// Kills all running processes with the name.
        /// This is necessary when replacing the embedded service executable.
        /// </summary>
        /// <param name="servyProcessName">Servy Process Name to kill.</param>
        public static void KillServyServiceIfRunning(string servyProcessName)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(servyProcessName))
                {
                    KillProcessAndChildren(process.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to terminate running service: " + ex.Message);
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

        /// <summary>
        /// Copies the embedded executable resource to the application's base directory
        /// if the file does not exist or if the embedded version is newer.
        /// </summary>
        /// <param name="fileName">The name of the embedded resource without extension.</param>
        /// <param name="extension">The extension of the embedded resource.</param>
        public static void CopyEmbeddedResource(string fileName, string extension)
        {
            string targetFileName = $"{fileName}.{extension}";
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetFileName);
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = $"Servy.CLI.Resources.{fileName}.{extension}";

            bool shouldCopy = true;

            if (File.Exists(targetPath))
            {
                DateTime existingFileTime = File.GetLastWriteTimeUtc(targetPath);
                DateTime embeddedResourceTime = GetEmbeddedResourceLastWriteTime(asm);
                shouldCopy = embeddedResourceTime > existingFileTime;
            }

            if (shouldCopy)
            {
                if (extension.ToLower().Equals("exe"))
                    KillServyServiceIfRunning(targetFileName);

                using (Stream resourceStream = asm.GetManifestResourceStream(resourceName))
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

        /// <summary>
        /// Awaits the execution of a <see cref="CommandResult"/>-returning task,
        /// prints its message to the console, and returns an appropriate exit code.
        /// </summary>
        /// <param name="task">The task that produces a <see cref="CommandResult"/>.</param>
        /// <returns>
        /// A <see cref="Task{Int32}"/> representing the asynchronous operation,
        /// with 0 if <see cref="CommandResult.Success"/> is <c>true</c>, or 1 otherwise.
        /// </returns>
        public static async Task<int> PrintAndReturnAsync(Task<CommandResult> task)
        {
            var result = await task;
            Console.WriteLine(result.Message);
            return result.Success ? 0 : 1;
        }

    }
}
