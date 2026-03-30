using Servy.Core.Config;
using Servy.Core.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
            try
            {
                // 1. Evaluate selfPid EXACTLY ONCE at the top-level entry point
                int selfPid = Process.GetCurrentProcess().Id;

                DateTime parentStartTime;
                using (var parent = Process.GetProcessById(parentPid))
                {
                    parentStartTime = parent.StartTime;
                }

                // 2. Pass selfPid into the recursive chain
                KillChildrenInternal(parentPid, parentStartTime, selfPid);
            }
            catch
            {
                // Parent already gone, nothing to kill
            }
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
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId, CreationDate FROM Win32_Process WHERE ParentProcessId={parentPid.ToString(CultureInfo.InvariantCulture)}"
                ))
                {
                    foreach (var obj in searcher.Get())
                    {
                        int childPid = Convert.ToInt32(obj["ProcessId"]);

                        // WMI dates are strings; ManagementDateTimeConverter handles the conversion
                        DateTime childStartTime = ManagementDateTimeConverter.ToDateTime(obj["CreationDate"].ToString());

                        // SAFETY CHECK: The child must have started AFTER the parent
                        if (childStartTime < parentStartTime)
                            continue;

                        // Skip the current process to avoid a self-kill scenario
                        if (childPid == selfPid)
                            continue;

                        // Pass the child's start time and selfPid down for its own children
                        KillChildrenInternal(childPid, childStartTime, selfPid);

                        using (var child = Process.GetProcessById(childPid))
                        {
                            try
                            {
                                // Double-check StartTime via Process object for maximum accuracy 
                                // before the final kill
                                if (!child.HasExited && child.StartTime == childStartTime)
                                {
                                    child.Kill();
                                    child.WaitForExit();
                                }
                            }
                            catch
                            {
                                // Already exited or Access Denied
                            }
                        }
                    }
                }
            }
            catch
            {
                // Handle WMI or enumeration errors
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

                // Capture the process snapshot once for the entire operation
                var allProcesses = Process.GetProcesses();

                // Filter the initial targets. Use ToList() to avoid re-evaluating 
                // the LINQ expression if the underlying process state changes.
                var targetProcesses = allProcesses
                    .Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (targetProcesses.Count == 0)
                    return true;

                // 1. Kill the tree (children and the process itself)
                foreach (var proc in targetProcesses)
                {
                    KillProcessTree(proc, allProcesses);
                }

                // 2. Kill the parents if requested
                if (killParents)
                {
                    foreach (var proc in targetProcesses)
                    {
                        // Note: We pass the target proc even if it was killed in step 1.
                        // Our updated KillParentProcesses uses the cached StartTime 
                        // and PID from the 'proc' object to safely identify parents.
                        KillParentProcesses(proc, allProcesses);
                    }
                }

                return true;
            }
            catch
            {
                // General fallback to ensure the service manager doesn't crash 
                // during cleanup operations.
                return false;
            }
            finally
            {
                // Optional: Clean up process handles from the snapshot if needed, 
                // though GC handles this well for the Process class.
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
        private static void KillProcessTree(Process process, Process[] allProcesses)
        {
            try
            {
                // Get the parent's start time once to compare against all potential children
                DateTime parentStartTime = process.StartTime;

                var children = allProcesses.Where(p =>
                {
                    try
                    {
                        // SAFETY CHECK: Identity = PID + StartTime
                        // 1. Does the PID match?
                        // 2. Did this 'child' start AFTER the parent? 
                        // If it started before, it's a recycled PID from a previous process.
                        return GetParentProcessId(p) == process.Id && p.StartTime >= parentStartTime;
                    }
                    catch
                    {
                        return false;
                    }
                });

                foreach (var child in children)
                {
                    KillProcessTree(child, allProcesses);
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(10_000);
                }
            }
            catch
            {
                // Ignore if the process has already exited or access is denied
            }
        }

        /// <summary>
        /// Recursively kills the parent processes of the specified process.
        /// </summary>
        /// <param name="process">The process whose parents to kill.</param>
        /// <param name="allProcesses">All currently running processes.</param>
        private static void KillParentProcesses(Process process, Process[] allProcesses)
        {
            try
            {
                int parentId = GetParentProcessId(process);
                if (parentId <= 0) return;

                var parent = allProcesses.FirstOrDefault(p => p.Id == parentId);
                if (parent == null) return;

                // SAFETY CHECK: PID REUSE PROTECTION
                // Verify the parent's StartTime is earlier than the child's StartTime.
                // If the "parent" started after the child, the PID has been recycled 
                // and this is an unrelated process.
                try
                {
                    if (parent.StartTime >= process.StartTime)
                    {
                        return;
                    }
                }
                catch
                {
                    // If we can't access StartTime (Access Denied), it's safer to 
                    // abort the parent-kill chain to avoid collateral damage.
                    return;
                }

                KillParentProcesses(parent, allProcesses);

                parent.Kill();
                parent.WaitForExit();
            }
            catch
            {
                // Ignore if the process has already exited.
            }
        }
    }
}
