using Servy.Core.Config;
using System;
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
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId, ParentProcessId FROM Win32_Process WHERE ParentProcessId={parentPid.ToString(CultureInfo.InvariantCulture)}"
                ))
                {
                    foreach (var obj in searcher.Get())
                    {
                        int childPid = Convert.ToInt32(obj["ProcessId"]);

                        if (childPid == Process.GetCurrentProcess().Id)
                            continue;

                        KillChildren(childPid); // Recursively kill grandchildren

                        using (var child = Process.GetProcessById(childPid))
                        {
                            try
                            {
                                if (!child.HasExited)
                                {
                                    child.Kill();
                                    child.WaitForExit();
                                }
                            }
                            catch
                            {
                                // Ignore if the process has already exited or access denied
                            }
                        }
                    }
                }

            }
            catch
            {
                // Ignore if parent process already exited
            }
        }

        /// <summary>
        /// Kills all processes with the specified name, including their child and parent processes.
        /// </summary>
        /// <param name="processName">The name of the process to kill. Can include or exclude ".exe".</param>
        /// <param name="killParents">Whether to kill parents as well.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public static bool KillProcessTreeAndParents(string processName, bool killParents = true)
        {
            try
            {
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                var allProcesses = Process.GetProcesses();
                var processes = allProcesses
                    .Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var proc in processes)
                    KillProcessTree(proc, allProcesses);

                if (killParents)
                {
                    foreach (var proc in processes)
                        KillParentProcesses(proc, allProcesses);
                }

                return true;
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
                Debug.WriteLine($"File not found: {filePath}");
                return true;
            }

            var handleExePath = AppConfig.GetHandleExePath();

            if (!File.Exists(handleExePath))
            {
                Debug.WriteLine($"Handle.exe not found at: {handleExePath}");
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
                        Debug.WriteLine($"Failed to kill process {procInfo.ProcessName} (PID {procInfo.ProcessId}): {ex.Message}");
                        success = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to enumerate processes using {filePath}: {ex.Message}");
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
        /// Recursively kills the specified process and all its child processes.
        /// </summary>
        /// <param name="process">The process to kill.</param>
        /// <param name="allProcesses">All currently running processes.</param>
        private static void KillProcessTree(Process process, Process[] allProcesses)
        {
            try
            {
                var children = allProcesses.Where(p =>
                {
                    try { return GetParentProcessId(p) == process.Id; }
                    catch { return false; }
                });

                foreach (var child in children)
                    KillProcessTree(child, allProcesses);

                process.Kill();
                process.WaitForExit();
            }
            catch
            {
                // Ignore if the process has already exited.
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
