using Servy.Core.Config;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
    /// Provides a comprehensive collection of Win32 API definitions, structures, and constants 
    /// for Windows Service management, process lifecycle control, and security rights.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static partial class NativeMethods
    {
        #region Constants

        /// <summary>Required to query the status of a service.</summary>
        public const int SERVICE_QUERY_STATUS = 0x0004;
        /// <summary>Service start type: started by the service control manager when a process calls StartService.</summary>
        public const int SERVICE_DEMAND_START = 0x00000003;
        /// <summary>Value used in ChangeServiceConfig to indicate no change to a parameter.</summary>
        public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        /// <summary>Control code to stop the service.</summary>
        public const int SERVICE_CONTROL_STOP = 0x00000001;
        /// <summary>Information level for QueryServiceConfig2/ChangeServiceConfig2: Delayed auto-start.</summary>
        public const int SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 0x00000003;
        /// <summary>Information level for QueryServiceConfig2/ChangeServiceConfig2: Service description.</summary>
        public const int SERVICE_CONFIG_DESCRIPTION = 1;

        /// <summary>Access right to enumerate services in the SCM database.</summary>
        public const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;
        /// <summary>Access right to query the configuration parameters of a service.</summary>
        public const uint SERVICE_QUERY_CONFIG = 0x0001;

        /// <summary>Logon type: Interactive. Designed for users who will be using the computer interactively.</summary>
        public const int LOGON32_LOGON_INTERACTIVE = 2;
        /// <summary>Logon type: Network. Intended for high-performance servers to authenticate clear-text passwords.</summary>
        public const int LOGON32_LOGON_NETWORK = 3;
        /// <summary>Uses the standard logon provider for the system.</summary>
        public const int LOGON32_PROVIDER_DEFAULT = 0;

        /// <summary>Snapshot flag: Includes all processes in the system in the snapshot.</summary>
        public const uint TH32CS_SNAPPROCESS = 0x00000002;
        /// <summary>Represents an invalid handle value (-1).</summary>
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>Pseudo-handle for the parent process of the calling process.</summary>
        public const int ATTACH_PARENT_PROCESS = -1;
        /// <summary>UTF-8 code page identifier.</summary>
        public const uint CP_UTF8 = 65001;

        /// <summary>Identifier for the standard output device.</summary>
        public const int STD_OUTPUT_HANDLE = -11;
        /// <summary>Identifier for the standard error device.</summary>
        public const int STD_ERROR_HANDLE = -12;

        /// <summary>The service accepts pre-shutdown notifications.</summary>
        public const int SERVICE_ACCEPT_PRESHUTDOWN = 0x00000100;
        /// <summary>The service is running.</summary>
        public const int SERVICE_RUNNING = 0x00000004;
        /// <summary>Control code: Pre-shutdown notification.</summary>
        public const int SERVICE_CONTROL_PRESHUTDOWN = 0x0000000F;
        /// <summary>The service is in the process of stopping.</summary>
        public const int SERVICE_STOP_PENDING = 0x00000003;
        /// <summary>The service has stopped.</summary>
        public const int SERVICE_STOPPED = 0x00000001;
        /// <summary>The service accepts the STOP control code.</summary>
        public const int SERVICE_ACCEPT_STOP = 0x00000001;
        /// <summary>The service runs in its own process.</summary>
        public const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        /// <summary>Share mode: Enables subsequent open operations on a file/device to request read access.</summary>
        public const uint FILE_SHARE_READ = 0x00000001;
        /// <summary>Share mode: Enables subsequent open operations on a file/device to request write access.</summary>
        public const uint FILE_SHARE_WRITE = 0x00000002;
        /// <summary>Share mode: Enables subsequent open operations on a file/device to request delete access.</summary>
        public const uint FILE_SHARE_DELETE = 0x00000004;
        /// <summary>Creation disposition: Opens a file/device only if it exists.</summary>
        public const uint OPEN_EXISTING = 3;
        /// <summary>Flag: Opens a directory for backup/restore or identity tracking.</summary>
        public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        /// <summary>Path flag: Return the path with a drive letter.</summary>
        public const uint VOLUME_NAME_DOS = 0x0;

        private const uint MOVEFILE_REPLACE_EXISTING = 0x01;
        private const uint MOVEFILE_WRITE_THROUGH = 0x08;

        #endregion

        #region Structures & Enums

        /// <summary>Contains a service description string.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceDescription
        {
            /// <summary>A pointer to the description string.</summary>
            public IntPtr lpDescription;
        }

        /// <summary>Contains the delayed auto-start setting of an auto-start service.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceDelayedAutoStartInfo
        {
            /// <summary>If true, the service is started after other auto-start services have finished.</summary>
            [MarshalAs(UnmanagedType.Bool)]
            public bool fDelayedAutostart;
        }

        /// <summary>Contains the pre-shutdown timeout setting for a service.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ServicePreShutdownInfo
        {
            /// <summary>The timeout value in milliseconds.</summary>
            public uint dwPreshutdownTimeout;
        }

        /// <summary>Contains configuration information for an installed service.</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct QUERY_SERVICE_CONFIG
        {
            /// <summary>The type of service.</summary>
            public uint dwServiceType;
            /// <summary>When to start the service.</summary>
            public uint dwStartType;
            /// <summary>Severity of error if service fails to start.</summary>
            public uint dwErrorControl;
            /// <summary>Path to the service binary.</summary>
            public IntPtr lpBinaryPathName;
            /// <summary>Load ordering group name.</summary>
            public IntPtr lpLoadOrderGroup;
            /// <summary>Tag identifier.</summary>
            public uint dwTagId;
            /// <summary>Names of services/groups that must start before this service.</summary>
            public IntPtr lpDependencies;
            /// <summary>Account name under which the service runs.</summary>
            public IntPtr lpServiceStartName;
            /// <summary>Service display name.</summary>
            public IntPtr lpDisplayName;
        }

        /// <summary>Represents the status of a service.</summary>
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

        /// <summary>Internal structure for service status reports.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_STATUS
        {
            public int dwServiceType;
            public int dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }

        /// <summary>Describes a process entry in a system snapshot.</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        /// <summary>Contains basic information about a process for internal NT queries.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessBasicInformation
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            private readonly IntPtr Reserved2_1;
            private readonly IntPtr Reserved2_2;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        /// <summary>Defines common process access rights.</summary>
        [Flags]
        public enum ProcessAccess : uint
        {
            /// <summary>Required to retrieve information about a process.</summary>
            QueryInformation = 0x0400,
            /// <summary>Required to retrieve a subset of information about a process.</summary>
            QueryLimitedInformation = 0x1000,
        }

        /// <summary>Information classes for NtQueryInformationProcess.</summary>
        public enum ProcessInfoClass
        {
            ProcessBasicInformation = 0,
        }

        /// <summary>Represents console control signal types.</summary>
        public enum CtrlEvents : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6,
        }

        /// <summary>Information classes for Job Object configuration.</summary>
        public enum JobObjectInfoClass
        {
            JobObjectExtendedLimitInformation = 9
        }

        /// <summary>Defines limit flags for job objects.</summary>
        [Flags]
        public enum JobLimits : uint
        {
            /// <summary>Forces all processes in the job to terminate when the job handle is closed.</summary>
            KillOnJobClose = 0x00002000
        }

        /// <summary>Contains extended limit information for a job object.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct JobobjectExtendedLimitInformation
        {
            public JobobjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        /// <summary>Contains basic limit information for a job object.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct JobobjectBasicLimitInformation
        {
            public Int64 PerProcessUserTimeLimit;
            public Int64 PerJobUserTimeLimit;
            public JobLimits LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public UInt32 ActiveProcessLimit;
            public Int64 Affinity;
            public UInt32 PriorityClass;
            public UInt32 SchedulingClass;
        }

        /// <summary>Contains I/O accounting information for a job object.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IoCounters
        {
            public UInt64 ReadOperationCount;
            public UInt64 WriteOperationCount;
            public UInt64 OtherOperationCount;
            public UInt64 ReadTransferCount;
            public UInt64 WriteTransferCount;
            public UInt64 OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        /// <summary>Provides a unique identifier for a file on a specific volume.</summary>
        public struct FileIdentity
        {
            /// <summary>The unique file index.</summary>
            public ulong FileIndex;
            /// <summary>The volume serial number.</summary>
            public uint VolumeSerialNumber;
            /// <summary>A hash of the start of the file for secondary identification.</summary>
            public string PrefixHash;
            /// <summary>Indicates if handle-based information was successfully retrieved.</summary>
            public bool IsValidHandleInfo;

            /// <summary>Checks if the identity is different from another file identity.</summary>
            public bool IsDifferentFrom(FileIdentity other)
            {
                if (IsValidHandleInfo && other.IsValidHandleInfo)
                {
                    if (FileIndex != other.FileIndex || VolumeSerialNumber != other.VolumeSerialNumber)
                        return true;
                }

                if (PrefixHash != null && other.PrefixHash != null)
                {
                    return PrefixHash != other.PrefixHash;
                }

                return false;
            }
        }

        #endregion

        #region SCM & Service Functions

        /// <summary>Connects to the Service Control Manager on the specified computer.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeScmHandle OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

        /// <summary>Creates a service object and adds it to the specified SCM database.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeServiceHandle CreateService(
          SafeScmHandle hSCManager,
          string lpServiceName,
          string lpDisplayName,
          uint dwDesiredAccess,
          uint dwServiceType,
          uint dwStartType,
          uint dwErrorControl,
          string lpBinaryPathName,
          string? lpLoadOrderGroup,
          IntPtr lpdwTagId,
          string? lpDependencies,
          string? lpServiceStartName,
          string? lpPassword);

        /// <summary>Opens an existing service.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeServiceHandle OpenService(SafeScmHandle hSCManager, string lpServiceName, uint dwDesiredAccess);

        /// <summary>Marks the specified service for deletion from the SCM database.</summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DeleteService(SafeServiceHandle hService);

        /// <summary>Closes a handle to a service or SCM database.</summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);

        /// <summary>Sends a control code to a service.</summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool ControlService(SafeServiceHandle hService, int dwControl, ref ServiceStatus lpServiceStatus);

        /// <summary>Retrieves the configuration parameters of the specified service.</summary>
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool QueryServiceConfig(
            SafeServiceHandle hService,
            IntPtr lpServiceConfig,
            int cbBufSize,
            out int pcbBytesNeeded);

        /// <summary>Retrieves optional configuration parameters (Delayed Auto Start).</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            ref ServiceDelayedAutoStartInfo lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        /// <summary>Retrieves optional configuration parameters using a raw buffer pointer.</summary>
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            IntPtr lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        /// <summary>Changes the configuration parameters of a service.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig(
            SafeServiceHandle hService,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string? lpBinaryPathName,
            string? lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string? lpDependencies,
            string? lpServiceStartName,
            string? lpPassword,
            string? lpDisplayName);

        /// <summary>Changes optional service configuration (Description).</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              SafeServiceHandle hService,
              int dwInfoLevel,
              ref ServiceDescription lpInfo);

        /// <summary>Changes optional service configuration (Delayed Auto Start).</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              SafeServiceHandle hService,
              int dwInfoLevel,
              ref ServiceDelayedAutoStartInfo lpInfo);

        /// <summary>Changes optional service configuration using a raw buffer pointer.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              SafeServiceHandle hService,
              int dwInfoLevel,
              IntPtr lpInfo);

        /// <summary>Updates the SCM's status information for the calling service.</summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);

        #endregion

        #region Job Object Functions

        /// <summary>Creates or opens a job object.</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern SafeJobObjectHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);

        /// <summary>Sets limits and configuration for a job object.</summary>
        [DllImport("kernel32.dll")]
        public static extern bool SetInformationJobObject(SafeJobObjectHandle hJob, JobObjectInfoClass infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        /// <summary>Assigns a process to an existing job object.</summary>
        [DllImport("kernel32.dll")]
        public static extern bool AssignProcessToJobObject(SafeJobObjectHandle hJob, IntPtr hProcess);

        #endregion

        #region Process & Snapshot Functions

        /// <summary>Takes a snapshot of specified processes, heaps, modules, and threads.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        /// <summary>Retrieves information about the first process encountered in a snapshot.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>Retrieves information about the next process recorded in a snapshot.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>Opens an existing local process object.</summary>
        [DllImport("kernel32.dll")]
        public static extern Handle OpenProcess(ProcessAccess desiredAccess, bool inheritHandle, int processId);

        /// <summary>Low-level NT function to retrieve process information.</summary>
        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref ProcessBasicInformation processInformation,
            uint processInformationLength,
            out uint returnLength);

        /// <summary>Internal overload for NtQueryInformationProcess.</summary>
        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            ProcessInfoClass processInformationClass,
            out ProcessBasicInformation processInformation,
            int processInformationLength,
            IntPtr returnLength = default);

        /// <summary>Retrieves the number of milliseconds since the system was started.</summary>
        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        #endregion

        #region Console Functions

        /// <summary>Delegate for processing console control signals.</summary>
        public delegate bool ConsoleCtrlHandlerRoutine(CtrlEvents ctrlType);

        /// <summary>Attaches the calling process to the console of a specified process.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(int processId);

        /// <summary>Allocates a new console for the calling process.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        /// <summary>Sets the output code page for the console.</summary>
        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleOutputCP(uint codePageID);

        /// <summary>Adds or removes an application-defined console signal handler.</summary>
        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerRoutine? handlerRoutine, bool add);

        /// <summary>Sends a specified signal to a console process group.</summary>
        [DllImport("kernel32.dll")]
        public static extern bool GenerateConsoleCtrlEvent(CtrlEvents ctrlEvent, uint processGroupId);

        /// <summary>Detaches the calling process from its console.</summary>
        [DllImport("kernel32.dll")]
        public static extern bool FreeConsole();

        /// <summary>Retrieves a handle to the specified standard device.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        /// <summary>Sets the handle for the specified standard device.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetStdHandle(int nStdHandle, IntPtr handle);

        #endregion

        #region File & Security Functions

        /// <summary>Closes an open object handle.</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern bool CloseHandle(IntPtr handle);

        /// <summary>Creates or opens a file or I/O device.</summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        /// <summary>Retrieves the final path for the specified file handle.</summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetFinalPathNameByHandle(
            IntPtr hFile,
            [Out] StringBuilder lpszFilePath,
            uint cchFilePath,
            uint dwFlags);

        /// <summary>Attempts to log a user on to the local computer.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(
            string lpszUsername,
            string? lpszDomain,
            string? lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        #endregion

        #region LogonAsServiceGrant Interop

        /// <summary>Used in LSA calls to represent a Unicode string.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct LsaUnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        /// <summary>Specifies attributes of a connection to the Policy object.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct LsaObjectAttributes
        {
            public int Length;
            public IntPtr RootDir;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDesc;
            public IntPtr SecurityQos;
        }

        /// <summary>LSA Policy database access rights.</summary>
        public static class PolicyAccess
        {
            public const uint POLICY_LOOKUP_NAMES = 0x00000800;
            public const uint POLICY_CREATE_ACCOUNT = 0x00000010;
            public const uint POLICY_ASSIGN_PRIVILEGE = 0x00000400;
        }

        /// <summary>Frees memory allocated by LSA functions.</summary>
        [DllImport("advapi32.dll")]
        public static extern int LsaFreeMemory(IntPtr buffer);

        /// <summary>Converts an NTSTATUS code to a Win32 error code.</summary>
        [DllImport("advapi32.dll")]
        public static extern int LsaNtStatusToWinError(int status);

        /// <summary>Opens a handle to the Policy object on a system.</summary>
        [DllImport("advapi32.dll")]
        public static extern int LsaOpenPolicy(
            IntPtr systemName,
            ref LsaObjectAttributes objectAttributes,
            uint desiredAccess,
            out IntPtr policyHandle);

        /// <summary>Adds rights to an account.</summary>
        [DllImport("advapi32.dll")]
        public static extern int LsaAddAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            LsaUnicodeString[] userRights,
            int count);

        /// <summary>Retrieves the rights assigned to an account.</summary>
        [DllImport("advapi32.dll")]
        public static extern int LsaEnumerateAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            out IntPtr userRights,
            out uint countOfRights);

        /// <summary>Closes an LSA Policy handle.</summary>
        [DllImport("advapi32.dll")]
        public static extern int LsaClose(IntPtr policyHandle);

        #endregion

        #region Helper Methods

        /// <summary>
        /// A safelist of built-in Windows service accounts and well-known identities that 
        /// do not have passwords and cannot be validated via standard LogonUser calls.
        /// </summary>
        private static readonly HashSet<string> BuiltInServiceAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Core Service Identities ---
            "System",
            "LocalSystem",
            "LocalService",
            "NetworkService",

            // --- NT AUTHORITY Prefixed (Exhaustive Spacing) ---
            "NT AUTHORITY\\System",
            "NT AUTHORITY\\LocalSystem",
            "NT AUTHORITY\\Local System",
            "NT AUTHORITY\\LocalService",
            "NT AUTHORITY\\Local Service",
            "NT AUTHORITY\\NetworkService",
            "NT AUTHORITY\\Network Service",

            // --- Dot / Local Prefixed ---
            ".\\System",
            ".\\LocalSystem",
            ".\\Local Service",
            ".\\LocalService",
            ".\\Network Service",
            ".\\NetworkService",

            // --- BUILTIN Prefixed ---
            "BUILTIN\\System",
            "BUILTIN\\LocalSystem",
            "BUILTIN\\LocalService",
            "BUILTIN\\NetworkService",

            // --- Specialized identities (Passwordless) ---
            "Anonymous Logon",
            "NT AUTHORITY\\Anonymous Logon",
            "Authenticated Users",
            "NT AUTHORITY\\Authenticated Users",
            "Everyone",
            "IUSR",
            "NT AUTHORITY\\IUSR",
    
            // --- Session/Context Identities ---
            "Batch",
            "NT AUTHORITY\\Batch",
            "Interactive",
            "NT AUTHORITY\\Interactive",
            "Service",
            "NT AUTHORITY\\Service",
            "Network",
            "NT AUTHORITY\\Network"
        };

        /// <summary>
        /// Validates Windows credentials by resolving the identity and attempting a network logon.
        /// Handles domain accounts, local accounts, gMSAs, and built-in service identities.
        /// </summary>
        /// <param name="username">The account name (e.g., DOMAIN\User, .\User, or NT AUTHORITY\NetworkService).</param>
        /// <param name="password">The account password. Must be null or empty for built-in accounts; optional for gMSAs.</param>
        /// <exception cref="ArgumentException">Invalid username format, or a password was provided for a passwordless built-in account.</exception>
        /// <exception cref="SecurityException">Identity cannot be resolved or translation failed.</exception>
        /// <exception cref="UnauthorizedAccessException">Invalid credentials or policy restriction.</exception>
        /// <exception cref="Win32Exception">Unexpected system error during logon.</exception>
        public static void ValidateCredentials(string username, string? password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.");

            // The pattern allows for 'NT AUTHORITY\Account', 'DOMAIN\Account', or '.\Account'
            const string pattern = @"^(?:[\w\s\.\-]+|\.)\\[\w\s\.@!\-]+\$?$";
            var isGMSA = username.EndsWith('$');

            // LOGIC: 
            // 1. Check the static exhaustive list (Case-Insensitive via HashSet comparer).
            // 2. Catch Virtual Service Accounts (NT SERVICE\...)
            // 3. Catch IIS AppPool Identities (IIS APPPOOL\...)
            var isBuiltIn = BuiltInServiceAccounts.Contains(username) ||
                            username.StartsWith("NT SERVICE\\", StringComparison.OrdinalIgnoreCase) ||
                            username.StartsWith("IIS APPPOOL\\", StringComparison.OrdinalIgnoreCase);

            // Skip regex validation for known built-in identities to avoid false negatives 
            // on specialized formats.
            const string invalidMsg = "Username format is invalid. Expected .\\Username, DOMAIN\\Username, or NT AUTHORITY\\ServiceAccount.";
            if (!isBuiltIn && !Regex.IsMatch(username, pattern, RegexOptions.IgnoreCase, AppConfig.InputRegexTimeout))
            {
                throw new ArgumentException(invalidMsg);
            }

            string? domain = null;
            string? user = username;

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

            // 1. Identity Resolution
            try
            {
                string translationName = username;
                if (username.StartsWith(".\\", StringComparison.OrdinalIgnoreCase))
                {
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

            // 2. Logon Validation Guard
            // Built-in service accounts (NetworkService, Virtual Accounts, etc.) are managed by the OS.
            // They are considered 'valid' if they pass the Translate check above, but they cannot have passwords.
            if (isBuiltIn)
            {
                if (!string.IsNullOrEmpty(password))
                {
                    throw new ArgumentException($"A password cannot be provided for the built-in passwordless identity '{username}'.");
                }
                return;
            }

            // gMSAs are managed by Active Directory and bypass standard local LogonUser validation.
            if (isGMSA)
            {
                return;
            }

            // 3. Password Validation for standard accounts
            var token = IntPtr.Zero;
            try
            {
                var success = LogonUser(
                    user,
                    domain,
                    password,
                    LOGON32_LOGON_NETWORK,
                    LOGON32_PROVIDER_DEFAULT,
                    out token
                );

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();

                    switch (error)
                    {
                        case 1326: // ERROR_LOGON_FAILURE
                            throw new UnauthorizedAccessException("Invalid username or password.");
                        case 1327: // ERROR_ACCOUNT_RESTRICTION
                            throw new UnauthorizedAccessException("Account restrictions prevent logon (e.g., blank password use is restricted).");
                        case 1385: // ERROR_LOGON_TYPE_NOT_GRANTED
                            throw new UnauthorizedAccessException(
                                "Authentication succeeded, but 'Network Logon' is denied by security policy. " +
                                "This is common for hardened service accounts and indicates the credentials " +
                                "are likely correct.");
                        default:
                            throw new Win32Exception(error, $"Logon failed with error code {error}.");
                    }
                }
            }
            finally
            {
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
        /// <param name="source">The path to the source file.</param>
        /// <param name="destination">The path to the destination file to replace.</param>
        public static void AtomicSecureMove(string source, string destination)
        {
            if (!MoveFileEx(source, destination, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Failed to atomically replace secure file. Win32 Error: {error}");
            }
        }

        /// <summary>
        /// Extracts the unique <see cref="FileIdentity"/> from an open <see cref="FileStream"/>.
        /// </summary>
        /// <param name="fs">The open file stream.</param>
        /// <returns>A populated identity structure.</returns>
        public static FileIdentity GetFileIdentity(FileStream fs)
        {
            var identity = new FileIdentity();
            try
            {
                if (GetFileInformationByHandle(fs.SafeFileHandle.DangerousGetHandle(), out var info))
                {
                    identity.VolumeSerialNumber = info.VolumeSerialNumber;
                    identity.FileIndex = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
                    identity.IsValidHandleInfo = true;
                }
            }
            catch
            {
                // Fallback or log failure here if necessary
            }

            try
            {
                long origPos = fs.Position;
                fs.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[64];
                int read = fs.Read(buffer, 0, buffer.Length);
                identity.PrefixHash = Convert.ToBase64String(buffer, 0, read);
                fs.Seek(origPos, SeekOrigin.Begin);
            }
            catch
            {
                // Fallback or log failure here if necessary
            }

            return identity;
        }

        #endregion
    }
}

#pragma warning restore SYSLIB1054
#pragma warning restore IDE0079