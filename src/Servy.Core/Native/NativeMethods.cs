using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
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

        /// <summary>Access right to all control manager operations.</summary>
        public const int SC_MANAGER_ALL_ACCESS = 0xF003F;

        /// <summary>Access right to all service operations.</summary>
        public const int SERVICE_ALL_ACCESS = 0xF01FF;

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
        internal static extern bool LogonUser(
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
        internal static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Logon type for interactive logon (user can log on at the computer's console).
        /// </summary>
        internal const int LOGON32_LOGON_INTERACTIVE = 2;

        /// <summary>
        /// Logon type for network login. Logs on the user for network access (no profile loaded).
        /// </summary>
        internal const int LOGON32_LOGON_NETWORK = 3;

        /// <summary>
        /// The default logon provider.
        /// </summary>
        internal const int LOGON32_PROVIDER_DEFAULT = 0;

        /// <summary>
        /// Validates Windows credentials by attempting a logon using the specified username and password.
        /// </summary>
        /// <param name="username">
        /// The username in one of the following formats:
        /// <list type="bullet">
        /// <item><description><c>DOMAIN\Username</c> for domain accounts</description></item>
        /// <item><description><c>.\Username</c> for local accounts on the current machine</description></item>
        /// <item><description><c>DOMAIN\gMSA$</c> for gMSA accounts</description></item>
        /// </list>
        /// </param>
        /// <param name="password">
        /// The password associated with the specified username. 
        /// Can be <c>null</c> or empty if the account has no password.
        /// Password is ignored for gMSA.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when the username is null, empty, or not in a valid format.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown if the provided credentials are invalid or the account cannot log on.
        /// </exception>
        /// <exception cref="Win32Exception">
        /// Thrown for other system-related logon errors.
        /// </exception>
        /// <remarks>
        /// This method uses the Windows API function <c>LogonUser</c> to verify that the provided credentials 
        /// are valid for a network logon. It does not check whether the account has the 
        /// "Log on as a service" right, which may be required for running a Windows service.
        /// </remarks>
        public static void ValidateCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.");

            // Regex pattern:
            // Matches:
            // - DOMAIN\User (domain and username separated by \)
            // - .\User (local machine)
            const string pattern = @"^(?:[\w\s\.-]+|\.)\\[\w\s\.@!-]+\$?$";
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
                        case 1326:
                            throw new UnauthorizedAccessException("Invalid username or password.");
                        case 1327:
                            throw new UnauthorizedAccessException("Account restrictions prevent logon.");
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

    }
}

#pragma warning restore SYSLIB1054
#pragma warning restore IDE0079
