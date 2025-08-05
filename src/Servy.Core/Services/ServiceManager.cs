using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace Servy.Core
{
    /// <summary>
    /// Provides methods to install, uninstall, start, stop, and restart Windows services.
    /// </summary>
    public class ServiceManager: IServiceManager
    {
        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_QUERY_CONFIG = 0x0001;
        private const uint SERVICE_CHANGE_CONFIG = 0x0002;
        private const uint SERVICE_START = 0x0010;
        private const uint SERVICE_STOP = 0x0020;
        private const uint SERVICE_DELETE = 0x00010000;
        private const int SERVICE_CONFIG_DESCRIPTION = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_DESCRIPTION
        {
            public IntPtr lpDescription;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateService(
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

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ChangeServiceConfig2(
            IntPtr hService,
            int dwInfoLevel,
            ref SERVICE_DESCRIPTION lpInfo);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ChangeServiceConfig(
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

        /// <inheritdoc />
        public bool InstallService(
            string serviceName,
            string description,
            string wrapperExePath,
            string realExePath,
            string workingDirectory,
            string realArgs,
            ServiceStartType startType,
            ProcessPriority processPriority,
            string stdoutPath,
            string stderrPath,
            int rotationSizeInBytes,
            int heartbeatInterval,
            int maxFailedChecks,
            RecoveryAction recoveryAction,
            int maxRestartAttempts
            )
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentNullException(nameof(serviceName));
            if (string.IsNullOrWhiteSpace(wrapperExePath))
                throw new ArgumentNullException(nameof(wrapperExePath));
            if (string.IsNullOrWhiteSpace(realExePath))
                throw new ArgumentNullException(nameof(realExePath));

            // Compose the binary path with the wrapper exe and the parameters for the real exe and working directory
            string binPath = string.Join(" ",
                Helper.Quote(wrapperExePath),
                Helper.Quote(realExePath),
                Helper.Quote(realArgs),
                Helper.Quote(workingDirectory),
                Helper.Quote(processPriority.ToString()),
                Helper.Quote(stdoutPath),
                Helper.Quote(stderrPath),
                Helper.Quote(rotationSizeInBytes.ToString()),
                Helper.Quote(heartbeatInterval.ToString()),
                Helper.Quote(maxFailedChecks.ToString()),
                Helper.Quote(recoveryAction.ToString()),
                Helper.Quote(serviceName),
                Helper.Quote(maxRestartAttempts.ToString())
                );

            IntPtr scmHandle = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scmHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open Service Control Manager.");

            IntPtr serviceHandle = IntPtr.Zero;
            try
            {
                serviceHandle = CreateService(
                    scmHandle,
                    serviceName,
                    serviceName,
                    SERVICE_START | SERVICE_STOP | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_DELETE,
                    SERVICE_WIN32_OWN_PROCESS,
                    (uint)startType,
                    SERVICE_ERROR_NORMAL,
                    binPath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null);

                if (serviceHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();

                    // If service exists, update config instead
                    if (err == 1073) // ERROR_SERVICE_EXISTS
                    {
                        return UpdateServiceConfig(scmHandle, serviceName, description, binPath, startType);
                    }

                    throw new Win32Exception(err, "Failed to create service.");
                }

                SetServiceDescription(serviceHandle, description);

                return true;
            }
            finally
            {
                if (serviceHandle != IntPtr.Zero)
                    CloseServiceHandle(serviceHandle);
                if (scmHandle != IntPtr.Zero)
                    CloseServiceHandle(scmHandle);
            }
        }

        /// <summary>
        /// Updates the configuration of an existing service.
        /// </summary>
        /// <param name="scmHandle">Handle to the Service Control Manager.</param>
        /// <param name="serviceName">The service name.</param>
        /// <param name="description">The service description.</param>
        /// <param name="binPath">The path to the service executable.</param>
        /// <param name="startType">The service startup type.</param>
        /// <returns>True if the update succeeded; otherwise false.</returns>
        /// <exception cref="Win32Exception">Thrown on Win32 errors.</exception>
        private bool UpdateServiceConfig(
            IntPtr scmHandle,
            string serviceName,
            string description,
            string binPath,
            ServiceStartType startType)
        {
            IntPtr serviceHandle = OpenService(
                scmHandle,
                serviceName,
                SERVICE_CHANGE_CONFIG | SERVICE_QUERY_CONFIG);

            if (serviceHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open existing service.");

            try
            {
                bool result = ChangeServiceConfig(
                    serviceHandle,
                    SERVICE_WIN32_OWN_PROCESS,
                    (uint)startType,
                    SERVICE_ERROR_NORMAL,
                    binPath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    null);

                if (!result)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to update service config.");

                SetServiceDescription(serviceHandle, description);

                return true;
            }
            finally
            {
                CloseServiceHandle(serviceHandle);
            }
        }

        /// <summary>
        /// Sets the description for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">Handle to the service.</param>
        /// <param name="description">The description text.</param>
        private void SetServiceDescription(IntPtr serviceHandle, string description)
        {
            if (string.IsNullOrEmpty(description))
                return;

            var desc = new SERVICE_DESCRIPTION
            {
                lpDescription = Marshal.StringToHGlobalUni(description)
            };

            if (!ChangeServiceConfig2(serviceHandle, SERVICE_CONFIG_DESCRIPTION, ref desc))
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "Failed to set service description.");
            }

            Marshal.FreeHGlobal(desc.lpDescription);
        }

        /// <inheritdoc />
        public bool UninstallService(string serviceName)
        {
            IntPtr scmHandle = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
            if (scmHandle == IntPtr.Zero)
                return false;

            try
            {
                IntPtr serviceHandle = NativeMethods.OpenService(scmHandle, serviceName, NativeMethods.SERVICE_ALL_ACCESS);
                if (serviceHandle == IntPtr.Zero)
                    return false;

                try
                {
                    // Change start type to demand start (if it's disabled)
                    NativeMethods.ChangeServiceConfig(
                        serviceHandle,
                        NativeMethods.SERVICE_NO_CHANGE,
                        NativeMethods.SERVICE_DEMAND_START,
                        NativeMethods.SERVICE_NO_CHANGE,
                        null,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null);

                    // Try to stop service
                    var status = new NativeMethods.SERVICE_STATUS();
                    NativeMethods.ControlService(serviceHandle, NativeMethods.SERVICE_CONTROL_STOP, ref status);

                    // Give it some time to stop
                    Thread.Sleep(2000);

                    // Delete the service
                    return NativeMethods.DeleteService(serviceHandle);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(serviceHandle);
                }
            }
            finally
            {
                // OpenSCManager returns a handle to the Service Control Manager.
                // OpenService returns a handle to the individual service.
                // These are two different resources that must each be closed separately to avoid leaking handles.
                NativeMethods.CloseServiceHandle(scmHandle);
            }
        }

        /// <inheritdoc />
        public bool StartService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return true;

                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool StopService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return true;

                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool RestartService(string serviceName)
        {
            try
            {
                if (!StopService(serviceName))
                    return false;

                return StartService(serviceName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Contains native methods for low-level Windows service operations.
        /// </summary>
        internal static class NativeMethods
        {
            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

            public const int SC_MANAGER_ALL_ACCESS = 0xF003F;
            public const int SERVICE_ALL_ACCESS = 0xF01FF;
            public const int SERVICE_QUERY_STATUS = 0x0004;
            public const int SERVICE_DEMAND_START = 0x00000003;
            public const int SERVICE_NO_CHANGE = -1;
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

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern IntPtr OpenSCManager(string machineName, string databaseName, int dwAccess);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, int dwDesiredAccess);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool DeleteService(IntPtr hService);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool CloseServiceHandle(IntPtr hSCObject);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool ControlService(IntPtr hService, int dwControl, ref SERVICE_STATUS lpServiceStatus);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern bool ChangeServiceConfig(
                IntPtr hService,
                int nServiceType,
                int nStartType,
                int nErrorControl,
                string lpBinaryPathName,
                string lpLoadOrderGroup,
                IntPtr lpdwTagId,
                string lpDependencies,
                string lpServiceStartName,
                string lpPassword,
                string lpDisplayName);
        }
    }
}
