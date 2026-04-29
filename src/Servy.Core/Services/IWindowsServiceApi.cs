using Servy.Core.Native;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides an abstraction for invoking native Windows Service API functions.
    /// </summary>
    public interface IWindowsServiceApi
    {
        /// <summary>
        /// Opens a connection to the service control manager.
        /// </summary>
        /// <param name="machineName">The target machine name. Use null for the local machine.</param>
        /// <param name="databaseName">The name of the service control manager database. Use null for default.</param>
        /// <param name="dwAccess">The desired access rights.</param>
        /// <returns>A handle to the service control manager.</returns>
        SafeScmHandle OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

        /// <summary>
        /// Ensures the specified account has the "Log on as a service" right.
        /// </summary>
        /// <param name="accountName">
        /// The account to grant the right to. Can be a domain account (DOMAIN\user),
        /// or a local account (.\user or MACHINE_NAME\user).
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the account cannot be resolved to a SID.
        /// </exception>
        void EnsureLogOnAsServiceRight(string accountName);

        /// <summary>
        /// Creates a service object and adds it to the specified service control manager database.
        /// </summary>
        /// <param name="hSCManager">Handle to the service control manager.</param>
        /// <param name="lpServiceName">The name of the service.</param>
        /// <param name="lpDisplayName">The display name of the service.</param>
        /// <param name="dwDesiredAccess">The desired access rights for the service.</param>
        /// <param name="dwServiceType">The type of service.</param>
        /// <param name="dwStartType">The service start option.</param>
        /// <param name="dwErrorControl">The severity of the error if the service fails to start.</param>
        /// <param name="lpBinaryPathName">The fully qualified path to the service binary.</param>
        /// <param name="lpLoadOrderGroup">The load ordering group name.</param>
        /// <param name="lpdwTagId">Receives a tag identifier for ordering.</param>
        /// <param name="lpDependencies">The names of services this service depends on.</param>
        /// <param name="lpServiceStartName">The name of the account under which the service runs.</param>
        /// <param name="lpPassword">The password for the account specified by <paramref name="lpServiceStartName" />.</param>
        /// <returns>A handle to the newly created service.</returns>
        SafeServiceHandle CreateService(
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
            string? lpPassword
        );

        /// <summary>
        /// Opens an existing service.
        /// </summary>
        /// <param name="hSCManager">Handle to the service control manager.</param>
        /// <param name="lpServiceName">The name of the service to open.</param>
        /// <param name="dwDesiredAccess">The access to the service.</param>
        /// <returns>A handle to the service.</returns>
        SafeServiceHandle OpenService(SafeScmHandle hSCManager, string lpServiceName, uint dwDesiredAccess);

        /// <summary>
        /// Marks the specified service for deletion from the service control manager database.
        /// </summary>
        /// <param name="hService">Handle to the service.</param>
        /// <returns><c>true</c> if the operation succeeds; otherwise, <c>false</c>.</returns>
        bool DeleteService(SafeServiceHandle hService);

        /// <summary>
        /// Closes a handle to a service control manager or service object.
        /// </summary>
        /// <param name="hSCObject">Handle to close.</param>
        /// <returns><c>true</c> if the handle is successfully closed; otherwise, <c>false</c>.</returns>
        bool CloseServiceHandle(IntPtr hSCObject);

        /// <summary>
        /// Sends a control code to a service.
        /// </summary>
        /// <param name="hService">Handle to the service.</param>
        /// <param name="dwControl">The control code to send.</param>
        /// <param name="lpServiceStatus">Receives the latest status information about the service.</param>
        /// <returns><c>true</c> if the operation succeeds; otherwise, <c>false</c>.</returns>
        bool ControlService(SafeServiceHandle hService, int dwControl, ref SERVICE_STATUS lpServiceStatus);

        /// <summary>
        /// Changes the configuration parameters of a service.
        /// </summary>
        /// <param name="hService">Handle to the service.</param>
        /// <param name="dwServiceType">The new service type.</param>
        /// <param name="dwStartType">The new start type.</param>
        /// <param name="dwErrorControl">The severity of the error if the service fails to start.</param>
        /// <param name="lpBinaryPathName">The new path to the service binary.</param>
        /// <param name="lpLoadOrderGroup">The new load ordering group.</param>
        /// <param name="lpdwTagId">Receives a tag identifier for ordering.</param>
        /// <param name="lpDependencies">The new dependencies.</param>
        /// <param name="lpServiceStartName">The name of the account under which the service runs.</param>
        /// <param name="lpPassword">The password for the specified account.</param>
        /// <param name="lpDisplayName">The new display name of the service.</param>
        /// <returns><c>true</c> if the configuration is successfully changed; otherwise, <c>false</c>.</returns>
        bool ChangeServiceConfig(
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
            string? lpDisplayName
        );

        /// <summary>
        /// Changes the optional configuration parameters of a service using a description structure.
        /// </summary>
        /// <param name="hService">Handle to the service.</param>
        /// <param name="dwInfoLevel">The information level of the configuration to change.</param>
        /// <param name="lpInfo">A reference to the new configuration information.</param>
        /// <returns><c>true</c> if the configuration is successfully changed; otherwise, <c>false</c>.</returns>
        bool ChangeServiceConfig2(
            SafeServiceHandle hService,
            int dwInfoLevel,
            ref SERVICE_DESCRIPTION lpInfo
        );

        /// <summary>
        /// Changes the optional configuration parameters of a service using a raw pointer.
        /// </summary>
        /// <param name="hService">A handle to the service.</param>
        /// <param name="dwInfoLevel">The configuration information to be changed.</param>
        /// <param name="lpInfo">A pointer to the buffer that contains the new configuration data.</param>
        /// <returns>Returns true if the function succeeds; otherwise, false.</returns>
        bool ChangeServiceConfig2(
            SafeServiceHandle hService,
            int dwInfoLevel,
            IntPtr lpInfo
        );

        /// <summary>
        /// Changes the optional configuration parameters of a service using a delayed auto-start structure.
        /// </summary>
        /// <param name="hService">A handle to the service.</param>
        /// <param name="dwInfoLevel">The configuration information level to be set.</param>
        /// <param name="lpInfo">A reference to the structure containing the configuration data.</param>
        /// <returns><see langword="true"/> if the function succeeds; otherwise, <see langword="false"/>.</returns>
        bool ChangeServiceConfig2(
            SafeServiceHandle hService,
            int dwInfoLevel,
            ref SERVICE_DELAYED_AUTO_START_INFO lpInfo
        );

        /// <summary>
        /// Retrieves the configuration parameters of the specified service.
        /// </summary>
        /// <param name="hService">A handle to the service.</param>
        /// <param name="lpServiceConfig">A pointer to a buffer that receives the configuration information.</param>
        /// <param name="cbBufSize">The size of the buffer, in bytes.</param>
        /// <param name="pcbBytesNeeded">Receives the number of bytes needed if the function fails with ERROR_INSUFFICIENT_BUFFER.</param>
        /// <returns>If the function succeeds, the return value is true. If it fails, the return value is false.</returns>
        bool QueryServiceConfig(
            SafeServiceHandle hService,
            IntPtr lpServiceConfig,
            int cbBufSize,
            out int pcbBytesNeeded);

        /// <summary>
        /// Retrieves optional configuration information for a service using a delayed auto-start structure.
        /// </summary>
        /// <param name="hService">A handle to the service.</param>
        /// <param name="dwInfoLevel">The configuration information level to query.</param>
        /// <param name="lpBuffer">A reference to a structure that receives the configuration information.</param>
        /// <param name="cbBufSize">The size, in bytes, of the buffer pointed to by <paramref name="lpBuffer"/>.</param>
        /// <param name="pcbBytesNeeded">On output, receives the number of bytes required if the buffer is too small.</param>
        /// <returns><see langword="true"/> if the function succeeds; otherwise, <see langword="false"/>.</returns>
        bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            ref SERVICE_DELAYED_AUTO_START_INFO lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        /// <summary>
        /// Retrieves optional configuration information for a service using a raw pointer.
        /// </summary>
        /// <param name="hService">A handle to the service.</param>
        /// <param name="dwInfoLevel">The configuration information level to query (e.g., description).</param>
        /// <param name="lpBuffer">A pointer to the buffer that receives the service configuration information.</param>
        /// <param name="cbBufSize">The size of the buffer, in bytes.</param>
        /// <param name="pcbBytesNeeded">Receives the number of bytes needed if the buffer is too small.</param>
        /// <returns>If the function succeeds, the return value is true. If it fails, the return value is false.</returns>
        bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            IntPtr lpBuffer,
            int cbBufSize,
            ref int pcbBytesNeeded);

        /// <summary>
        /// Gets all installed Windows services on the system.
        /// </summary>
        /// <returns>An enumerable of <see cref="WindowsServiceInfo"/> representing installed services.</returns>
        IEnumerable<WindowsServiceInfo> GetServices();
    }
}