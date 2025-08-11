using System;
using System.Runtime.InteropServices;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Interfaces
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
        IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

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
        IntPtr CreateService(
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
            string lpPassword
        );

        /// <summary>
        /// Opens an existing service.
        /// </summary>
        /// <param name="hSCManager">Handle to the service control manager.</param>
        /// <param name="lpServiceName">The name of the service to open.</param>
        /// <param name="dwDesiredAccess">The access to the service.</param>
        /// <returns>A handle to the service.</returns>
        IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        /// <summary>
        /// Marks the specified service for deletion from the service control manager database.
        /// </summary>
        /// <param name="hService">Handle to the service.</param>
        /// <returns><c>true</c> if the operation succeeds; otherwise, <c>false</c>.</returns>
        bool DeleteService(IntPtr hService);

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
        bool ControlService(IntPtr hService, int dwControl, ref SERVICE_STATUS lpServiceStatus);

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
            string lpDisplayName
        );

        /// <summary>
        /// Changes the optional configuration parameters of a service.
        /// </summary>
        /// <param name="hService">Handle to the service.</param>
        /// <param name="dwInfoLevel">The information level of the configuration to change.</param>
        /// <param name="lpInfo">A reference to the new configuration information.</param>
        /// <returns><c>true</c> if the configuration is successfully changed; otherwise, <c>false</c>.</returns>
        bool ChangeServiceConfig2(
            IntPtr hService,
            int dwInfoLevel,
            ref SERVICE_DESCRIPTION lpInfo
        );


        /// <summary>
        /// Validates a given Windows username and password by attempting a logon.
        /// </summary>
        /// <param name="username">
        /// The username in the format <c>DOMAIN\Username</c>, <c>.\Username</c> for local accounts, 
        /// or just <c>Username</c> for local accounts without a domain prefix.
        /// </param>
        /// <param name="password">
        /// The password associated with the specified username. Can be <c>null</c> or empty if the account has no password.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when the username is null or empty.
        /// </exception>
        /// <exception cref="Win32Exception">
        /// Thrown if the credentials are invalid or the account cannot log on interactively.
        /// </exception>
        /// <remarks>
        /// This method uses the Windows API <c>LogonUser</c> function to verify that the provided credentials 
        /// are valid for interactive logon. It does not check whether the account has the 
        /// "Log on as a service" right, which may be required to run a service.
        /// </remarks>
        void ValidateCredentials(string username, string password);
    }
}
