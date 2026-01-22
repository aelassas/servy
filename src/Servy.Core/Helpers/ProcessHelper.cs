using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for retrieving and formatting process-related information
    /// such as CPU usage and RAM usage.
    /// </summary>
    public static class ProcessHelper
    {
        /// <summary>
        /// Stores the last CPU measurement for a process.
        /// </summary>
        [ExcludeFromCodeCoverage]
        private sealed class CpuSample
        {
            /// <summary>
            /// The date and time of the last CPU measurement.
            /// </summary>
            public DateTime LastTime;

            /// <summary>
            /// The total processor time used by the process at the last measurement.
            /// </summary>
            public TimeSpan LastTotalTime;
        }

        /// <summary>
        /// Provides a storage container for CPU usage samples.
        /// This class holds the last recorded CPU usage values for each process ID
        /// and is excluded from code coverage because it only acts as an internal cache.
        /// </summary>
        [ExcludeFromCodeCoverage]
        private static class CpuTimesStore
        {
            /// <summary>
            /// Stores the last recorded CPU usage sample for each process ID.
            /// </summary>
            public static readonly ConcurrentDictionary<int, CpuSample> PrevCpuTimes = new ConcurrentDictionary<int, CpuSample>();
        }

        /// <summary>
        /// Gets the CPU usage percentage of a process over the interval since the last sample.
        /// Should be called repeatedly (e.g., by a background timer every 4 seconds).
        /// </summary>
        /// <param name="pid">The process ID.</param>
        /// <returns>The CPU usage percentage rounded to one decimal place, or 0 if not available.</returns>
        [ExcludeFromCodeCoverage]
        public static double GetCpuUsage(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    var now = DateTime.UtcNow;
                    var totalTime = process.TotalProcessorTime;

                    if (!CpuTimesStore.PrevCpuTimes.TryGetValue(pid, out var prev) || prev == null)
                    {
                        // First measurement -> just store sample
                        CpuTimesStore.PrevCpuTimes[pid] = new CpuSample
                        {
                            LastTime = now,
                            LastTotalTime = totalTime
                        };
                        return 0;
                    }

                    var deltaTime = (now - prev.LastTime).TotalMilliseconds;
                    var deltaCpu = (totalTime - prev.LastTotalTime).TotalMilliseconds;

                    // Handle invalid or inconsistent samples
                    if (deltaTime <= 0 || deltaCpu < 0)
                        return 0;

                    // Normalize CPU usage across all cores
                    double usage = (deltaCpu / (deltaTime * Environment.ProcessorCount)) * 100.0;

                    // Update stored sample
                    CpuTimesStore.PrevCpuTimes[pid] = new CpuSample
                    {
                        LastTime = now,
                        LastTotalTime = totalTime
                    };

                    return Math.Round(usage, 1, MidpointRounding.AwayFromZero);
                }
            }
            catch (ArgumentException)
            {
                // Process no longer exists -> remove stale entry
                CpuTimesStore.PrevCpuTimes.TryRemove(pid, out _);
                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the RAM usage of a process by its PID.
        /// </summary>
        /// <param name="pid">The process identifier (PID).</param>
        /// <returns>
        /// The RAM usage in bytes.
        /// Returns <c>0</c> if the process cannot be accessed.
        /// </returns>
        [ExcludeFromCodeCoverage]
        public static long GetRamUsage(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    // Private bytes (close to Task Manager's "Memory" column)
                    return process.PrivateMemorySize64;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Formats a CPU usage value as a percentage string.
        /// </summary>
        /// <param name="cpuUsage">The CPU usage value.</param>
        /// <returns>
        /// A formatted string with a percent sign.
        /// Examples:
        /// <list type="bullet">
        /// <item><description>0 -> "0%"</description></item>
        /// <item><description>0.03 -> "0%"</description></item>
        /// <item><description>1 -> "1.0%"</description></item>
        /// <item><description>1.04 -> "1.0%"</description></item>
        /// <item><description>1.05 -> "1.1%"</description></item>
        /// <item><description>1.06 -> "1.1%"</description></item>
        /// <item><description>1.1 -> "1.1%"</description></item>
        /// <item><description>1.49 -> "1.4%"</description></item>
        /// <item><description>1.51 -> "1.5%"</description></item>
        /// <item><description>1.57 -> "1.5%"</description></item>
        /// <item><description>1.636 -> "1.6%"</description></item>
        /// </list>
        /// </returns>
        public static string FormatCpuUsage(double cpuUsage)
        {
            double rounded = Math.Round(cpuUsage, 1, MidpointRounding.AwayFromZero);
            const double epsilon = 0.0001;

            string formatted = Math.Abs(rounded) < epsilon
                ? "0"
                : rounded.ToString("0.0", CultureInfo.InvariantCulture);

            return $"{formatted}%";
        }

        /// <summary>
        /// Formats a RAM usage value in human-readable units.
        /// </summary>
        /// <param name="ramUsage">The RAM usage in bytes.</param>
        /// <returns>
        /// A formatted string with the most appropriate unit:
        /// B, KB, MB, GB, or TB.
        /// Examples:
        /// <list type="bullet">
        /// <item><description>512 -> "512.0 B"</description></item>
        /// <item><description>2048 -> "2.0 KB"</description></item>
        /// <item><description>1048576 -> "1.0 MB"</description></item>
        /// <item><description>1073741824 -> "1.0 GB"</description></item>
        /// </list>
        /// </returns>
        public static string FormatRamUsage(long ramUsage)
        {
            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;
            const double TB = GB * 1024.0;

            string result;
            if (ramUsage < KB)
            {
                result = $"{ramUsage.ToString("0.0", CultureInfo.InvariantCulture)} B";
            }
            else if (ramUsage < MB)
            {
                result = $"{(ramUsage / KB).ToString("0.0", CultureInfo.InvariantCulture)} KB";
            }
            else if (ramUsage < GB)
            {
                result = $"{(ramUsage / MB).ToString("0.0", CultureInfo.InvariantCulture)} MB";
            }
            else if (ramUsage < TB)
            {
                result = $"{(ramUsage / GB).ToString("0.0", CultureInfo.InvariantCulture)} GB";
            }
            else
            {
                result = $"{(ramUsage / TB).ToString("0.0", CultureInfo.InvariantCulture)} TB";
            }

            return result;
        }

        /// <summary>
        /// Resolves and validates an absolute filesystem path for use by a Windows service.
        /// </summary>
        /// <param name="inputPath">
        /// The input path, which may contain environment variables (e.g. %ProgramFiles%).
        /// Environment variables are expanded using the service account's environment only.
        /// </param>
        /// <returns>
        /// A normalized, absolute path with environment variables expanded.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the path is relative or contains environment variables that could not be expanded.
        /// </exception>
        /// <remarks>
        /// This method is intentionally strict:
        /// <list type="bullet">
        /// <item>Only absolute paths are allowed.</item>
        /// <item>Environment variables must be defined at the system level and visible to the service account.</item>
        /// <item>User-level environment variables are not supported.</item>
        /// </list>
        /// Use <see cref="ValidatePath"/> if you only need a boolean existence check.
        /// </remarks>
        public static string ResolvePath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return inputPath;

            inputPath = inputPath.Trim();

            // 1. Expand variables (Note: only expands variables existing in the SERVICE'S environment)
            var expandedPath = Environment.ExpandEnvironmentVariables(inputPath);

            // 2. Strict Check: If the path still contains %, expansion likely failed 
            // because the variable is not defined for the service account (e.g., LocalSystem).
            var match = Regex.Match(expandedPath, @"%[^%]+%");
            if (match.Success)
            {
                var varName = match.Groups[0].Value;
                throw new InvalidOperationException(
                    $"Environment variable '{varName}' could not be expanded. " +
                    "Ensure it is defined as a System variable and visible to the service account.");
            }

            // 3. Ensure the path is absolute
            if (!Path.IsPathRooted(expandedPath))
            {
                throw new InvalidOperationException(string.Format("Path '{0}' is relative. Only absolute paths are allowed.", expandedPath));
            }

            // 4. Normalize (removes trailing slashes, resolves ..\ segments)
            return Path.GetFullPath(expandedPath);
        }

        /// <summary>
        /// Validates that a file or directory path exists after resolving environment variables
        /// and normalizing the path.
        /// </summary>
        /// <param name="path">
        /// The path to validate. May contain environment variables.
        /// </param>
        /// <param name="isFile">
        /// True to validate a file path; false to validate a directory path.
        /// </param>
        /// <returns>
        /// True if the path resolves successfully and exists; otherwise false.
        /// </returns>
        /// <remarks>
        /// This method never throws exceptions.
        /// Any failure during resolution (such as unexpanded environment variables,
        /// relative paths, or invalid paths) results in a false return value.
        /// </remarks>
        public static bool ValidatePath(string path, bool isFile = true)
        {
            try
            {
                var expandedPath = ResolvePath(path);

                if (string.IsNullOrWhiteSpace(expandedPath)) return false;

                if (isFile)
                {
                    return File.Exists(expandedPath);
                }
                else
                {
                    return Directory.Exists(expandedPath);
                }
            }
            catch
            {
                return false; // ResolvePath failed (unexpanded vars or relative path)
            }
        }

    }

}
