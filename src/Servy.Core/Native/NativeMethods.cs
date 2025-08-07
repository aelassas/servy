using System.Runtime.InteropServices;

#pragma warning disable IDE0079
#pragma warning disable SYSLIB1054

namespace Servy.Core.Native
{
    /// <summary>
    /// Contains native methods for low-level Windows service operations.
    /// </summary>
    internal static partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_DESCRIPTION
        {
            public IntPtr lpDescription;
        }

        public const int SC_MANAGER_ALL_ACCESS = 0xF003F;
        public const int SERVICE_ALL_ACCESS = 0xF01FF;
        public const int SERVICE_QUERY_STATUS = 0x0004;
        public const int SERVICE_DEMAND_START = 0x00000003;
        public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        public const int SERVICE_CONTROL_STOP = 0x00000001;

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

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]

        public static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

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

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool ControlService(IntPtr hService, int dwControl, ref SERVICE_STATUS lpServiceStatus);

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

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(
              IntPtr hService,
              int dwInfoLevel,
              ref SERVICE_DESCRIPTION lpInfo);
    }
}

#pragma warning restore SYSLIB1054
#pragma warning restore IDE0079
