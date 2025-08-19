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
        /// <summary>
        /// Represents the basic information of a process used for querying the parent PID via Win32 API.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
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
            ref PROCESS_BASIC_INFORMATION processInformation,
            uint processInformationLength,
            out uint returnLength
        );

        /// <summary>
        /// Retrieves the parent process ID of a given <see cref="Process"/>.
        /// </summary>
        /// <param name="process">The process to query.</param>
        /// <returns>The parent process ID.</returns>
        private static int GetParentProcessId(Process process)
        {
            PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
            uint retLen;
            NtQueryInformationProcess(process.Handle, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out retLen);
            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }

        /// <summary>
        /// Kills all processes with the specified name, including their child and parent processes.
        /// </summary>
        /// <param name="processName">The name of the process to kill. Can include or exclude ".exe".</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public static bool KillServyProcessTree(string processName)
        {
            try
            {
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                var allProcesses = Process.GetProcesses();
                var servyProcesses = allProcesses
                    .Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var proc in servyProcesses)
                    KillProcessTree(proc, allProcesses);

                foreach (var proc in servyProcesses)
                    KillParentProcesses(proc, allProcesses);

                return true;
            }
            catch
            {
                return false;
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
