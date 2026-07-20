using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Stores the last CPU measurement for a process.
    /// </summary>
    internal sealed class CpuSample
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
    /// Provides helper methods for retrieving and formatting process-related information
    /// such as CPU usage and RAM usage.
    /// </summary>
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
        /// <param name="rootPid">The process ID of the root process to scan.</param>
        /// <returns>A list of process IDs representing the validated process tree.</returns>
        private List<int> GetProcessTree(int rootPid)
        {
            var tree = new List<int> { rootPid };

            // 1. Defensiveness against PID reuse: Capture the root process's creation time boundary.
            // If the root process has already exited, short-circuit immediately to avoid capturing phantom children.
            DateTime rootStartTime;
            try
            {
                using (var rootProcess = Process.GetProcessById(rootPid))
                {
                    rootStartTime = rootProcess.StartTime;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Win32Exception)
            {
                Logger.Warn($"GetProcessTree: Root process {rootPid} is no longer running. Returning empty tree boundary.");
                return tree;
            }

            // Leverage centralized Toolhelp32 helper to ensure consistent snapshots and avoid logic duplication bugs.
            var (_, parentToChildren) = Toolhelp32Snapshot.BuildSnapshotAndChildMap();

            // Map each validated process to its confirmed start time to ensure temporal checking down the tree
            var processStartTimes = new Dictionary<int, DateTime> { { rootPid, rootStartTime } };

            // BFS traversal to find all nested descendants with cycle and PID-reuse protection
            var queue = new Queue<int>();
            var visited = new HashSet<int>(); // 1. Create a tracking set

            queue.Enqueue(rootPid);
            visited.Add(rootPid); // 2. Mark root as visited

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                // Retrieve the parent's validated start time for downstream comparison
                if (!processStartTimes.TryGetValue(current, out var currentParentStartTime))
                {
                    continue;
                }

                if (parentToChildren.TryGetValue(current, out var children))
                {
                    // 3. Only process the child if we haven't seen it before
                    foreach (var child in children)
                    {
                        if (!visited.Add(child))
                            continue;   // already seen - cycle/duplicate protection

                        // Validate child creation temporal boundary to ensure it is not a recycled PID phantom
                        try
                        {
                            using (var childProcess = Process.GetProcessById(child))
                            {
                                DateTime childStartTime = childProcess.StartTime;

                                // A true descendant can never be created before its parent.
                                // If it is older, this configuration represents a clear OS PID recycling collision.
                                if (childStartTime >= currentParentStartTime.AddSeconds(-AppConfig.PidReuseToleranceSeconds))
                                {
                                    processStartTimes[child] = childStartTime;
                                    tree.Add(child);
                                    queue.Enqueue(child);
                                }
                                else
                                {
                                    Logger.Debug($"GetProcessTree: Pruned recycled PID descendant {child}. " +
                                                 $"Creation time ({childStartTime}) predates parent ({currentParentStartTime}) by more than {AppConfig.PidReuseToleranceSeconds}s.");
                                }
                            }
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Win32Exception)
                        {
                            // Child process exited between snapshot capturing and tree evaluation; skip gracefully
                            Logger.Debug($"GetProcessTree: cannot query start time for PID {child} ({ex.Message}); skipping.");
                        }
                    }
                }
            }

            return tree;
        }

        #endregion

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
                catch (ArgumentException) { /* Process gone */ }
                catch (Win32Exception ex)
                {
                    // Access denied or query failed; process is highly likely still running under elevated/protected context.
                    // Keep the sample entry active to retain baseline delta continuity.
                    isAlive = true;
                    Logger.Debug($"MaintainCache: PID {pid} returned access denied ({ex.Message}); keeping cache sample active.");
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Debug($"MaintainCache: process state for PID {pid} unavailable ({ex.Message}); evicting.");
                }

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
            catch (InvalidOperationException)
            {
                // Process exited between GetProcessById and the property read - same eviction semantics as ArgumentException.
                lock (GetLockForPid(pid)) { _prevCpuTimes.TryRemove(pid, out _); }
                return new ProcessMetrics(0, 0);
            }
            catch (Exception ex)
            {
                // Win32Exception (access denied) and similar - process likely still alive, keep the sample.
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

            // CPU is normalized 0-100% of whole-machine capacity. The sum across a tree
            // is bounded by 100% because they share the same physical cores.
            totalCpu = Math.Min(Math.Max(totalCpu, 0.0), 100.0);
            return new ProcessMetrics(totalCpu, totalRam);
        }

        /// <inheritdoc />
        public string FormatCpuUsage(double cpuUsage)
        {
            double rounded = Math.Round(cpuUsage, 1, MidpointRounding.AwayFromZero);
            return $"{rounded.ToString("0.0", CultureInfo.InvariantCulture)}%";
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
        public string? ResolvePath(string? inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return null;

            inputPath = inputPath.Trim();

            // 1. Expand variables (Note: only expands variables existing in the calling process's environment)
            var expandedPath = Environment.ExpandEnvironmentVariables(inputPath);

            // 2. Ensure the path is absolute before checking the filesystem
            if (!Helper.IsAbsolute(expandedPath))
            {
                throw new InvalidOperationException($"Path '{expandedPath}' is relative. Only absolute paths are allowed.");
            }

            // 3. Normalize (removes trailing slashes, resolves ..\ segments)
            var normalizedPath = Path.GetFullPath(expandedPath);

            return normalizedPath;
        }

        /// <inheritdoc />
        public bool ValidatePath(string? path, bool isFile = true)
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
            catch (Exception ex)
            {
                Logger.Debug($"ValidatePath: could not resolve '{path}': {ex.Message}");
                return false; // ResolvePath failed (unexpanded vars or relative path)
            }
        }

        /// <inheritdoc />
        public Process? Start(ProcessStartInfo psi)
        {
            return Process.Start(psi);
        }
    }
}