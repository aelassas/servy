using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for retrieving and formatting process-related information
    /// such as CPU usage and RAM usage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ProcessHelper : IProcessHelper
    {
        private long _lastPruneTicks = DateTime.MinValue.Ticks;

        /// <summary>
        /// Maintains a lightweight sync object for each PID to allow concurrent metrics gathering 
        /// for different processes, while serializing requests for the same process.
        /// </summary>
        private readonly ConcurrentDictionary<int, object> _pidLocks = new ConcurrentDictionary<int, object>();

        /// <summary>
        /// Stores the last recorded CPU usage sample for each process ID.
        /// </summary>
        private readonly ConcurrentDictionary<int, CpuSample> _prevCpuTimes = new ConcurrentDictionary<int, CpuSample>();

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
        private object GetLockForPid(int pid)
        {
            return _pidLocks.GetOrAdd(pid, _ => new object());
        }

        #region Native Methods for Process Tree

        /// <summary>
        /// Efficiently retrieves the process ID and all descendant process IDs for a given root process
        /// using native Windows APIs to avoid WMI overhead.
        /// </summary>
        private List<int> GetProcessTree(int rootPid)
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

        /// <inheritdoc />
        public void MaintainCache()
        {
            // 1. Thread-safe check of the last prune time
            long now = DateTime.UtcNow.Ticks;
            long last = Interlocked.Read(ref _lastPruneTicks);

            if (now - last < AppConfig.ProcessHelperPruneInterval.Ticks) return;

            // 2. Atomic CompareExchange: Only the winning thread proceeds to prune.
            // This prevents multiple parallel 'Refresh' tasks from iterating the dictionary simultaneously.
            if (Interlocked.CompareExchange(ref _lastPruneTicks, now, last) != last) return;

            foreach (var pid in _prevCpuTimes.Keys)
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
                    // Synchronize eviction with any in-flight metric requests for this recycled PID.
                    lock (GetLockForPid(pid))
                    {
                        _prevCpuTimes.TryRemove(pid, out _);

                        // NOTE: We intentionally DO NOT remove from _pidLocks. 
                        // Evicting lock objects creates a TOCTOU race where a concurrent GetProcessMetrics 
                        // could allocate a new lock, resulting in two threads mutating _prevCpuTimes 
                        // simultaneously. The dictionary is bounded by the OS PID limit, so memory growth is negligible.
                    }
                }
            }
        }

        /// <summary>
        /// Internal helper to mutate the shared sample state.
        /// </summary>
        private void UpdateCpuSample(int pid, DateTime time, TimeSpan totalTime)
        {
            _prevCpuTimes[pid] = new CpuSample
            {
                LastTime = time,
                LastTotalTime = totalTime
            };
        }

        /// <inheritdoc />
        public ProcessMetrics GetProcessMetrics(int pid)
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

                        if (!_prevCpuTimes.TryGetValue(pid, out var prev) || prev == null)
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
                // The process has exited or is inaccessible. Safely evict the CPU sample.
                lock (GetLockForPid(pid))
                {
                    _prevCpuTimes.TryRemove(pid, out _);
                    // Intentionally preserving the lock in _pidLocks
                }
                return new ProcessMetrics(0, 0);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to get process metrics for PID {pid}.", ex);
                return new ProcessMetrics(0, 0);
            }
        }

        /// <inheritdoc />
        public ProcessMetrics GetProcessTreeMetrics(int rootPid)
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

            // We allow the sum to exceed 100% to accurately reflect multi-core utilization.
            return new ProcessMetrics(totalCpu, totalRam);
        }

        /// <inheritdoc />
        public string FormatCpuUsage(double cpuUsage)
        {
            double rounded = Math.Round(cpuUsage, 1, MidpointRounding.AwayFromZero);
            const double epsilon = 0.0001;

            string formatted = Math.Abs(rounded) < epsilon
                ? "0"
                : rounded.ToString("0.0", CultureInfo.InvariantCulture);

            return $"{formatted}%";
        }

        /// <inheritdoc />
        public string FormatRamUsage(long ramUsage)
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

        /// <inheritdoc />
        public string ResolvePath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return null;

            inputPath = inputPath.Trim();

            // 1. Expand variables (Note: only expands variables existing in the calling process's environment)
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

        /// <inheritdoc />
        public bool ValidatePath(string path, bool isFile = true)
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