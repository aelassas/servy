using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Native
{
    /// <summary>
    /// A lightweight container for process metadata captured from a native snapshot to prevent redundant operating system queries.
    /// </summary>
    public struct ProcessInfoNode
    {
        /// <summary>
        /// The numerical identifier of the parent process.
        /// </summary>
        public int ParentId;

        /// <summary>
        /// The name of the executable file.
        /// </summary>
        public string Name;
    }

    /// <summary>
    /// Provides centralized access to Toolhelp32 process snapshot generation, 
    /// preventing DRY violations across the Core and Service assemblies.
    /// </summary>
    public static class Toolhelp32Snapshot
    {
        /// <summary>
        /// Performs a single-pass iteration of the OS process table using Toolhelp32 to build both an in-memory 
        /// parent-to-children relationship map and a complete snapshot map for upward traversal.
        /// </summary>
        /// <returns>A tuple containing the metadata snapshot map and the parent-to-child relationship map.</returns>
        public static (Dictionary<int, ProcessInfoNode> Snapshot, Dictionary<int, List<int>> ByParent) BuildSnapshotAndChildMap()
        {
            var snapshotMap = new Dictionary<int, ProcessInfoNode>();
            var byParent = new Dictionary<int, List<int>>();
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

            if (snapshot == IntPtr.Zero || snapshot == INVALID_HANDLE_VALUE)
            {
                int err = Marshal.GetLastWin32Error();
                Logger.Warn($"CreateToolhelp32Snapshot failed (Win32 error {err}). Process map build will be a no-op for this invocation.");
                return (snapshotMap, byParent);
            }

            try
            {
                PROCESSENTRY32 pe32 = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                if (Process32First(snapshot, ref pe32))
                {
                    do
                    {
                        int ppid = (int)pe32.th32ParentProcessID;
                        int pid = (int)pe32.th32ProcessID;

                        // 1. Populate the upward-traversal metadata map
                        snapshotMap[pid] = new ProcessInfoNode
                        {
                            ParentId = ppid,
                            Name = pe32.szExeFile
                        };

                        // 2. Populate the downward-traversal relationship map
                        if (!byParent.TryGetValue(ppid, out var children))
                        {
                            children = new List<int>();
                            byParent[ppid] = children;
                        }
                        children.Add(pid);

                    } while (Process32Next(snapshot, ref pe32));
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return (snapshotMap, byParent);
        }
    }
}