using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

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
        ///<summary>Logon type: Service. Intended for service accounts that run in the background without user interaction.</summary>
        public const int LOGON32_LOGON_SERVICE = 5;

        /// <summary>Snapshot flag: Includes all processes in the system in the snapshot.</summary>
        public const uint TH32CS_SNAPPROCESS = 0x00000002;
        /// <summary>Represents an invalid handle value (-1).</summary>
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>Pseudo-handle for the parent process of the calling process.</summary>
        public const int ATTACH_PARENT_PROCESS = -1;
        /// <summary>UTF-8 code page identifier.</summary>
        public const uint CP_UTF8 = 65001;

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

        /// <summary>Replaces the destination file if it already exists.</summary>
        public const uint MOVEFILE_REPLACE_EXISTING = 0x01;
        /// <summary>Ensures the move operation is flushed to disk before returning.</summary>
        public const uint MOVEFILE_WRITE_THROUGH = 0x08;

        /// <summary>Represents the Win32 error code indicating the provided buffer is too small to contain the data.</summary>
        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        #endregion

        #region SCM Access Rights

        /// <summary>Access right to connect to the Service Control Manager.</summary>
        public const uint SC_MANAGER_CONNECT = 0x0001;

        /// <summary>Access right to create a service object and add it to the database.</summary>
        public const uint SC_MANAGER_CREATE_SERVICE = 0x0002;

        #endregion

        #region Service Access Rights

        /// <summary>Access right to change the configuration of a service.</summary>
        public const uint SERVICE_CHANGE_CONFIG = 0x0002;

        /// <summary>Access right to start the service.</summary>
        public const uint SERVICE_START = 0x0010;

        /// <summary>Access right to stop the service.</summary>
        public const uint SERVICE_STOP = 0x0020;

        /// <summary>Access right to delete the service.</summary>
        public const uint SERVICE_DELETE = 0x00010000;

        #endregion

        #region Service Configuration & Type Flags

        /// <summary>Logs the error and continues the startup operation if the service fails to start.</summary>
        public const uint SERVICE_ERROR_NORMAL = 0x00000001;

        /// <summary>Information level to retrieve or set pre-shutdown information.</summary>
        public const int SERVICE_CONFIG_PRESHUTDOWN_INFO = 7;

        #endregion

        #region Structures & Enums

        /// <summary>Contains a service description string.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_DESCRIPTION
        {
            /// <summary>A pointer to the description string.</summary>
            public IntPtr lpDescription;
        }

        /// <summary>Contains the delayed auto-start setting of an auto-start service.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_DELAYED_AUTO_START_INFO
        {
            /// <summary>If true, the service is started after other auto-start services have finished.</summary>
            [MarshalAs(UnmanagedType.Bool)]
            public bool fDelayedAutostart;
        }

        /// <summary>Contains the pre-shutdown timeout setting for a service.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_PRE_SHUTDOWN_INFO
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
        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;       // ExitStatus
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;     // AffinityMask
            public IntPtr Reserved2_1;     // BasePriority
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId; // Reserved3
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

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BY_HANDLE_FILE_INFORMATION
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
        public struct FILE_IDENTITY
        {
            /// <summary>The unique file index.</summary>
            public ulong FileIndex;

            /// <summary>The volume serial number.</summary>
            public uint VolumeSerialNumber;

            /// <summary>A digest of the start of the file for secondary identification.</summary>
            /// <remarks>Renamed from PrefixHash to reflect its role as a content-based digest.</remarks>
            public string PrefixDigest;

            /// <summary>Indicates if handle-based information was successfully retrieved.</summary>
            public bool IsValidHandleInfo;

            /// <summary>
            /// Compares the current file identity against another to determine if the underlying 
            /// file object on disk has been replaced (rotated) or truncated.
            /// </summary>
            /// <param name="other">The previously known file identity to compare against.</param>
            /// <returns>
            /// <c>true</c> if the files are proven different or if identity is undeterminable; 
            /// <c>false</c> only if they are proven to be the same file object.
            /// </returns>
            public bool IsDifferentFrom(FILE_IDENTITY other)
            {
                // If one probe succeeded and the other failed, they are fundamentally different states.
                if (IsValidHandleInfo != other.IsValidHandleInfo) return true;

                // 1. Primary Probe: Win32 File Index and Volume Serial Number (Most reliable)
                if (IsValidHandleInfo && other.IsValidHandleInfo)
                {
                    if (FileIndex != other.FileIndex || VolumeSerialNumber != other.VolumeSerialNumber)
                        return true;
                    return false;
                }

                // 2. Secondary Probe: Content Prefix Digest (Used when handle info is unavailable, e.g., FAT32)
                if (PrefixDigest != null && other.PrefixDigest != null)
                {
                    // If content digests differ, the file has definitely changed.
                    // If both are empty strings (empty files), we treat them as same content-wise.
                    return PrefixDigest != other.PrefixDigest;
                }

                // 3. Fallback: Identity Undeterminable
                // If we reach this point, both robust probes failed or yielded null data (e.g., due to 
                // exclusive file locks, antivirus interference, or I/O errors).
                //
                // SAFE DEFAULT: We return 'true' to signal a potential difference. This forces the 
                // caller (like LogTailer) to break its inner loop and perform a "soft refresh" via 
                // metadata-guarded re-opening. This prevents masking rotations on hostile file 
                // systems where tunneling hides metadata changes.
                return true;
            }
        }

        #endregion

        #region SCM & Service Functions

        /// <summary>Connects to the Service Control Manager on the specified computer.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeScmHandle OpenSCManager(string machineName, string databaseName, uint dwAccess);

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
          string lpLoadOrderGroup,
          IntPtr lpdwTagId,
          string lpDependencies,
          string lpServiceStartName,
          string lpPassword);

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
        public static extern bool ControlService(SafeServiceHandle hService, int dwControl, ref SERVICE_STATUS lpServiceStatus);

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
            ref SERVICE_DELAYED_AUTO_START_INFO lpBuffer,
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
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName);

        /// <summary>Changes optional service configuration (Description).</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              SafeServiceHandle hService,
              int dwInfoLevel,
              ref SERVICE_DESCRIPTION lpInfo);

        /// <summary>Changes optional service configuration (Delayed Auto Start).</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              SafeServiceHandle hService,
              int dwInfoLevel,
              ref SERVICE_DELAYED_AUTO_START_INFO lpInfo);

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

        #region Process & Snapshot Functions

        /// <summary>Takes a snapshot of specified processes, heaps, modules, and threads.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        /// <summary>Retrieves information about the first process encountered in a snapshot.</summary>
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)]
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>Retrieves information about the next process recorded in a snapshot.</summary>
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>Opens an existing local process object.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeWinProcessHandle OpenProcess(ProcessAccess desiredAccess, bool inheritHandle, int processId);

        /// <summary>Low-level NT function to retrieve process information.</summary>
        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            uint processInformationLength,
            out uint returnLength);

        /// <summary>Internal overload for NtQueryInformationProcess.</summary>
        [DllImport("ntdll.dll")]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            ProcessInfoClass processInformationClass,
            out PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength,
            IntPtr returnLength = default);

        /// <summary>Retrieves the number of milliseconds since the system was started.</summary>
        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        /// <summary>
        /// Parses a Unicode command-line string and returns an array of pointers to the command-line arguments, 
        /// along with a count of such arguments, in a manner similar to standard C run-time argv/argc values.
        /// </summary>
        /// <param name="lpCmdLine">A pointer to a null-terminated Unicode string that contains the full command line.</param>
        /// <param name="pNumArgs">A pointer to an integer that receives the number of array elements returned.</param>
        /// <returns>
        /// If the function succeeds, the return value is a pointer to an array of Unicode string pointers. 
        /// If the function fails, the return value is <see cref="IntPtr.Zero"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The memory allocated for the argument list must be freed by calling <see cref="LocalFree"/> 
        /// using the pointer returned by this function.
        /// </para>
        /// <para>
        /// This method is essential for correctly resolving unquoted service paths that contain spaces 
        /// (e.g., "C:\Program Files\Servy\Servy.Service.exe"), as it mirrors the OS's own argument parsing logic.
        /// </para>
        /// </remarks>
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
            out int pNumArgs);

        /// <summary>Frees the specified local memory object and invalidates its handle.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

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
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleOutputCP(uint codePageID);

        /// <summary>Adds or removes an application-defined console signal handler.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerRoutine handlerRoutine, bool add);

        /// <summary>Sends a specified signal to a console process group.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GenerateConsoleCtrlEvent(CtrlEvents ctrlEvent, uint processGroupId);

        /// <summary>Detaches the calling process from its console.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();

        #endregion

        #region File & Security Functions

        /// <summary>Closes an open object handle.</summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CloseHandle(IntPtr handle);

        /// <summary>Retrieves the final path for the specified file handle.</summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetFinalPathNameByHandle(
           SafeFileHandle hFile, // Use SafeFileHandle here instead of IntPtr
           [Out] StringBuilder lpszFilePath,
           uint cchFilePath,
           uint dwFlags);

        /// <summary>Attempts to log a user on to the local computer.</summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        /// <summary>Renames or moves an existing file or directory, with options to control the move operation.</summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);

        /// <summary>Retrieves file information for the specified file handle.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        #endregion

        #region LogonAsServiceGrant Interop

        /// <summary>Used in LSA calls to represent a Unicode string.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        /// <summary>Specifies attributes of a connection to the Policy object.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDir;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDesc;
            public IntPtr SecurityQos;
        }

        /// <summary>LSA Policy database access rights.</summary>
        public static class POLICY_ACCESS
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
            ref LSA_OBJECT_ATTRIBUTES objectAttributes,
            uint desiredAccess,
            out IntPtr policyHandle);

        /// <summary>Adds rights to an account.</summary>
        [DllImport("advapi32.dll")]
        public static extern int LsaAddAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            LSA_UNICODE_STRING[] userRights,
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
    }
}
