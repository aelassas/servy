using Servy.Core.Config;
using Servy.Core.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            catch (System.ComponentModel.Win32Exception) { /* Access denied */ }
            catch (Exception ex) { Logger.Warn($"Unexpected error getting parent start time: {ex.Message}"); }

            KillChildrenInternal(parentPid, parentStartTime, selfPid);
        }

        /// <summary>
        /// Takes a snapshot of the specified processes, as well as the heaps, modules, and threads used by these processes.
        /// </summary>
        /// <param name="dwFlags">The portions of the system to be included in the snapshot (e.g., TH32CS_SNAPPROCESS).</param>
        /// <param name="th32ProcessID">The process identifier of the process to be included in the snapshot. Use 0 for the current process.</param>
        /// <returns>An open handle to the specified snapshot if successful; otherwise, INVALID_HANDLE_VALUE (-1).</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        /// <summary>
        /// Retrieves information about the first process encountered in a system snapshot.
        /// </summary>
        /// <param name="hSnapshot">A handle to the snapshot returned by a previous call to CreateToolhelp32Snapshot.</param>
        /// <param name="lppe">A pointer to a <see cref="PROCESSENTRY32"/> structure.</param>
        /// <returns>True if the first entry of the process list has been copied to the buffer; otherwise, false.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>
        /// Retrieves information about the next process recorded in a system snapshot.
        /// </summary>
        /// <param name="hSnapshot">A handle to the snapshot returned by a previous call to CreateToolhelp32Snapshot.</param>
        /// <param name="lppe">A pointer to a <see cref="PROCESSENTRY32"/> structure.</param>
        /// <returns>True if the next entry of the process list has been copied to the buffer; otherwise, false.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>
        /// Closes an open object handle.
        /// </summary>
        /// <param name="hObject">A valid handle to an open object.</param>
        /// <returns>True if the function succeeds; otherwise, false.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Describes an entry from a list of the processes residing in the system address space when a snapshot was taken.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            /// <summary>The size of the structure, in bytes. Before calling Process32First, set this to Marshal.SizeOf(typeof(PROCESSENTRY32)).</summary>
            public uint dwSize;
            /// <summary>This member is no longer used and is always set to zero.</summary>
            public uint cntUsage;
            /// <summary>The process identifier (PID).</summary>
            public uint th32ProcessID;
            /// <summary>This member is no longer used and is always set to zero.</summary>
            public IntPtr th32DefaultHeapID;
            /// <summary>This member is no longer used and is always set to zero.</summary>
            public uint th32ModuleID;
            /// <summary>The number of execution threads started by the process.</summary>
            public uint cntThreads;
            /// <summary>The identifier of the process that created this process (its parent process).</summary>
            public uint th32ParentProcessID;
            /// <summary>The base priority of any threads created by this process.</summary>
            public int pcPriClassBase;
            /// <summary>This member is no longer used and is always set to zero.</summary>
            public uint dwFlags;
            /// <summary>The name of the executable file for the process.</summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        /// <summary>
        /// Includes all processes in the system in the snapshot.
        /// </summary>
        private const uint TH32CS_SNAPPROCESS = 0x00000002;

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
                            catch { /* Process already exited or access denied */ }
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
                        child.WaitForExit(2000);
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
            catch (InvalidOperationException) { /* Process already exited */ }
            catch (System.ComponentModel.Win32Exception) { /* Access denied */ }
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
