using Servy.Service.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Format extensions for processes.
    /// </summary>
    internal static class ProcessExtensions
    {
        /// <summary>
        /// Formats the process as "ProcessName (Id)".
        /// </summary>
        /// <param name="process">Process.</param>
        /// <returns>Process info.</returns>
        internal static string Format(this Process process)
        {
            try
            {
                return $"{process.ProcessName} ({process.Id})";
            }
            catch (InvalidOperationException)
            {
                return $"({process.Id})";
            }
        }

        /// <summary>
        /// Gets the child processes of the specified process.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <returns>Children.</returns>
        internal static unsafe List<(Process Process, Handle Handle)> GetChildren(this Process process)
        {
            var startTime = process.StartTime;
            int processId = process.Id;

            var children = new List<(Process Process, Handle Handle)>();

            foreach (var other in Process.GetProcesses())
            {
                var handle = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.QueryInformation, false, other.Id);
                if (handle == IntPtr.Zero)
                {
                    goto Next;
                }

                try
                {
                    if (other.StartTime <= startTime)
                    {
                        goto Next;
                    }
                }
                catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                {
                    goto Next;
                }

                if (NativeMethods.NtQueryInformationProcess(
                    handle,
                    NativeMethods.PROCESSINFOCLASS.ProcessBasicInformation,
                    out var information,
                    sizeof(NativeMethods.PROCESS_BASIC_INFORMATION)) != 0)
                {
                    goto Next;
                }

                if ((int)information.InheritedFromUniqueProcessId == processId)
                {
                    // debug
                    Debug.WriteLine($"Found child process '{other.Format()}'.");
                    children.Add((other, handle));
                    continue;
                }

            Next:
                other.Dispose();
                handle.Dispose();
            }

            return children;
        }

    }
}
