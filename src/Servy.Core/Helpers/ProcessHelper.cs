using Servy.Core.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for retrieving and formatting process-related information
    /// such as CPU usage and RAM usage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ProcessHelper
    {
        private static long _lastPruneTicks = DateTime.MinValue.Ticks;
        private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maintains a lightweight sync object for each PID to allow concurrent metrics gathering 
        /// for different processes, while serializing requests for the same process.
        /// </summary>
        private static readonly ConcurrentDictionary<int, object> _pidLocks = new ConcurrentDictionary<int, object>();

        /// <summary>
        /// Retrieves or creates a synchronization object dedicated to a specific process ID.
        /// </summary>
        /// <param name="pid">The unique identifier of the process.</param>
        /// <returns>
        /// A synchronization object that can be used with the <c>lock</c> statement 
        /// to serialize access to the specified process's data.
        /// </returns>
        /// <remarks>
        /// This provides fine-grained locking, allowing the Manager to gather metrics 
        /// for multiple services in parallel while ensuring that concurrent requests 
        /// for the same PID do not corrupt the CPU delta calculations.
        /// </remarks>
        private static object GetLockForPid(int pid)
        {
            return _pidLocks.GetOrAdd(pid, _ => new object());
        }

        #region Native Methods for Process Tree

        /// <summary>
        /// Efficiently retrieves the process ID and all descendant process IDs for a given root process
        /// using native Windows APIs to avoid WMI overhead.
        /// </summary>
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
                    // 3. Only process the child if we haven't seen it before
                    foreach (var child in children.Where(visited.Add))
                    {
                        // If visited.Add returns true, the child is new and already added to the hash set
                        tree.Add(child);
                        queue.Enqueue(child);
                    }
                }
            }

            return tree;
        }

        #endregion

        /// <summary>
        /// Stores the last CPU measurement for a process.
        /// </summary>
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
        private static class CpuTimesStore
        {
            /// <summary>
            /// Stores the last recorded CPU usage sample for each process ID.
            /// </summary>
            public static readonly ConcurrentDictionary<int, CpuSample> PrevCpuTimes = new ConcurrentDictionary<int, CpuSample>();
        }

        /// <summary>
        /// Performs maintenance on the process cache by removing entries for PIDs that are no longer active.
        /// Should be called by a background timer to prevent memory leaks.
        /// </summary>
        public static void MaintainCache()
        {
            // 1. Thread-safe check of the last prune time
            long now = DateTime.UtcNow.Ticks;
            long last = Interlocked.Read(ref _lastPruneTicks);

            if (now - last < PruneInterval.Ticks) return;

            // 2. Atomic CompareExchange: Only the winning thread proceeds to prune.
            // This prevents multiple parallel 'Refresh' tasks from iterating the dictionary simultaneously.
            if (Interlocked.CompareExchange(ref _lastPruneTicks, now, last) != last) return;

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
                    _pidLocks.TryRemove(pid, out _);
                }
            }
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
        /// Retrieves both CPU and RAM metrics for a process using a single handle.
        /// </summary>
        public static ProcessMetrics GetProcessMetrics(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    // RAM is an instant point-in-time read, no lock needed yet.
                    long ram = process.PrivateMemorySize64;

                    // Lock only on this specific PID to safely compute the CPU delta
                    lock (GetLockForPid(pid))
                    {
                        var now = DateTime.UtcNow;
                        var totalTime = process.TotalProcessorTime;

                        if (!CpuTimesStore.PrevCpuTimes.TryGetValue(pid, out var prev) || prev == null)
                        {
                            UpdateCpuSample(pid, now, totalTime);
                            return new ProcessMetrics(0, ram);
                        }

                        var deltaTime = (now - prev.LastTime).TotalMilliseconds;
                        var deltaCpu = (totalTime - prev.LastTotalTime).TotalMilliseconds;

                        double cpu = 0;
                        if (deltaTime > 0 && deltaCpu >= 0)
                        {
                            cpu = (deltaCpu / (deltaTime * Environment.ProcessorCount)) * 100.0;
                            cpu = Math.Round(cpu, 1, MidpointRounding.AwayFromZero);
                        }

                        UpdateCpuSample(pid, now, totalTime);
                        return new ProcessMetrics(cpu, ram);
                    }
                }
            }
            catch (ArgumentException)
            {
                CpuTimesStore.PrevCpuTimes.TryRemove(pid, out _);
                _pidLocks.TryRemove(pid, out _); // Clean up the lock too
                return new ProcessMetrics(0, 0);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to get process metrics for PID {pid}.", ex);
                return new ProcessMetrics(0, 0);
            }
        }

        /// <summary>
        /// Retrieves combined CPU and RAM metrics for an entire process tree.
        /// Performs a single pass over the tree to minimize kernel transitions.
        /// </summary>
        public static ProcessMetrics GetProcessTreeMetrics(int rootPid)
        {
            var pids = GetProcessTree(rootPid);
            double totalCpu = 0;
            long totalRam = 0;

            foreach (var pid in pids)
            {
                var metrics = GetProcessMetrics(pid);
                totalCpu += metrics.CpuUsage;
                totalRam += metrics.RamUsage;
            }

            return new ProcessMetrics(Math.Min(100.0, totalCpu), totalRam);
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
        /// <item><description>1.57 -> "1.6%"</description></item>
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
        public static string? ResolvePath(string? inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return null;

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
        public static bool ValidatePath(string? path, bool isFile = true)
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
        /// Escapes an argument for the Windows command line to prevent breakout.
        /// </summary>
        public static string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";

            // 1. Escape existing double quotes by doubling them or prefixing with \ 
            // (Win32 standard for arguments is backslash-escaping quotes)
            string escaped = arg.Replace("\"", "\\\"");

            // 2. If the string ends with a backslash, it will escape our closing quote.
            // We must double trailing backslashes.
            if (escaped.EndsWith("\\"))
            {
                escaped += "\\";
            }

            // 3. Wrap the whole thing in quotes
            return $"\"{escaped}\"";
        }
    }
}