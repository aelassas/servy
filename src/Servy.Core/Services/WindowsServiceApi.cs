using Servy.Core.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.ServiceProcess;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class WindowsServiceApi : IWindowsServiceApi
    {
        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public SafeScmHandle OpenSCManager(string machineName, string databaseName, uint dwAccess)
            => NativeMethods.OpenSCManager(machineName, databaseName, dwAccess);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public void EnsureLogOnAsServiceRight(string accountName)
            => LogonAsServiceGrant.Ensure(accountName);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public SafeServiceHandle CreateService(
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
        [ExcludeFromCodeCoverage]
        public SafeServiceHandle OpenService(SafeScmHandle hSCManager, string lpServiceName, uint dwDesiredAccess)
            => NativeMethods.OpenService(hSCManager, lpServiceName, dwDesiredAccess);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool DeleteService(SafeServiceHandle hService)
            => NativeMethods.DeleteService(hService);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ControlService(SafeServiceHandle hService, int dwControl, ref SERVICE_STATUS lpServiceStatus)
            => NativeMethods.ControlService(hService, dwControl, ref lpServiceStatus);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig(
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

        // --- ChangeServiceConfig2 Overloads ---

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig2(SafeServiceHandle hService, uint dwInfoLevel, ref SERVICE_DESCRIPTION lpInfo)
            => NativeMethods.ChangeServiceConfig2(hService, dwInfoLevel, ref lpInfo);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig2(SafeServiceHandle hService, uint dwInfoLevel, ref SERVICE_DELAYED_AUTO_START_INFO lpInfo)
            => NativeMethods.ChangeServiceConfig2(hService, dwInfoLevel, ref lpInfo);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig2(SafeServiceHandle hService, uint dwInfoLevel, IntPtr lpInfo)
            => NativeMethods.ChangeServiceConfig2(hService, dwInfoLevel, lpInfo);

        // --- QueryServiceConfig Overloads ---

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool QueryServiceConfig(
            SafeServiceHandle hService,
            IntPtr lpServiceConfig,
            int cbBufSize,
            out int pcbBytesNeeded)
            => NativeMethods.QueryServiceConfig(
                hService,
                lpServiceConfig,
                cbBufSize,
                out pcbBytesNeeded);

        // --- QueryServiceConfig2 Overloads ---

        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            ref SERVICE_DELAYED_AUTO_START_INFO lpBuffer,
            int cbBufSize,
            out int pcbBytesNeeded)
            => NativeMethods.QueryServiceConfig2(hService, dwInfoLevel, ref lpBuffer, cbBufSize, out pcbBytesNeeded);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            IntPtr lpBuffer,
            int cbBufSize,
            out int pcbBytesNeeded)
            => NativeMethods.QueryServiceConfig2(
                hService,
                dwInfoLevel,
                lpBuffer,
                cbBufSize,
                out pcbBytesNeeded);

        /// <inheritdoc />
        public IEnumerable<WindowsServiceInfo> GetServices()
        {
            // DRY Unification: Outsource handle tracking and extraction mechanics to the central mapping pipeline
            return ServiceControllerProvider.MapAndDisposeServices(s => new WindowsServiceInfo
            {
                ServiceName = s.ServiceName,
                DisplayName = s.DisplayName
            });
        }
    }
}