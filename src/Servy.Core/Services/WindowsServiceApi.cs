using Servy.Core.Interfaces;
using Servy.Core.Native;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class WindowsServiceApi : IWindowsServiceApi
    {
        /// <inheritdoc />
        public IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess)
            => NativeMethods.OpenSCManager(machineName, databaseName, dwAccess);

        /// <inheritdoc />
        public IntPtr CreateService(
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
            string lpPassword)
            => NativeMethods.CreateService(
                hSCManager,
                lpServiceName,
                lpDisplayName,
                dwDesiredAccess,
                dwServiceType,
                dwStartType,
                dwErrorControl,
                lpBinaryPathName,
                lpLoadOrderGroup,
                lpdwTagId,
                lpDependencies,
                lpServiceStartName,
                lpPassword);

        /// <inheritdoc />
        public IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess)
            => NativeMethods.OpenService(hSCManager, lpServiceName, dwDesiredAccess);

        /// <inheritdoc />
        public bool DeleteService(IntPtr hService)
            => NativeMethods.DeleteService(hService);

        /// <inheritdoc />
        public bool CloseServiceHandle(IntPtr hSCObject)
            => NativeMethods.CloseServiceHandle(hSCObject);

        /// <inheritdoc />
        public bool ControlService(IntPtr hService, int dwControl, ref SERVICE_STATUS lpServiceStatus)
            => NativeMethods.ControlService(hService, dwControl, ref lpServiceStatus);

        /// <inheritdoc />
        public bool ChangeServiceConfig(
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
            string lpDisplayName)
            => NativeMethods.ChangeServiceConfig(
                hService,
                dwServiceType,
                dwStartType,
                dwErrorControl,
                lpBinaryPathName,
                lpLoadOrderGroup,
                lpdwTagId,
                lpDependencies,
                lpServiceStartName,
                lpPassword,
                lpDisplayName);

        /// <inheritdoc />
        public bool ChangeServiceConfig2(IntPtr hService, int dwInfoLevel, ref SERVICE_DESCRIPTION lpInfo)
            => NativeMethods.ChangeServiceConfig2(hService, dwInfoLevel, ref lpInfo);
    }
}
