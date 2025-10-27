using System;
using System.Runtime.InteropServices;

#pragma warning disable IDE0079
#pragma warning disable SYSLIB1054

namespace Servy.Service.Native
{
    public static class NativeMethods
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
        /// Constant used to indicate that the current process should attach to the parent process's console.
        /// </summary>
        public const int ATTACH_PARENT_PROCESS = -1;

        /// <summary>
        /// Attaches the calling process to the console of the specified process.
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(int processId);

        /// <summary>
        /// The UTF-8 code page identifier.
        /// </summary>
        /// <remarks>
        /// This constant corresponds to the Windows code page 65001 (<c>CP_UTF8</c>).
        /// </remarks>
        public const uint CP_UTF8 = 65001;

        /// <summary>
        /// Allocates a new console for the calling process.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the function succeeds; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// If the process already has a console, the function fails.  
        /// This function is often used by GUI applications that need to display console output at runtime.  
        /// See <see href="https://learn.microsoft.com/windows/console/allocconsole">AllocConsole (MSDN)</see>.
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        /// <summary>
        /// Sets the output code page used by the console associated with the calling process.
        /// </summary>
        /// <param name="codePageID">The identifier of the code page to set, such as <see cref="CP_UTF8"/>.</param>
        /// <returns>
        /// <see langword="true"/> if the function succeeds; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The output code page determines how characters written to the console are encoded.  
        /// See <see href="https://learn.microsoft.com/windows/console/setconsoleoutputcp">SetConsoleOutputCP (MSDN)</see>.
        /// </remarks>
        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleOutputCP(uint codePageID);

        /// <summary>
        /// Adds or removes an application-defined handler function from the list of handler functions
        /// for the calling process.
        /// </summary>
        /// <param name="handlerRoutine">
        /// A delegate to a handler function to add or remove.  
        /// Pass <see langword="null"/> to remove all handlers for the process.
        /// </param>
        /// <param name="add">
        /// <see langword="true"/> to add the handler;  
        /// <see langword="false"/> to remove it.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the operation succeeds;  
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The <c>SetConsoleCtrlHandler</c> function enables a process to handle console control signals
        /// such as <c>CTRL_C_EVENT</c> or <c>CTRL_CLOSE_EVENT</c>.  
        /// See the official documentation:
        /// <see href="https://learn.microsoft.com/windows/console/setconsolectrlhandler">SetConsoleCtrlHandler (MSDN)</see>.
        /// </remarks>
        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerRoutine handlerRoutine, bool add);

        /// <summary>
        /// Represents the method that handles console control events received by the process.
        /// </summary>
        /// <param name="ctrlType">The control event that triggered the handler.</param>
        /// <returns>
        /// <see langword="true"/> if the handler processed the event and should prevent further handlers from being called;  
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public delegate bool ConsoleCtrlHandlerRoutine(CtrlEvents ctrlType);

        /// <summary>
        /// Generates a console control event.
        /// </summary>
        /// <param name="ctrlEvent"></param>
        /// <param name="processGroupId"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern bool GenerateConsoleCtrlEvent(CtrlEvents ctrlEvent, uint processGroupId);

        /// <summary>
        /// Frees the console associated with the calling process.
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetStdHandle(int nStdHandle, IntPtr handle);

        public const int STD_OUTPUT_HANDLE = -11;
        public const int STD_ERROR_HANDLE = -12;

        /// <summary>
        /// Opens an existing local process object.
        /// </summary>
        /// <param name="desiredAccess"></param>
        /// <param name="inheritHandle"></param>
        /// <param name="processId"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern Handle OpenProcess(ProcessAccess desiredAccess, bool inheritHandle, int processId);

        /// <summary>
        /// Process access rights.
        /// </summary>
        public enum ProcessAccess : uint
        {
            QueryInformation = 0x0400,
        }

        /// <summary>
        /// Queries information about the specified process.
        /// </summary>
        /// <param name="processHandle"></param>
        /// <param name="processInformationClass"></param>
        /// <param name="processInformation"></param>
        /// <param name="processInformationLength"></param>
        /// <param name="returnLength"></param>
        /// <returns></returns>
        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            ProcessInfoClass processInformationClass,
            out ProcessBasicInformation processInformation,
            int processInformationLength,
            IntPtr returnLength = default);

        /// <summary>
        /// Process information classes for NtQueryInformationProcess.
        /// </summary>
        public enum ProcessInfoClass
        {
            ProcessBasicInformation = 0,
        }

        /// <summary>
        /// Process basic information structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct ProcessBasicInformation
        {
#pragma warning disable SA1306 // Field names should begin with lower-case letter
            private readonly IntPtr Reserved1;
            private readonly IntPtr PebBaseAddress;
            private readonly IntPtr Reserved2_1;
            private readonly IntPtr Reserved2_2;
            public readonly IntPtr UniqueProcessId;
            public readonly IntPtr InheritedFromUniqueProcessId;
#pragma warning restore SA1306 // Field names should begin with lower-case letter
        }

        /// <summary>
        /// Control events for console processes.
        /// </summary>
        public enum CtrlEvents : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6,
        }

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
        public enum JobLimits : uint
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
            public JobLimits LimitFlags;

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