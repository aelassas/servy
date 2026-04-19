using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable IDE0079
#pragma warning disable SYSLIB1054

namespace Servy.Core.Native
{
    /// <summary>
    /// Contains native methods for low-level Windows service operations.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static partial class NativeMethods
    {
        /// <summary>
        /// Describes a Windows service description string used in service configuration.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceDescription
        {
            /// <summary>
            /// A pointer to a Unicode string containing the service description.
            /// </summary>
            public IntPtr lpDescription;
        }

        /// <summary>
        /// Contains configuration information for a service's delayed auto-start setting.
        /// </summary>
        /// <remarks>
        /// This structure is used with the <c>ChangeServiceConfig2</c> function when the
        /// <c>dwInfoLevel</c> parameter is set to <c>SERVICE_CONFIG_DELAYED_AUTO_START_INFO</c>.
        /// It allows marking an auto-start service to start automatically after a delay,
        /// improving system boot performance.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceDelayedAutoStartInfo
        {
            /// <summary>
            /// Indicates whether the service should use delayed auto-start.
            /// </summary>
            /// <value>
            /// <see langword="true"/> to enable delayed auto-start;
            /// <see langword="false"/> to disable it.
            /// </value>
            [MarshalAs(UnmanagedType.Bool)]
            public bool fDelayedAutostart;
        }

        /// <summary>
        /// Contains the pre-shutdown timeout setting for a service.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServicePreShutdownInfo
        {
            /// <summary>
            /// The time-out value, in milliseconds.
            /// </summary>
            public uint dwPreshutdownTimeout;
        }

        /// <summary>Permission to query the status of a service.</summary>
        public const int SERVICE_QUERY_STATUS = 0x0004;

        /// <summary>Specifies the service is started manually.</summary>
        public const int SERVICE_DEMAND_START = 0x00000003;

        /// <summary>Indicates that the service configuration should remain unchanged.</summary>
        public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

        /// <summary>Control code to stop a service.</summary>
        public const int SERVICE_CONTROL_STOP = 0x00000001;

        /// <summary>
        /// Configuration level identifier for setting or querying delayed auto-start behavior
        /// via the <c>ChangeServiceConfig2</c> or <c>QueryServiceConfig2</c> functions.
        /// </summary>
        public const int SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 0x00000003;

        /// <summary>
        /// Service configuration information level for retrieving or changing the service description.
        /// </summary>
        public const int SERVICE_CONFIG_DESCRIPTION = 1;

        /// <summary>
        /// Contains configuration information for an installed service.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct QUERY_SERVICE_CONFIG
        {
            /// <summary>The type of service.</summary>
            public uint dwServiceType;
            /// <summary>The service start options.</summary>
            public uint dwStartType;
            /// <summary>The severity of the error, and action taken, if this service fails to start.</summary>
            public uint dwErrorControl;
            /// <summary>The fully qualified path to the service binary file.</summary>
            public IntPtr lpBinaryPathName;
            /// <summary>The name of the load ordering group to which this service belongs.</summary>
            public IntPtr lpLoadOrderGroup;
            /// <summary>A unique tag value for this service in the group specified by the lpLoadOrderGroup parameter.</summary>
            public uint dwTagId;
            /// <summary>A pointer to an array of null-separated names of services or load ordering groups that must start before this service.</summary>
            public IntPtr lpDependencies;
            /// <summary>The name of the account under which the service should run (e.g., "LocalSystem" or "NT AUTHORITY\NetworkService").</summary>
            public IntPtr lpServiceStartName;
            /// <summary>The display name to be used by service control programs to identify the service.</summary>
            public IntPtr lpDisplayName;
        }

        /// <summary>
        /// Represents the current status of a Windows service.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public int dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool QueryServiceConfig(
            IntPtr hService,
            IntPtr lpServiceConfig,
            int cbBufSize,
            out int pcbBytesNeeded);

        /// <summary>
        /// Retrieves optional configuration information for a specified service.
        /// </summary>
        /// <param name="hService">
        /// A handle to the service. This handle is obtained by calling <see cref="OpenService"/> 
        /// or <see cref="CreateService"/> and must have the <c>SERVICE_QUERY_CONFIG</c> access right.
        /// </param>
        /// <param name="dwInfoLevel">
        /// The configuration information level.  
        /// Use <c>SERVICE_CONFIG_DELAYED_AUTO_START_INFO</c> to retrieve delayed auto-start information.
        /// </param>
        /// <param name="lpBuffer">
        /// A reference to a buffer that receives the configuration information structure, 
        /// such as <see cref="ServiceDelayedAutoStartInfo"/>.
        /// </param>
        /// <param name="cbBufSize">
        /// The size of the buffer pointed to by <paramref name="lpBuffer"/>, in bytes.
        /// </param>
        /// <param name="pcbBytesNeeded">
        /// On output, receives the number of bytes required if the buffer is too small.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the function succeeds; otherwise, <see langword="false"/>.  
        /// Call <see cref="Marshal.GetLastWin32Error"/> to obtain the error code.
        /// </returns>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryServiceConfig2(
            IntPtr hService,
            uint dwInfoLevel,
            ref ServiceDelayedAutoStartInfo lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool QueryServiceConfig2(
            IntPtr hService,
            uint dwInfoLevel,
            IntPtr lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        /// <summary>
        /// Grants the right to enumerate all services in the specified service control manager (SCM) database.
        /// </summary>
        public const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;

        /// <summary>
        /// Grants the right to query the configuration of a service.
        /// Required for calls to functions such as <see cref="QueryServiceConfig"/> or <see cref="QueryServiceConfig2"/>.
        /// </summary>
        public const uint SERVICE_QUERY_CONFIG = 0x0001;

        /// <summary>
        /// Opens a connection to the service control manager.
        /// </summary>
        /// <param name="machineName">The name of the target computer.</param>
        /// <param name="databaseName">The service control manager database name.</param>
        /// <param name="dwAccess">The desired access rights.</param>
        /// <returns>A handle to the service control manager database.</returns>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

        /// <summary>
        /// Creates a service object and adds it to the specified service control manager database.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateService(
          IntPtr hSCManager,
          string lpServiceName,
          string lpDisplayName,
          uint dwDesiredAccess,
          uint dwServiceType,
          uint dwStartType,
          uint dwErrorControl,
          string lpBinaryPathName,
          string lpLoadOrderGroup,
          IntPtr lpdwTagId,
          string lpDependencies,
          string lpServiceStartName,
          string lpPassword);

        /// <summary>
        /// Opens an existing service.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        /// <summary>
        /// Deletes a service from the service control manager database.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DeleteService(IntPtr hService);

        /// <summary>
        /// Closes a handle to a service or the service control manager.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);

        /// <summary>
        /// Sends a control code to a service.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool ControlService(IntPtr hService, int dwControl, ref ServiceStatus lpServiceStatus);

        /// <summary>
        /// Changes the configuration parameters of a service.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig(
            IntPtr hService,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName);

        /// <summary>
        /// Changes the optional configuration parameters of a service (e.g. description).
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              IntPtr hService,
              int dwInfoLevel,
              ref ServiceDescription lpInfo);

        /// <summary>
        /// Changes the optional configuration parameters of a service (e.g. DelayedAutostart).
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              IntPtr hService,
              int dwInfoLevel,
              ref ServiceDelayedAutoStartInfo lpInfo);

        /// <summary>
        /// Changes the optional configuration parameters of a service (e.g. pre-shutdown timeout).
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              IntPtr hService,
              int dwInfoLevel,
              IntPtr lpInfo);

        /// <summary>
        /// Attempts to log a user on to the local computer using the specified credentials.
        /// </summary>
        /// <param name="lpszUsername">The username for the logon attempt. Do not include the domain here.</param>
        /// <param name="lpszDomain">The domain or computer name. Use <c>null</c> for the local computer.</param>
        /// <param name="lpszPassword">The password for the account. Can be empty for accounts without a password.</param>
        /// <param name="dwLogonType">The type of logon operation to perform. For example, <see cref="LOGON32_LOGON_INTERACTIVE"/>.</param>
        /// <param name="dwLogonProvider">The logon provider. Typically <see cref="LOGON32_PROVIDER_DEFAULT"/>.</param>
        /// <param name="phToken">When the function succeeds, receives a handle to a token representing the specified user.</param>
        /// <returns><c>true</c> if the logon attempt succeeded; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method is a P/Invoke wrapper for the Windows API <c>LogonUserW</c> function from advapi32.dll.
        /// </remarks>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken
        );

        /// <summary>
        /// Closes an open object handle.
        /// </summary>
        /// <param name="handle">The handle to close.</param>
        /// <returns><c>true</c> if the function succeeds; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method is a P/Invoke wrapper for the Windows API <c>CloseHandle</c> function from kernel32.dll.
        /// </remarks>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Logon type for interactive logon (user can log on at the computer's console).
        /// </summary>
        public const int LOGON32_LOGON_INTERACTIVE = 2;

        /// <summary>
        /// Logon type for network login. Logs on the user for network access (no profile loaded).
        /// </summary>
        public const int LOGON32_LOGON_NETWORK = 3;

        /// <summary>
        /// The default logon provider.
        /// </summary>
        public const int LOGON32_PROVIDER_DEFAULT = 0;

        /// <summary>
        /// Takes a snapshot of the specified processes, as well as the heaps, modules, and threads used by these processes.
        /// </summary>
        /// <param name="dwFlags">The portions of the system to be included in the snapshot. Use <see cref="TH32CS_SNAPPROCESS"/> to include the process list.</param>
        /// <param name="th32ProcessID">The process identifier of the process to be included in the snapshot. Use 0 to include all processes in the system.</param>
        /// <returns>If the function succeeds, it returns an open handle to the specified snapshot. On failure, it returns <see cref="INVALID_HANDLE_VALUE"/>.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        /// <summary>
        /// Retrieves information about the first process encountered in a system snapshot.
        /// </summary>
        /// <param name="hSnapshot">A handle to the snapshot returned by a previous call to <see cref="CreateToolhelp32Snapshot"/>.</param>
        /// <param name="lppe">A pointer to a <see cref="PROCESSENTRY32"/> structure.</param>
        /// <returns>Returns <see langword="true"/> if the first entry of the process list has been copied to the buffer; otherwise <see langword="false"/>.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>
        /// Retrieves information about the next process recorded in a system snapshot.
        /// </summary>
        /// <param name="hSnapshot">A handle to the snapshot returned by a previous call to <see cref="CreateToolhelp32Snapshot"/>.</param>
        /// <param name="lppe">A pointer to a <see cref="PROCESSENTRY32"/> structure.</param>
        /// <returns>Returns <see langword="true"/> if the next entry of the process list has been copied to the buffer; otherwise <see langword="false"/>.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>
        /// Includes all processes in the system in the snapshot.
        /// </summary>
        public const uint TH32CS_SNAPPROCESS = 0x00000002;

        /// <summary>
        /// Represents an invalid Win32 handle value.
        /// </summary>
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>
        /// Describes an entry from a list of the processes residing in the system address space when a snapshot was taken.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PROCESSENTRY32
        {
            /// <summary>The size of the structure, in bytes. Before calling <see cref="Process32First"/>, set this to sizeof(PROCESSENTRY32).</summary>
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
        /// Contains basic information about a process, including its unique PID and its parent's PID.
        /// Used primarily with <see cref="NtQueryInformationProcess"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessBasicInformation
        {
            public IntPtr Reserved1;
            /// <summary>The address of the Process Environment Block (PEB).</summary>
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            private readonly IntPtr Reserved2_1;
            private readonly IntPtr Reserved2_2;
            /// <summary>The unique identifier for the process.</summary>
            public IntPtr UniqueProcessId;
            /// <summary>The unique identifier for the parent process.</summary>
            public IntPtr InheritedFromUniqueProcessId;
        }

        /// <summary>
        /// Retrieves information about the specified process from the native NT system.
        /// </summary>
        /// <param name="processHandle">A handle to the process for which information is to be retrieved.</param>
        /// <param name="processInformationClass">The type of process information to be retrieved (e.g., ProcessBasicInformation = 0).</param>
        /// <param name="processInformation">A pointer to a buffer in which the function returns the requested information.</param>
        /// <param name="processInformationLength">The size of the buffer pointed to by the <paramref name="processInformation"/> parameter, in bytes.</param>
        /// <param name="returnLength">A pointer to a variable in which the function returns the size of the requested information.</param>
        /// <returns>Returns an NTSTATUS success or error code.</returns>
        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref ProcessBasicInformation processInformation,
            uint processInformationLength,
            out uint returnLength
        );

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

        /// <summary>
        /// Retrieves a handle to the specified standard device (standard input, standard output, or standard error).
        /// </summary>
        /// <param name="nStdHandle">
        /// The standard device. This parameter can be one of the following values:
        /// <see cref="STD_OUTPUT_HANDLE"/> (-11) or <see cref="STD_ERROR_HANDLE"/> (-12).
        /// </param>
        /// <returns>
        /// If the function succeeds, the return value is a handle to the specified device.
        /// If the function fails, the return value is <see cref="IntPtr.Zero"/> (INVALID_HANDLE_VALUE).
        /// </returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        /// <summary>
        /// Sets the handle for the specified standard device (standard input, standard output, or standard error).
        /// </summary>
        /// <param name="nStdHandle">The standard device for which the handle is to be set.</param>
        /// <param name="handle">The handle for the standard device.</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetStdHandle(int nStdHandle, IntPtr handle);

        /// <summary>
        /// The standard output device. Initially, this is the active console screen buffer.
        /// </summary>
        public const int STD_OUTPUT_HANDLE = -11;

        /// <summary>
        /// The standard error device. Initially, this is the active console screen buffer.
        /// </summary>
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
        [Flags]
        public enum ProcessAccess : uint
        {
            /// <summary>
            /// Required to retrieve certain information about a process, such as its token, 
            /// exit code, and priority class.
            /// </summary>
            QueryInformation = 0x0400,

            /// <summary>
            /// Required to retrieve a subset of the information about a process (see GetExitCodeProcess).
            /// A handle that has this access right can be opened for any process regardless of 
            /// the user's security context.
            /// </summary>
            QueryLimitedInformation = 0x1000,
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

        /// <summary>
        /// The service can perform cleanup tasks during a system shutdown. 
        /// Setting this flag in the 'acceptedCommands' bitmask enables the service 
        /// to receive the SERVICE_CONTROL_PRESHUTDOWN notification.
        /// </summary>
        public const int SERVICE_ACCEPT_PRESHUTDOWN = 0x00000100;

        /// <summary>
        /// The service is running. This corresponds to the <c>SERVICE_RUNNING</c> state.
        /// </summary>
        public const int SERVICE_RUNNING = 0x00000004;

        /// <summary>
        /// The control code sent by the SCM to notify a service that the system is 
        /// about to shut down. This notification provides a longer timeout window 
        /// than the standard SERVICE_CONTROL_SHUTDOWN signal.
        /// </summary>
        public const int SERVICE_CONTROL_PRESHUTDOWN = 0x0000000F;

        /// <summary>
        /// The service is stopping. This corresponds to the <c>SERVICE_STOP_PENDING</c> state.
        /// </summary>
        public const int SERVICE_STOP_PENDING = 0x00000003;

        /// <summary>
        /// The service is not running. This corresponds to the <c>SERVICE_STOPPED</c> state.
        /// </summary>
        public const int SERVICE_STOPPED = 0x00000001;

        /// <summary>
        /// The service can be stopped. This control code allows the SCM to send the <c>SERVICE_CONTROL_STOP</c> request.
        /// </summary>
        public const int SERVICE_ACCEPT_STOP = 0x00000001;

        /// <summary>
        /// The service runs in its own process. Corresponds to <c>SERVICE_WIN32_OWN_PROCESS</c>.
        /// </summary>
        public const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        /// <summary>
        /// Updates the service control manager's status information for the calling service.
        /// </summary>
        /// <param name="hServiceStatus">A handle to the status information structure for the current service.</param>
        /// <param name="lpServiceStatus">A pointer to the <see cref="SERVICE_STATUS"/> structure containing the latest status information.</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);

        /// <summary>
        /// Retrieves the number of milliseconds that have elapsed since the system was started.
        /// </summary>
        /// <returns>The number of milliseconds since the system was started.</returns>
        /// <remarks>
        /// This native method is used in .NET Framework 4.8 as a replacement for 
        /// <see cref="System.Environment.TickCount"/> to avoid the 32-bit signed integer rollover 
        /// (which occurs every 24.9 days). <c>GetTickCount64</c> provides a 64-bit unsigned 
        /// integer that does not wrap around for approximately 584 million years.
        /// </remarks>
        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        /// <summary>
        /// Contains status information for a service.
        /// </summary>
        /// <remarks>
        /// See <see href="https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_status">SERVICE_STATUS documentation</see> for details.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_STATUS
        {
            /// <summary>The type of service.</summary>
            public int dwServiceType;

            /// <summary>The current state of the service (e.g., STARTING, RUNNING, STOPPING).</summary>
            public int dwCurrentState;

            /// <summary>The control codes the service accepts and processes in its handler function.</summary>
            public int dwControlsAccepted;

            /// <summary>The error code the service uses to report an error that occurs when it is starting or stopping.</summary>
            public int dwWin32ExitCode;

            /// <summary>A service-specific error code that the service returns when an error occurs while the service is starting or stopping.</summary>
            public int dwServiceSpecificExitCode;

            /// <summary>The check-point value that the service increments periodically to report its progress during a lengthy start, stop, pause, or continue operation.</summary>
            public int dwCheckPoint;

            /// <summary>The estimated time required for a pending start, stop, pause, or continue operation, in milliseconds.</summary>
            public int dwWaitHint;
        }

        /// <summary>
        /// Enables subsequent open operations on a file or device to request read access.
        /// </summary>
        public const uint FILE_SHARE_READ = 0x00000001;

        /// <summary>
        /// Enables subsequent open operations on a file or device to request write access.
        /// </summary>
        public const uint FILE_SHARE_WRITE = 0x00000002;

        /// <summary>
        /// Enables subsequent open operations on a file or device to request delete access.
        /// </summary>
        public const uint FILE_SHARE_DELETE = 0x00000004;

        /// <summary>
        /// Opens a file or device only if it exists.
        /// </summary>
        public const uint OPEN_EXISTING = 3;

        /// <summary>
        /// Indicates that the file is being opened or created for a backup or restore operation. 
        /// Required to obtain a handle to a directory.
        /// </summary>
        public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        /// <summary>
        /// Return the path with a drive letter (e.g., C:\).
        /// </summary>
        public const uint VOLUME_NAME_DOS = 0x0;

        /// <summary>
        /// Creates or opens a file or I/O device.
        /// </summary>
        /// <param name="lpFileName">The name of the file or device to be created or opened.</param>
        /// <param name="dwDesiredAccess">The requested access to the file or device (Read, Write, etc.).</param>
        /// <param name="dwShareMode">The requested sharing mode of the file or device.</param>
        /// <param name="lpSecurityAttributes">A pointer to a SECURITY_ATTRIBUTES structure.</param>
        /// <param name="dwCreationDisposition">An action to take on a file or device that exists or does not exist.</param>
        /// <param name="dwFlagsAndAttributes">The file or device attributes and flags.</param>
        /// <param name="hTemplateFile">A valid handle to a template file with GENERIC_READ access rights.</param>
        /// <returns>An open handle to the specified file, device, named pipe, or mail slot.</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        /// <summary>
        /// Retrieves the final path for the specified file handle.
        /// </summary>
        /// <param name="hFile">A handle to a file or directory.</param>
        /// <param name="lpszFilePath">A pointer to a buffer that receives the path of hFile.</param>
        /// <param name="cchFilePath">The size of lpszFilePath, in TCHARs.</param>
        /// <param name="dwFlags">The type of format to be returned.</param>
        /// <returns>The length of the string copied to the buffer.</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetFinalPathNameByHandle(
            IntPtr hFile,
            [Out] StringBuilder lpszFilePath,
            uint cchFilePath,
            uint dwFlags);

        #region LogonAsServiceGrant Interop

        /// <summary>
        /// The LSA_UNICODE_STRING structure is used by various Local Security Authority (LSA) 
        /// functions to specify a Unicode string.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct LsaUnicodeString
        {
            /// <summary>The length, in bytes, of the string pointed to by the Buffer member.</summary>
            public ushort Length;
            /// <summary>The total size, in bytes, of the memory allocated for Buffer.</summary>
            public ushort MaximumLength;
            /// <summary>Pointer to a wide-character string.</summary>
            public IntPtr Buffer;
        }

        /// <summary>
        /// The LSA_OBJECT_ATTRIBUTES structure is used with the LsaOpenPolicy function to 
        /// specify the attributes of the connection to the Policy object.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct LsaObjectAttributes
        {
            /// <summary>The size of the structure in bytes. Must be initialized using Marshal.SizeOf.</summary>
            public int Length;
            /// <summary>Reserved; must be IntPtr.Zero.</summary>
            public IntPtr RootDir;
            /// <summary>Reserved; must be IntPtr.Zero.</summary>
            public IntPtr ObjectName;
            /// <summary>Reserved; must be 0.</summary>
            public uint Attributes;
            /// <summary>Reserved; must be IntPtr.Zero.</summary>
            public IntPtr SecurityDesc;
            /// <summary>Reserved; must be IntPtr.Zero.</summary>
            public IntPtr SecurityQos;
        }

        /// <summary>
        /// Contains access right constants for opening the LSA Policy object.
        /// </summary>
        public static class PolicyAccess
        {
            /// <summary>Required to look up the names of SIDs.</summary>
            public const uint POLICY_LOOKUP_NAMES = 0x00000800;
            /// <summary>Required to create a new account in the LSA database.</summary>
            public const uint POLICY_CREATE_ACCOUNT = 0x00000010;
            /// <summary>Required to assign a privilege to an account.</summary>
            public const uint POLICY_ASSIGN_PRIVILEGE = 0x00000400;
        }

        /// <summary>
        /// Frees memory allocated for an LSA buffer by functions such as LsaEnumerateAccountRights.
        /// </summary>
        /// <param name="buffer">Pointer to the memory buffer to free.</param>
        /// <returns>An NTSTATUS code.</returns>
        [DllImport("advapi32.dll")]
        public static extern int LsaFreeMemory(IntPtr buffer);

        /// <summary>
        /// Converts an LSA NTSTATUS code to its corresponding Win32 error code.
        /// </summary>
        /// <param name="status">The NTSTATUS code returned by an LSA function.</param>
        /// <returns>The mapping Win32 error code.</returns>
        [DllImport("advapi32.dll")]
        public static extern int LsaNtStatusToWinError(int status);

        /// <summary>
        /// Opens a handle to the Policy object on a local or remote system.
        /// </summary>
        /// <param name="systemName">Target system name, or IntPtr.Zero for the local machine.</param>
        /// <param name="objectAttributes">Connection attributes; Length must be initialized.</param>
        /// <param name="desiredAccess">The requested access rights (see <see cref="PolicyAccess"/>).</param>
        /// <param name="policyHandle">The resulting handle to the Policy object.</param>
        /// <returns>An NTSTATUS code. 0 (STATUS_SUCCESS) indicates success.</returns>
        [DllImport("advapi32.dll")]
        public static extern int LsaOpenPolicy(
            IntPtr systemName,
            ref LsaObjectAttributes objectAttributes,
            uint desiredAccess,
            out IntPtr policyHandle);

        /// <summary>
        /// Adds one or more privileges to an account. If the account does not exist, it is created.
        /// </summary>
        /// <param name="policyHandle">Handle to the Policy object.</param>
        /// <param name="accountSid">Pointer to the SID of the account.</param>
        /// <param name="userRights">Array of Unicode strings containing the names of the rights to add.</param>
        /// <param name="count">Number of elements in the userRights array.</param>
        /// <returns>An NTSTATUS code.</returns>
        [DllImport("advapi32.dll")]
        public static extern int LsaAddAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            LsaUnicodeString[] userRights,
            int count);

        /// <summary>
        /// Returns the privileges assigned to a specific account.
        /// </summary>
        /// <param name="policyHandle">Handle to the Policy object.</param>
        /// <param name="accountSid">Pointer to the SID of the account.</param>
        /// <param name="userRights">Output pointer to an array of LSA_UNICODE_STRINGs. Must be freed via LsaFreeMemory.</param>
        /// <param name="countOfRights">The number of rights returned in the array.</param>
        /// <returns>An NTSTATUS code.</returns>
        [DllImport("advapi32.dll")]
        public static extern int LsaEnumerateAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            out IntPtr userRights,
            out uint countOfRights);

        /// <summary>
        /// Closes a handle to the Policy object and releases associated resources.
        /// </summary>
        /// <param name="policyHandle">The handle to close.</param>
        /// <returns>An NTSTATUS code.</returns>
        [DllImport("advapi32.dll")]
        public static extern int LsaClose(IntPtr policyHandle);

        /// <summary>
        /// Native P/Invoke signature for the Windows MoveFileEx function.
        /// </summary>
        /// <param name="lpExistingFileName">The current name of the file or directory on the local computer.</param>
        /// <param name="lpNewFileName">The new name of the file or directory on the local computer.</param>
        /// <param name="dwFlags">Flags that control the move operation (e.g., replacement, write-through).</param>
        /// <returns><see langword="true"/> if the function succeeds; otherwise, <see langword="false"/>.</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);

        /// <summary>
        /// Flag indicating that if a file with the destination name already exists, it should be replaced.
        /// </summary>
        private const uint MOVEFILE_REPLACE_EXISTING = 0x01;

        /// <summary>
        /// Flag indicating that the function should not return until the file has actually been moved on the disk.
        /// </summary>
        private const uint MOVEFILE_WRITE_THROUGH = 0x08;

        /// <summary>
        /// Retrieves information for the specified file.
        /// </summary>
        /// <param name="hFile">A handle to the file that contains the information to be retrieved.</param>
        /// <param name="lpFileInformation">A pointer to a <see cref="BY_HANDLE_FILE_INFORMATION"/> structure that receives the file information.</param>
        /// <returns>If the function succeeds, the return value is nonzero; otherwise, it is zero.</returns>
        /// <remarks>
        /// This Win32 API is used to obtain the <see cref="BY_HANDLE_FILE_INFORMATION.VolumeSerialNumber"/> and 
        /// <see cref="BY_HANDLE_FILE_INFORMATION.FileIndexHigh"/> / <see cref="BY_HANDLE_FILE_INFORMATION.FileIndexLow"/>, 
        /// which together form a unique identifier for a file on a specific volume.
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        /// <summary>
        /// Represents a 64-bit value stating the number of 100-nanosecond intervals since January 1, 1601 (UTC).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            /// <summary>The low-order part of the file time.</summary>
            public uint dwLowDateTime;
            /// <summary>The high-order part of the file time.</summary>
            public uint dwHighDateTime;
        }

        /// <summary>
        /// Contains information that the <see cref="GetFileInformationByHandle"/> function retrieves.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            /// <summary>The file attributes.</summary>
            public uint FileAttributes;
            /// <summary>A <see cref="FILETIME"/> structure that specifies when a file or directory is created.</summary>
            public FILETIME CreationTime;
            /// <summary>A <see cref="FILETIME"/> structure that specifies when a file or directory is last accessed.</summary>
            public FILETIME LastAccessTime;
            /// <summary>A <see cref="FILETIME"/> structure that specifies when a file or directory is last written to.</summary>
            public FILETIME LastWriteTime;
            /// <summary>The serial number of the volume that contains a file.</summary>
            public uint VolumeSerialNumber;
            /// <summary>The high-order part of the file size.</summary>
            public uint FileSizeHigh;
            /// <summary>The low-order part of the file size.</summary>
            public uint FileSizeLow;
            /// <summary>The number of links to this file.</summary>
            public uint NumberOfLinks;
            /// <summary>The high-order part of a unique identifier that is associated with a file.</summary>
            public uint FileIndexHigh;
            /// <summary>The low-order part of a unique identifier that is associated with a file.</summary>
            public uint FileIndexLow;
        }

        /// <summary>
        /// Encapsulates the unique identity of a file on disk to detect rotation or replacement.
        /// </summary>
        public struct FileIdentity
        {
            /// <summary>
            /// Gets the unique 64-bit file index (inode-equivalent on Windows).
            /// </summary>
            public ulong FileIndex;

            /// <summary>
            /// Gets the serial number of the volume where the file resides.
            /// </summary>
            public uint VolumeSerialNumber;

            /// <summary>
            /// Gets a Base64-encoded hash of the file's leading bytes, used as a fallback identity signal.
            /// </summary>
            public string PrefixHash;

            /// <summary>
            /// Gets a value indicating whether the <see cref="FileIndex"/> and <see cref="VolumeSerialNumber"/> 
            /// were successfully retrieved via P/Invoke.
            /// </summary>
            public bool IsValidHandleInfo;

            /// <summary>
            /// Compares this identity with another to determine if the underlying file has been rotated or replaced.
            /// </summary>
            /// <param name="other">The other identity to compare against.</param>
            /// <returns>True if the identities differ, indicating a file rotation; otherwise, false.</returns>
            public bool IsDifferentFrom(FileIdentity other)
            {
                // Primary check: unique OS file identifier
                if (IsValidHandleInfo && other.IsValidHandleInfo)
                {
                    return FileIndex != other.FileIndex || VolumeSerialNumber != other.VolumeSerialNumber;
                }

                // Fallback: Check if the content prefix changed (useful for FAT32 or certain network shares)
                if (PrefixHash != null && other.PrefixHash != null)
                {
                    return PrefixHash != other.PrefixHash;
                }

                return false;
            }
        }

        #endregion

        /// <summary>
        /// Validates Windows credentials by resolving the identity and attempting a network logon.
        /// </summary>
        /// <param name="username">
        /// The username in one of the following formats:
        /// <list type="bullet">
        /// <item><description><c>DOMAIN\Username</c> for domain accounts</description></item>
        /// <item><description><c>.\Username</c> for local accounts on the current machine</description></item>
        /// <item><description><c>DOMAIN\gMSA$</c> for Group Managed Service Accounts</description></item>
        /// </list>
        /// </param>
        /// <param name="password">
        /// The password associated with the account. 
        /// Can be <c>null</c> or empty for accounts without passwords or for gMSA.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when the username is null, empty, or fails format validation.
        /// </exception>
        /// <exception cref="SecurityException">
        /// Thrown if the account cannot be resolved to a Security Identifier (SID).
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown if the password is incorrect or account restrictions prevent logon.
        /// </exception>
        /// <exception cref="Win32Exception">
        /// Thrown for underlying system-related logon errors.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method performs a two-stage validation: 
        /// 1. Identity resolution via <see cref="NTAccount.Translate(Type)"/> to ensure the account exists.
        /// 2. Authentication via the Win32 <c>LogonUser</c> API using <c>LOGON32_LOGON_NETWORK</c>.
        /// It does not verify the "Log on as a service" right (<c>SeServiceLogonRight</c>).
        /// </para>
        /// <para>
        /// <b>CAUTION:</b> This method performs a live network logon. Excessive calls with 
        /// incorrect passwords will trigger Active Directory account lockout policies.
        /// </para>
        /// <para>
        /// Use this method sparingly and consider implementing a "Test Connection" UI pattern 
        /// rather than automatic validation on every keystroke.
        /// </para>
        /// </remarks>
        public static void ValidateCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.");

            // Regex pattern:
            // Matches:
            // - DOMAIN\User (domain and username separated by \)
            // - .\User (local machine)
            const string pattern = @"^(?:[\w\s\.\-]+|\.)\\[\w\s\.@!\-]+\$?$";
            var isGMSA = username.EndsWith("$");

            const string invalidMsg = "Username format is invalid. Expected .\\Username, DOMAIN\\Username, or DOMAIN\\gMSA$.";
            if (!Regex.IsMatch(username, pattern))
            {
                throw new ArgumentException(invalidMsg);
            }

            // Split DOMAIN\user or .\user into domain and username parts
            string domain = null;
            string user = username;

            if (username.Contains("\\"))
            {
                var parts = username.Split('\\');
                domain = parts[0];
                user = parts[1]?.Trim();

                if (string.IsNullOrWhiteSpace(user?.TrimEnd('$')))
                {
                    throw new ArgumentException(invalidMsg);
                }
            }

            // Identity Resolution Check:
            // We attempt to translate the name to a SID. This verifies the account 
            // exists and is reachable (e.g., Domain Controller is online).
            try
            {
                string translationName = username;
                if (username.StartsWith(".\\", StringComparison.OrdinalIgnoreCase))
                {
                    // Replace ".\" with "MACHINE_NAME\"
                    translationName = Environment.MachineName + username.Substring(1);
                }
                var ntAccount = new NTAccount(translationName);
                _ = ntAccount.Translate(typeof(SecurityIdentifier));
            }
            catch (IdentityNotMappedException)
            {
                throw new SecurityException($"The account '{username}' could not be resolved. Please verify the username.");
            }
            catch (Exception ex)
            {
                throw new SecurityException($"An error occurred while resolving the account '{username}': {ex.Message}");
            }

            if (string.IsNullOrEmpty(password))
            {
                // For empty passwords, skip password validation
                return;
            }

            if (isGMSA)
            {
                // For gMSA, skip password validation
                return;
            }

            var token = IntPtr.Zero;

            try
            {
                var success = LogonUser(
                user,
                domain,
                password,
                LOGON32_LOGON_NETWORK, // Verify network logon works
                LOGON32_PROVIDER_DEFAULT,
                out token
            );

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();

                    // Common error codes for clarity
                    switch (error)
                    {
                        case 1326: // ERROR_LOGON_FAILURE
                            throw new UnauthorizedAccessException("Invalid username or password.");

                        case 1327: // ERROR_ACCOUNT_RESTRICTION
                            throw new UnauthorizedAccessException("Account restrictions prevent logon.");

                        case 1385: // ERROR_LOGON_TYPE_NOT_GRANTED
                                   // This is reached ONLY if the credentials are valid but the 'Network Logon' 
                                   // type is restricted (e.g., by GPO).
                            throw new UnauthorizedAccessException(
                                "Authentication succeeded, but 'Network Logon' is denied by security policy. " +
                                "This is common for hardened service accounts and indicates the credentials " +
                                "are likely correct, even though a validation session could not be fully opened.");

                        default:
                            throw new Win32Exception(error, $"Logon failed with error code {error}.");
                    }
                }
            }
            finally
            {
                // Always close the logon token handle
                if (token != IntPtr.Zero)
                {
                    CloseHandle(token);
                }
            }
        }

        /// <summary>
        /// Replaces a destination file with a source file atomically, ensuring that the source's 
        /// security descriptor (ACLs) and metadata are preserved at the destination.
        /// </summary>
        /// <param name="source">The full path of the new file (typically a temporary file) to be moved.</param>
        /// <param name="destination">The full path of the file to be replaced.</param>
        /// <remarks>
        /// This method utilizes the native Win32 <c>MoveFileEx</c> function. Unlike <see cref="System.IO.File.Replace(string, string, string)"/>, 
        /// which preserves the destination's ACLs, this approach ensures that the hardened security descriptor 
        /// applied to the source file travels with it, effectively overwriting any permissive ACLs on the destination.
        /// </remarks>
        /// <exception cref="Win32Exception">Thrown if the native move operation fails.</exception>
        public static void AtomicSecureMove(string source, string destination)
        {
            // MOVEFILE_REPLACE_EXISTING: Overwrites the target atomically.
            // MOVEFILE_WRITE_THROUGH: Ensures the command doesn't return until the drive actually commits the change.
            if (!MoveFileEx(source, destination, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Failed to atomically replace secure file. Win32 Error: {error}");
            }
        }

        /// <summary>
        /// Extracts the <see cref="FileIdentity"/> from an open <see cref="FileStream"/>.
        /// </summary>
        /// <param name="fs">The stream of the file to identify.</param>
        /// <returns>A <see cref="FileIdentity"/> representing the current file state.</returns>
        /// <remarks>
        /// This method attempts to use the high-reliability <see cref="GetFileInformationByHandle"/> API first.
        /// If the API fails or is unsupported by the filesystem, it captures a hash of the first 64 bytes 
        /// of the file as a secondary identity signal.
        /// </remarks>
        public static FileIdentity GetFileIdentity(FileStream fs)
        {
            var identity = new FileIdentity();
            try
            {
                // Use SafeFileHandle to safely retrieve the Win32 handle
                if (GetFileInformationByHandle(fs.SafeFileHandle.DangerousGetHandle(), out var info))
                {
                    identity.VolumeSerialNumber = info.VolumeSerialNumber;
                    identity.FileIndex = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
                    identity.IsValidHandleInfo = true;
                }
            }
            catch
            {
                // Fail-silent on P/Invoke to allow the hash fallback to take over
            }

            // Secondary Fallback: Capture a "fingerprint" of the first 64 bytes
            try
            {
                long origPos = fs.Position;
                fs.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[64];
                int read = fs.Read(buffer, 0, buffer.Length);
                identity.PrefixHash = Convert.ToBase64String(buffer, 0, read);

                // Restore original stream position to avoid disrupting callers
                fs.Seek(origPos, SeekOrigin.Begin);
            }
            catch
            {
                // Keep PrefixHash null if the stream is unreadable or closed
            }

            return identity;
        }

    }
}

#pragma warning restore SYSLIB1054
#pragma warning restore IDE0079
