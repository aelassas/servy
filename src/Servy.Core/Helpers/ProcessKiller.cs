using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides methods to recursively kill a process tree by process name.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ProcessKiller
    {
        #region Win32 API

        /// <summary>
        /// Represents the basic information of a process used for querying the parent PID via Win32 API.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessBasicInformation
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref ProcessBasicInformation processInformation,
            uint processInformationLength,
            out uint returnLength
        );

        #endregion

        /// <summary>
        /// Recursively kills all child processes of a specified parent process.
        /// </summary>
        /// <param name="parentPid">The process ID of the parent whose children should be terminated.</param>
        /// <remarks>
        /// This method uses WMI (<c>Win32_Process</c>) to enumerate processes where
        /// <c>ParentProcessId</c> matches the given <paramref name="parentPid"/>.
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
            int selfPid = Process.GetCurrentProcess().Id;

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
            catch { /* Parent already exited, proceed with MinValue */ }

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
            try
            {
                // Optimization: Only select what we need. WMI is expensive.
                string query = $"SELECT ProcessId, CreationDate FROM Win32_Process WHERE ParentProcessId={parentPid}";

                using (var searcher = new ManagementObjectSearcher(query))
                using (var results = searcher.Get())
                {
                    foreach (var obj in results)
                    {
                        int childPid = Convert.ToInt32(obj["ProcessId"]);
                        if (childPid == selfPid) continue;

                        // Safely parse WMI date
                        DateTime childStartTime = DateTime.MinValue;
                        try
                        {
                            childStartTime = ManagementDateTimeConverter.ToDateTime(obj["CreationDate"].ToString());
                        }
                        catch { /* Ignore malformed dates */ }

                        // Validation: Only skip if we are SURE it's a PID reuse (child older than parent)
                        // We add a 2-second buffer for clock skew between WMI and KERNEL32
                        if (parentStartTime != DateTime.MinValue && childStartTime < parentStartTime.AddSeconds(-2))
                        {
                            continue;
                        }

                        // Recursion: Kill grandchildren first (Leaf-to-Root)
                        KillChildrenInternal(childPid, childStartTime, selfPid);

                        // Final Kill
                        try
                        {
                            using (var child = Process.GetProcessById(childPid))
                            {
                                if (!child.HasExited)
                                {
                                    child.Kill();
                                    // Do not wait indefinitely; 2s is plenty of time
                                    child.WaitForExit(2000);
                                }
                            }
                        }
                        catch { /* Process gone or Access Denied */ }
                    }
                }
            }
            catch (ManagementException) { /* WMI Service is stopping or busy */ }
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
        /// Kills all processes that currently hold a handle to the specified file.
        /// </summary>
        /// <param name="filePath">Full path to the file.</param>
        /// <returns><c>true</c> if all processes were successfully killed; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// This method requires Sysinternals Handle.exe or Handle64.exe to be available
        /// and assumes its path is in <c>C:\Program Files\Sysinternals\handle64.exe</c> by default.
        /// </remarks>
        public static bool KillProcessesUsingFile(string filePath)
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
                var processes = HandleHelper.GetProcessesUsingFile(handleExePath, filePath);

                foreach (var procInfo in processes)
                {
                    if (string.IsNullOrEmpty(procInfo.ProcessName))
                        continue; // skip null or empty names

                    try
                    {
                        KillProcessTreeAndParents(procInfo.ProcessName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to kill process {procInfo.ProcessName} (PID {procInfo.ProcessId}).", ex);
                        success = false;
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
                    process.WaitForExit(10_000);
                }
            }
            catch
            {
                // We catch all exceptions to ensure that a failure in killing one process does not prevent attempts to kill others.
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
                catch (System.ComponentModel.Win32Exception) { return; }

                // Move up first
                KillParentProcesses(parent, allProcesses, protectedPids);

                if (!parent.HasExited)
                {
                    parent.Kill();
                    parent.WaitForExit(5000);
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
                var current = Process.GetCurrentProcess();
                ancestors.Add(current.Id);

                int parentId = GetParentProcessId(current);
                while (parentId > 4)
                {
                    ancestors.Add(parentId);
                    try
                    {
                        using (var parent = Process.GetProcessById(parentId))
                        {
                            parentId = GetParentProcessId(parent);
                        }
                    }
                    catch { break; }
                }
            }
            catch
            {
                // In the unlikely event that we cannot get the current process or its parents, we return an empty set, which means no protection.
            }
            return ancestors;
        }

    }
}
