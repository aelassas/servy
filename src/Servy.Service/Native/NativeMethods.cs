using System;
using System.Runtime.InteropServices;

#pragma warning disable IDE0079
#pragma warning disable SYSLIB1054

namespace Servy.Service.Native
{
    internal static class NativeMethods
    {
        /// <summary>
        /// Creates or opens a job object.
        /// A job object allows groups of processes to be managed as a unit.
        /// </summary>
        /// <param name="lpJobAttributes">A pointer to a SECURITY_ATTRIBUTES structure. If IntPtr.Zero, the handle cannot be inherited.</param>
        /// <param name="lpName">The name of the job object. Can be null for an unnamed job object.</param>
        /// <returns>
        /// If the function succeeds, returns a handle to the job object.
        /// Otherwise, returns IntPtr.Zero.
        /// </returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        /// <summary>
        /// Sets limits or information for a job object.
        /// </summary>
        /// <param name="hJob">Handle to the job object.</param>
        /// <param name="infoClass">Specifies the type of information to set.</param>
        /// <param name="lpJobObjectInfo">Pointer to a structure containing the information to set.</param>
        /// <param name="cbJobObjectInfoLength">Size of the structure pointed to by lpJobObjectInfo, in bytes.</param>
        /// <returns>True if successful; otherwise false.</returns>
        [DllImport("kernel32.dll")]
        public static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoClass infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        /// <summary>
        /// Assigns a process to an existing job object.
        /// </summary>
        /// <param name="hJob">Handle to the job object.</param>
        /// <param name="hProcess">Handle to the process to assign.</param>
        /// <returns>True if successful; otherwise false.</returns>
        [DllImport("kernel32.dll")]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        /// <summary>
        /// Closes an open object handle.
        /// </summary>
        /// <param name="hObject">A valid handle to an open object.</param>
        /// <returns>True if successful; otherwise false.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Specifies the type of job object information to query or set.
        /// </summary>
        public enum JobObjectInfoClass
        {
            /// <summary>
            /// Extended limit information for the job object.
            /// </summary>
            JobObjectExtendedLimitInformation = 9
        }

        /// <summary>
        /// Flags that control the behavior of a job object’s limits.
        /// </summary>
        [Flags]
        public enum Limit : uint
        {
            /// <summary>
            /// When this flag is set, all processes associated with the job are terminated when the last handle to the job is closed.
            /// </summary>
            KillOnJobClose = 0x00002000
        }

        /// <summary>
        /// Contains extended limit information for a job object.
        /// Combines basic limits, IO accounting, and memory limits.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct JobobjectExtendedLimitInformation
        {
            /// <summary>
            /// Basic limit information for the job.
            /// </summary>
            public JobobjectBasicLimitInformation BasicLimitInformation;

            /// <summary>
            /// IO accounting information for the job.
            /// </summary>
            public IoCounters IoInfo;

            /// <summary>
            /// Maximum amount of memory the job's processes can commit.
            /// </summary>
            public UIntPtr ProcessMemoryLimit;

            /// <summary>
            /// Maximum amount of memory the job can commit.
            /// </summary>
            public UIntPtr JobMemoryLimit;

            /// <summary>
            /// Peak memory used by any process in the job.
            /// </summary>
            public UIntPtr PeakProcessMemoryUsed;

            /// <summary>
            /// Peak memory used by the job.
            /// </summary>
            public UIntPtr PeakJobMemoryUsed;
        }

        /// <summary>
        /// Contains basic limit information for a job object.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct JobobjectBasicLimitInformation
        {
            /// <summary>
            /// Per-process user-mode execution time limit, in 100-nanosecond ticks.
            /// </summary>
            public Int64 PerProcessUserTimeLimit;

            /// <summary>
            /// Per-job user-mode execution time limit, in 100-nanosecond ticks.
            /// </summary>
            public Int64 PerJobUserTimeLimit;

            /// <summary>
            /// Flags that control the job limits.
            /// </summary>
            public Limit LimitFlags;

            /// <summary>
            /// Minimum working set size, in bytes.
            /// </summary>
            public UIntPtr MinimumWorkingSetSize;

            /// <summary>
            /// Maximum working set size, in bytes.
            /// </summary>
            public UIntPtr MaximumWorkingSetSize;

            /// <summary>
            /// Maximum number of active processes in the job.
            /// </summary>
            public UInt32 ActiveProcessLimit;

            /// <summary>
            /// Processor affinity for processes in the job.
            /// </summary>
            public Int64 Affinity;

            /// <summary>
            /// Priority class for processes in the job.
            /// </summary>
            public UInt32 PriorityClass;

            /// <summary>
            /// Scheduling class for processes in the job.
            /// </summary>
            public UInt32 SchedulingClass;
        }

        /// <summary>
        /// Contains IO accounting information for a job object.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IoCounters
        {
            /// <summary>
            /// Number of read operations performed.
            /// </summary>
            public UInt64 ReadOperationCount;

            /// <summary>
            /// Number of write operations performed.
            /// </summary>
            public UInt64 WriteOperationCount;

            /// <summary>
            /// Number of other operations performed.
            /// </summary>
            public UInt64 OtherOperationCount;

            /// <summary>
            /// Number of bytes read.
            /// </summary>
            public UInt64 ReadTransferCount;

            /// <summary>
            /// Number of bytes written.
            /// </summary>
            public UInt64 WriteTransferCount;

            /// <summary>
            /// Number of bytes transferred in other operations.
            /// </summary>
            public UInt64 OtherTransferCount;
        }
    }
}

#pragma warning restore SYSLIB1054
#pragma warning restore IDE0079