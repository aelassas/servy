using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for retrieving and formatting process-related information
    /// such as CPU usage and RAM usage.
    /// </summary>
    public static class ProcessHelper
    {
        private static DateTime _lastPruneTime = DateTime.MinValue;
        private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(5);

        #region Native Methods for Process Tree

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        [ExcludeFromCodeCoverage]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        /// <summary>
        /// Efficiently retrieves the process ID and all descendant process IDs for a given root process
        /// using native Windows APIs to avoid WMI overhead.
        /// </summary>
        [ExcludeFromCodeCoverage]
        private static List<int> GetProcessTree(int rootPid)
        {
            var tree = new List<int> { rootPid };
            var parentToChildren = new Dictionary<int, List<int>>();

            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == INVALID_HANDLE_VALUE) return tree;

            try
            {
                PROCESSENTRY32 procEntry = new PROCESSENTRY32();
                procEntry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                if (Process32First(snapshot, ref procEntry))
                {
                    do
                    {
                        int pid = (int)procEntry.th32ProcessID;
                        int parentPid = (int)procEntry.th32ParentProcessID;

                        if (!parentToChildren.TryGetValue(parentPid, out var children))
                        {
                            children = new List<int>();
                            parentToChildren[parentPid] = children;
                        }
                        children.Add(pid);

                    } while (Process32Next(snapshot, ref procEntry));
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            // BFS traversal to find all nested descendants with cycle protection
            var queue = new Queue<int>();
            var visited = new HashSet<int>(); // 1. Create a tracking set

            queue.Enqueue(rootPid);
            visited.Add(rootPid); // 2. Mark root as visited

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (parentToChildren.TryGetValue(current, out var children))
                {
                    foreach (var child in children)
                    {
                        // 3. Only process the child if we haven't seen it before
                        if (visited.Add(child))
                        {
                            tree.Add(child);
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            return tree;
        }

        #endregion

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
        /// Retrieves the CPU usage percentage for a process. 
        /// Updates the internal cache with a new sample for delta calculation.
        /// </summary>
        /// <param name="pid">The process ID.</param>
        /// <returns>CPU usage percentage (0.0 to 100.0), or 0 if unavailable.</returns>
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
                        UpdateCpuSample(pid, now, totalTime);
                        return 0;
                    }

                    var deltaTime = (now - prev.LastTime).TotalMilliseconds;
                    var deltaCpu = (totalTime - prev.LastTotalTime).TotalMilliseconds;

                    if (deltaTime <= 0 || deltaCpu < 0) return 0;

                    double usage = (deltaCpu / (deltaTime * Environment.ProcessorCount)) * 100.0;

                    UpdateCpuSample(pid, now, totalTime);

                    return Math.Round(usage, 1, MidpointRounding.AwayFromZero);
                }
            }
            catch (ArgumentException)
            {
                CpuTimesStore.PrevCpuTimes.TryRemove(pid, out _);
                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Performs maintenance on the process cache by removing entries for PIDs that are no longer active.
        /// Should be called by a background timer to prevent memory leaks.
        /// </summary>
        [ExcludeFromCodeCoverage]
        public static void MaintainCache()
        {
            if (DateTime.UtcNow - _lastPruneTime < PruneInterval) return;

            foreach (var pid in CpuTimesStore.PrevCpuTimes.Keys)
            {
                bool isAlive = false;
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        isAlive = !p.HasExited;
                    }
                }
                catch (ArgumentException) { isAlive = false; }

                if (!isAlive)
                {
                    CpuTimesStore.PrevCpuTimes.TryRemove(pid, out _);
                }
            }

            _lastPruneTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Internal helper to mutate the shared sample state.
        /// </summary>
        private static void UpdateCpuSample(int pid, DateTime time, TimeSpan totalTime)
        {
            CpuTimesStore.PrevCpuTimes[pid] = new CpuSample
            {
                LastTime = time,
                LastTotalTime = totalTime
            };
        }

        /// <summary>
        /// Retrieves the current physical memory usage (Private Bytes) for a specific process.
        /// </summary>
        /// <param name="pid">The unique identifier (Process ID) of the target process.</param>
        /// <returns>
        /// The number of bytes allocated for the process that cannot be shared with other processes. 
        /// Returns 0 if the process is not found or if access is denied.
        /// </returns>
        /// <remarks>
        /// This method uses <see cref="Process.PrivateMemorySize64"/>, which represents the current 
        /// commit charge of the process. This value is generally the closest programmatic match to 
        /// the "Memory (Private Working Set)" column seen in Windows Task Manager.
        /// </remarks>
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
        /// Gets the combined CPU usage percentage of a process and all of its child descendant processes.
        /// Should be called repeatedly (e.g., by a background timer every 4 seconds) to maintain accurate deltas.
        /// </summary>
        /// <param name="pid">The root process ID.</param>
        /// <returns>The combined CPU usage percentage across the tree, rounded to one decimal place.</returns>
        [ExcludeFromCodeCoverage]
        public static double GetProcessTreeCpuUsage(int pid)
        {
            var pids = GetProcessTree(pid);
            double totalCpu = 0;

            foreach (var p in pids)
            {
                totalCpu += GetCpuUsage(p);
            }

            // Round to 1 decimal place
            double roundedTotal = Math.Round(totalCpu, 1, MidpointRounding.AwayFromZero);

            // Cap the maximum possible output at 100.0% to gracefully handle thread timer lag anomalies
            return Math.Min(100.0, roundedTotal);
        }

        /// <summary>
        /// Retrieves the combined physical memory usage (Private Bytes) for a specific process 
        /// and all of its child descendant processes.
        /// </summary>
        /// <param name="pid">The root process ID.</param>
        /// <returns>The total number of private bytes allocated across the entire process tree.</returns>
        [ExcludeFromCodeCoverage]
        public static long GetProcessTreeRamUsage(int pid)
        {
            var pids = GetProcessTree(pid);
            long totalRam = 0;

            foreach (var p in pids)
            {
                totalRam += GetRamUsage(p);
            }

            return totalRam;
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
                throw new InvalidOperationException($"Path '{expandedPath}' is relative. Only absolute paths are allowed.");
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

        /// <summary>
        /// Encapsulates a string argument in double quotes and escapes internal quotes and backslashes 
        /// according to the Win32 'CommandLineToArgvW' rules.
        /// </summary>
        /// <remarks>
        /// This is required in .NET Framework 4.8 and earlier when building <see cref="System.Diagnostics.ProcessStartInfo.Arguments"/> 
        /// to prevent argument injection vulnerabilities and ensure paths with spaces or special characters are parsed correctly.
        /// 
        /// Rule summary:
        /// 1. The argument is wrapped in double quotes.
        /// 2. Double quotes inside the string are escaped with a backslash (\").
        /// 3. Backslashes are treated literally unless they immediately precede a double quote.
        /// 4. If backslashes precede a double quote, they must be doubled (2n) so the last one doesn't escape the quote.
        /// </remarks>
        /// <param name="arg">The raw command-line argument to escape.</param>
        /// <returns>A shell-safe, quoted string ready for use in a process start command.</returns>
        public static string EscapeProcessArgument(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return "\"\"";

            // Replace " with \"
            // But we must also handle backslashes that precede a "
            // because \" is treated as a literal quote, and \\" is a literal backslash + quote.
            // The logic: 2n backslashes + " => n backslashes + literal "
            // 2n+1 backslashes + " => n backslashes + literal " + escape next... 
            // Actually, a simpler way for standard .NET/Windows:
            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            for (int i = 0; i < arg.Length; i++)
            {
                int backslashCount = 0;
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashCount++;
                    i++;
                }

                if (i == arg.Length)
                {
                    // Backslashes at the end of the string need to be doubled 
                    // so they don't escape the closing quote
                    sb.Append('\\', backslashCount * 2);
                }
                else if (arg[i] == '"')
                {
                    // Backslashes before a quote need to be doubled, 
                    // and then the quote itself needs a backslash
                    sb.Append('\\', backslashCount * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    // Regular character, just add the backslashes and the char
                    sb.Append('\\', backslashCount);
                    sb.Append(arg[i]);
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}