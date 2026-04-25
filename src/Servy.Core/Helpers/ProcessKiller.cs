using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides methods to recursively kill a process tree by process name.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ProcessKiller
    {
        /// <summary>
        /// Safelist of processes that should NEVER be targeted by Servy's auto-killer
        /// </summary>
        private static readonly HashSet<string> CriticalSystemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "idle", "csrss", "lsass", "wininit", "services",
            "winlogon", "smss", "svchost", "explorer", "runtimebroker"
        };

        /// <summary>
        /// Recursively kills all child processes of a specified parent process.
        /// </summary>
        /// <param name="parentPid">The process ID of the parent whose children should be terminated.</param>
        /// <remarks>
        /// This method enumerates processes where <c>ParentProcessId</c> matches 
        /// the given <paramref name="parentPid"/>.
        /// 
        /// It then recursively calls itself to ensure that grandchildren and deeper
        /// descendants are also terminated before finally killing the child itself.
        /// 
        /// Exceptions such as access denied or processes that have already exited are
        /// caught and ignored to allow cleanup to continue without interruption.
        /// </remarks>
        public static void KillChildren(int parentPid)
        {
            // 1. Get self PID to prevent suicide
            int selfPid;
            using (var current = Process.GetCurrentProcess())
            {
                selfPid = current.Id;
            }

            // 2. Try to get parent start time, but DON'T FAIL if the parent is already dead.
            // If the parent is dead, we use a fallback time (DateTime.MinValue) 
            // to allow the first level of children to be found.
            DateTime parentStartTime = DateTime.MinValue;
            try
            {
                using (var parent = Process.GetProcessById(parentPid))
                {
                    parentStartTime = parent.StartTime;
                }
            }
            catch (InvalidOperationException) { /* Parent already exited */ }
            catch (Win32Exception) { /* Access denied */ }
            catch (Exception ex) { Logger.Warn($"Unexpected error getting parent start time: {ex.Message}"); }

            KillChildrenInternal(parentPid, parentStartTime, selfPid);
        }

        /// <summary>
        /// Internal recursive method that terminates child processes while validating 
        /// their identity against the parent's start time to prevent PID-reuse accidents.
        /// </summary>
        /// <param name="parentPid">The process ID of the parent currently being processed.</param>
        /// <param name="parentStartTime">The start time of the parent for identity validation.</param>
        /// <param name="selfPid">The PID of the current process to prevent accidental self-termination.</param>
        private static void KillChildrenInternal(int parentPid, DateTime parentStartTime, int selfPid)
        {
            // 1. Collect PROCESS objects instead of just PIDs. 
            // Opening the handle early locks the PID from being reused for a *new* process.
            var childProcesses = new List<Process>();

            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
                return;

            try
            {
                PROCESSENTRY32 pe32 = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };

                if (Process32First(snapshot, ref pe32))
                {
                    do
                    {
                        if (pe32.th32ParentProcessID == parentPid && pe32.th32ProcessID != selfPid)
                        {
                            try
                            {
                                // Get the process object immediately. This opens a handle.
                                var proc = Process.GetProcessById((int)pe32.th32ProcessID);

                                // Identity Validation: Does this handle belong to a process 
                                // created after (or roughly at) our parent's start time?
                                if (parentStartTime == DateTime.MinValue || proc.StartTime >= parentStartTime.AddSeconds(-2))
                                {
                                    childProcesses.Add(proc);
                                }
                                else
                                {
                                    proc.Dispose(); // Not the process we're looking for.
                                }
                            }
                            catch (Exception ex) { Logger.Debug($"Could not inspect/kill child process.", ex); }
                        }
                    } while (Process32Next(snapshot, ref pe32));
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            // 2. Process the identified tree
            foreach (var child in childProcesses)
            {
                try
                {
                    // Recursion: Pass the verified start time down.
                    // We use the child's PID and its start time to lock the next level.
                    KillChildrenInternal(child.Id, child.StartTime, selfPid);

                    if (!child.HasExited)
                    {
                        child.Kill();
                        child.WaitForExit(AppConfig.KillChildWaitMs);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to kill child process {child.Id}: {ex.Message}");
                }
                finally
                {
                    child.Dispose(); // Release the handle so the PID can finally be recycled by Windows.
                }
            }
        }

        /// <summary>
        /// Kills all processes with the specified name, including their child and parent processes.
        /// </summary>
        /// <param name="processName">The name of the process to kill. Can include or exclude ".exe".</param>
        /// <param name="killParents">Whether to kill parents as well.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        /// <remarks>
        /// This method captures a snapshot of all running processes to ensure consistency 
        /// during the recursive tree walk. It handles the ".exe" extension automatically.
        /// </remarks>
        public static bool KillProcessTreeAndParents(string processName, bool killParents = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processName))
                    return false;

                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                var allProcesses = Process.GetProcesses();
                // CRITICAL: Identify the current process and its ancestors to avoid IDE suicide
                var protectedPids = GetAncestorPids();

                try
                {
                    var targetProcesses = allProcesses
                        .Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                        .Where(p => !protectedPids.Contains(p.Id)) // Never kill protected processes
                        .ToList();

                    if (targetProcesses.Count == 0)
                        return true;

                    foreach (var proc in targetProcesses)
                    {
                        KillProcessTree(proc, allProcesses, protectedPids);
                    }

                    if (killParents)
                    {
                        foreach (var proc in targetProcesses)
                        {
                            KillParentProcesses(proc, allProcesses, protectedPids);
                        }
                    }

                    return true;
                }
                finally
                {
                    foreach (var p in allProcesses) p.Dispose();
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kills a specific process by PID, including its entire child tree and (optionally) its ancestors.
        /// </summary>
        /// <param name="pid">The specific Process ID to target.</param>
        /// <param name="killParents">Whether to traverse up the tree and kill parent processes.</param>
        /// <returns>True if the process was found and termination was attempted; otherwise, false.</returns>
        public static bool KillProcessTreeAndParents(int pid, bool killParents = true)
        {
            if (pid <= 0) return false;

            // CRITICAL: Protect the current process, IDE, and critical system components
            var protectedPids = GetAncestorPids();
            if (protectedPids.Contains(pid))
            {
                Logger.Warn($"Execution blocked: Attempted to kill protected process (PID {pid}).");
                return false;
            }

            var allProcesses = Process.GetProcesses();
            try
            {
                var target = allProcesses.FirstOrDefault(p => p.Id == pid);
                if (target == null) return true; // Already gone

                // 1. Kill the children first (Bottom-up)
                KillProcessTree(target, allProcesses, protectedPids);

                // 2. Kill the parents if requested
                if (killParents)
                {
                    KillParentProcesses(target, allProcesses, protectedPids);
                }

                // 3. Finally, kill the target itself if it survived the tree walk
                try
                {
                    if (!target.HasExited)
                    {
                        target.Kill();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Final termination failed for PID {pid}.", ex);
                    return false;
                }

                return true;
            }
            finally
            {
                foreach (var p in allProcesses) p.Dispose();
            }
        }

        /// <summary>
        /// Kills all processes that currently hold a handle to the specified file.
        /// </summary>
        /// <param name="processHelper">An instance of <see cref="IProcessHelper"/> for process operations.</param>
        /// <param name="filePath">Full path to the file.</param>
        /// <returns><c>true</c> if all processes were successfully killed; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// This method requires Sysinternals Handle.exe or Handle64.exe to be available
        /// and assumes its path is in <c>C:\Program Files\Sysinternals\handle64.exe</c> by default.
        /// </remarks>
        public static bool KillProcessesUsingFile(IProcessHelper processHelper, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"File not found: {filePath}");
                return true;
            }

            var handleExePath = AppConfig.GetHandleExePath();
            if (!File.Exists(handleExePath))
            {
                Logger.Error($"Handle.exe not found at: {handleExePath}");
                return false;
            }

            bool success = true;

            try
            {
                var processes = HandleHelper.GetProcessesUsingFile(processHelper, handleExePath, filePath);

                foreach (var procInfo in processes)
                {
                    // 1. Validate procInfo
                    if (procInfo.ProcessId <= 0) continue;

                    // 2. Secondary Safelist Guardrail
                    // Even if a system process locks a file, killing it is usually more 
                    // destructive than failing the file operation.
                    if (!string.IsNullOrEmpty(procInfo.ProcessName) &&
                        CriticalSystemProcesses.Contains(procInfo.ProcessName))
                    {
                        Logger.Warn($"Skipping kill request for critical system process: {procInfo.ProcessName} (PID {procInfo.ProcessId})");
                        success = false;
                        continue;
                    }

                    // 3. Surgical Kill by PID
                    // Ensure KillProcessTreeAndParents has an overload that accepts 'int pid'
                    if (!KillProcessTreeAndParents(procInfo.ProcessId))
                    {
                        Logger.Error($"Failed to kill process {procInfo.ProcessName} (PID {procInfo.ProcessId}).");
                        success = false;
                    }
                    else
                    {
                        Logger.Info($"Successfully terminated process tree for PID {procInfo.ProcessId} ({procInfo.ProcessName}) to release lock on {filePath}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enumerate processes using {filePath}.", ex);
                return false;
            }

            return success;
        }

        /// <summary>
        /// Retrieves the parent process ID of a given <see cref="Process"/>.
        /// </summary>
        /// <param name="process">The process to query.</param>
        /// <returns>The parent process ID.</returns>
        private static int GetParentProcessId(Process process)
        {
            try
            {
                ProcessBasicInformation pbi = new ProcessBasicInformation();
                uint retLen;
                int status = NtQueryInformationProcess(process.Handle, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out retLen);

                if (status != 0) // STATUS_SUCCESS == 0
                    return -1;   // could not query parent

                return pbi.InheritedFromUniqueProcessId.ToInt32();
            }
            catch
            {
                return -1; // safer fallback
            }
        }

        /// <summary>
        /// Recursively kills the specified process and all its child processes, 
        /// validating the parent-child relationship using process start times.
        /// </summary>
        /// <param name="process">The process to kill.</param>
        /// <param name="allProcesses">All currently running processes.</param>
        private static void KillProcessTree(Process process, Process[] allProcesses, HashSet<int> protectedPids)
        {
            try
            {
                if (protectedPids.Contains(process.Id)) return;

                int parentId = process.Id;
                DateTime parentStartTime = process.StartTime;

                var children = allProcesses.Where(p =>
                {
                    try
                    {
                        return GetParentProcessId(p) == parentId &&
                               p.StartTime >= parentStartTime.AddSeconds(-2);
                    }
                    catch { return false; }
                }).ToArray();

                foreach (var child in children)
                {
                    KillProcessTree(child, allProcesses, protectedPids);
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(AppConfig.KillTreeWaitMs);
                }
            }
            catch (InvalidOperationException) { /* Process already exited */ }
            catch (Win32Exception) { /* Access denied */ }
            catch (Exception ex)
            {
                Logger.Warn($"Unexpected error killing process tree: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively kills the parent processes of the specified process.
        /// </summary>
        /// <param name="process">The process whose parents to kill.</param>
        /// <param name="allProcesses">All currently running processes.</param>
        private static void KillParentProcesses(Process process, Process[] allProcesses, HashSet<int> protectedPids)
        {
            try
            {
                int parentId = GetParentProcessId(process);

                // Stop at System/Idle or if the parent is part of the current execution chain
                if (parentId <= 4 || protectedPids.Contains(parentId)) return;

                var parent = allProcesses.FirstOrDefault(p => p.Id == parentId);
                if (parent == null) return;

                try
                {
                    if (parent.StartTime > process.StartTime.AddSeconds(2)) return;
                }
                catch (Win32Exception) { return; }

                // Move up first
                KillParentProcesses(parent, allProcesses, protectedPids);

                if (!parent.HasExited)
                {
                    parent.Kill();
                    parent.WaitForExit(AppConfig.KillParentWaitMs);
                }
            }
            catch
            {
                // We catch all exceptions to ensure that a failure in killing one parent does not prevent attempts to kill others.
            }
        }

        /// <summary>
        /// Gets the PID of the current process and all its ancestors.
        /// </summary>
        private static HashSet<int> GetAncestorPids()
        {
            var ancestors = new HashSet<int>();
            try
            {
                int currentPid;
                int parentId;

                // 1. Get current PID and dispose immediately
                using (var current = Process.GetCurrentProcess())
                {
                    currentPid = current.Id;
                    ancestors.Add(currentPid);
                    parentId = GetParentProcessId(current);
                }

                // 2. Walk up the tree
                // Stop at System (PID 4) or if we hit a loop/error
                while (parentId > 4)
                {
                    if (!ancestors.Add(parentId)) break; // Prevent infinite loops

                    try
                    {
                        using (var parent = Process.GetProcessById(parentId))
                        {
                            parentId = GetParentProcessId(parent);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Parent process no longer exists
                        break;
                    }
                    catch (Win32Exception)
                    {
                        // Access denied to the parent process
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not fully resolve ancestor tree: {ex.Message}");
            }
            return ancestors;
        }

    }
}
