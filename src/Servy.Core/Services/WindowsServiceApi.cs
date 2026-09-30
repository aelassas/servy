using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class WindowsServiceApi : IWindowsServiceApi
    {
        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public SafeScmHandle OpenSCManager(string? machineName, string? databaseName, uint dwAccess)
            => NativeMethods.OpenSCManager(
                machineName: machineName,
                databaseName: databaseName,
                dwAccess: dwAccess);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public void EnsureLogOnAsServiceRight(string accountName)
            => LogonAsServiceGrant.Ensure(accountName);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public void GrantServiceControlRights(SafeServiceHandle? serviceHandle, string accountName)
        {
            if (serviceHandle == null || serviceHandle.IsInvalid || serviceHandle.IsClosed)
            {
                throw new ArgumentException("Service handle is null, invalid, or closed.", nameof(serviceHandle));
            }

            if (string.IsNullOrWhiteSpace(accountName) || ServiceAccounts.IsBuiltInServiceAccount(accountName))
            {
                return;
            }

            try
            {
                var sid = LogonAsServiceGrant.AccountToSidOrThrow(accountName);

                // 1. Get required buffer size
                uint bytesNeeded = 0;
                QueryServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, null, 0, out bytesNeeded);

                if (bytesNeeded == 0)
                {
                    int err = Marshal.GetLastWin32Error();
                    Logger.Warn($"QueryServiceObjectSecurity buffer check returned 0 bytes needed. Win32 error: {err}");
                    return;
                }

                byte[] psd = new byte[bytesNeeded];
                if (!QueryServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, psd, bytesNeeded, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"Failed to query service security descriptor. Win32 error: {err}");
                }

                // 2. Parse Self-Relative Security Descriptor
                var rawSd = new RawSecurityDescriptor(psd, 0);

                if (rawSd.DiscretionaryAcl == null)
                {
                    rawSd.DiscretionaryAcl = new RawAcl(GenericAcl.AclRevision, 1);
                }

                // 3. Remove this SID's existing Allow ACEs if present (prevents duplicate ACE bloat on updates).
                //    A Deny ACE an administrator added is left in place: only the grant written here is replaced.
                RemoveAllowAces(rawSd.DiscretionaryAcl, sid);

                // 4. Insert new explicit Allow ACE
                rawSd.DiscretionaryAcl.InsertAce(
                    rawSd.DiscretionaryAcl.Count,
                    new CommonAce(
                        AceFlags.None,
                        AceQualifier.AccessAllowed,
                        (int)SERVICE_CONTROL_AND_STATUS_ACCESS,
                        sid,
                        false,
                        null));

                // 5. Convert back to binary self-relative form
                byte[] updatedPsd = new byte[rawSd.BinaryLength];
                rawSd.GetBinaryForm(updatedPsd, 0);

                if (!SetServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, updatedPsd))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"Failed to set service security descriptor. Win32 error: {err}");
                }

                Logger.Info($"Successfully granted service control & status rights to account '{accountName}'.");
            }
            catch (Exception ex)
            {
                Logger.Error($"CRITICAL: Failed to grant service control rights for account '{accountName}'.", ex);
            }
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public void RevokeServiceControlRights(SafeServiceHandle? serviceHandle, string accountName)
        {
            if (serviceHandle == null || serviceHandle.IsInvalid || serviceHandle.IsClosed)
            {
                throw new ArgumentException("Service handle is null, invalid, or closed.", nameof(serviceHandle));
            }

            if (string.IsNullOrWhiteSpace(accountName) || ServiceAccounts.IsBuiltInServiceAccount(accountName))
            {
                return;
            }

            try
            {
                var sid = LogonAsServiceGrant.AccountToSidOrThrow(accountName);

                // 1. Get required buffer size
                uint bytesNeeded = 0;
                QueryServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, null, 0, out bytesNeeded);

                if (bytesNeeded == 0)
                {
                    int err = Marshal.GetLastWin32Error();
                    Logger.Warn($"QueryServiceObjectSecurity buffer check returned 0 bytes needed. Win32 error: {err}");
                    return;
                }

                byte[] psd = new byte[bytesNeeded];
                if (!QueryServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, psd, bytesNeeded, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"Failed to query service security descriptor. Win32 error: {err}");
                }

                // 2. Parse Self-Relative Security Descriptor
                var rawSd = new RawSecurityDescriptor(psd, 0);

                if (rawSd.DiscretionaryAcl == null)
                {
                    return;
                }

                // 3. Remove the account's Allow ACEs only, so an administrator's Deny ACE survives the revocation
                if (RemoveAllowAces(rawSd.DiscretionaryAcl, sid) == 0)
                {
                    return;
                }

                // 4. Convert back to binary self-relative form
                byte[] updatedPsd = new byte[rawSd.BinaryLength];
                rawSd.GetBinaryForm(updatedPsd, 0);

                if (!SetServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, updatedPsd))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"Failed to set service security descriptor. Win32 error: {err}");
                }

                Logger.Info($"Successfully revoked service control & status rights from account '{accountName}'.");
            }
            catch (Exception ex)
            {
                Logger.Error($"CRITICAL: Failed to revoke service control rights for account '{accountName}'.", ex);
            }
        }

        /// <summary>
        /// Removes every explicit Allow entry for an account from a discretionary access control list, leaving its
        /// Deny entries in place.
        /// </summary>
        /// <param name="acl">The list to edit in place.</param>
        /// <param name="sid">The account whose Allow entries are removed.</param>
        /// <returns>The number of entries removed.</returns>
        [ExcludeFromCodeCoverage]
        private static int RemoveAllowAces(RawAcl acl, SecurityIdentifier sid)
        {
            int removed = 0;
            for (int i = acl.Count - 1; i >= 0; i--)
            {
                if (acl[i] is CommonAce ace
                    && ace.AceQualifier == AceQualifier.AccessAllowed
                    && ace.SecurityIdentifier == sid)
                {
                    acl.RemoveAce(i);
                    removed++;
                }
            }

            return removed;
        }

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
            string? lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string? lpDependencies,
            string? lpServiceStartName,
            string? lpPassword)
            => NativeMethods.CreateService(
                hSCManager: hSCManager,
                lpServiceName: lpServiceName,
                lpDisplayName: lpDisplayName,
                dwDesiredAccess: dwDesiredAccess,
                dwServiceType: dwServiceType,
                dwStartType: dwStartType,
                dwErrorControl: dwErrorControl,
                lpBinaryPathName: lpBinaryPathName,
                lpLoadOrderGroup: lpLoadOrderGroup,
                lpdwTagId: lpdwTagId,
                lpDependencies: lpDependencies,
                lpServiceStartName: lpServiceStartName,
                lpPassword: lpPassword);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public SafeServiceHandle OpenService(SafeScmHandle hSCManager, string lpServiceName, uint dwDesiredAccess)
            => NativeMethods.OpenService(
                hSCManager: hSCManager,
                lpServiceName: lpServiceName,
                dwDesiredAccess: dwDesiredAccess);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool DeleteService(SafeServiceHandle hService)
            => NativeMethods.DeleteService(hService);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ControlService(SafeServiceHandle hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus)
            => NativeMethods.ControlService(
                hService: hService,
                dwControl: dwControl,
                lpServiceStatus: ref lpServiceStatus);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig(
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
            string? lpDisplayName)
            => NativeMethods.ChangeServiceConfig(
                hService: hService,
                dwServiceType: dwServiceType,
                dwStartType: dwStartType,
                dwErrorControl: dwErrorControl,
                lpBinaryPathName: lpBinaryPathName,
                lpLoadOrderGroup: lpLoadOrderGroup,
                lpdwTagId: lpdwTagId,
                lpDependencies: lpDependencies,
                lpServiceStartName: lpServiceStartName,
                lpPassword: lpPassword,
                lpDisplayName: lpDisplayName);

        // --- ChangeServiceConfig2 Overloads ---

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig2(SafeServiceHandle hService, uint dwInfoLevel, ref SERVICE_DESCRIPTION lpInfo)
            => NativeMethods.ChangeServiceConfig2(
                hService: hService,
                dwInfoLevel: dwInfoLevel,
                lpInfo: ref lpInfo);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig2(SafeServiceHandle hService, uint dwInfoLevel, ref SERVICE_DELAYED_AUTO_START_INFO lpInfo)
            => NativeMethods.ChangeServiceConfig2(
                hService: hService,
                dwInfoLevel: dwInfoLevel,
                lpInfo: ref lpInfo);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool ChangeServiceConfig2(SafeServiceHandle hService, uint dwInfoLevel, IntPtr lpInfo)
            => NativeMethods.ChangeServiceConfig2(
                hService: hService,
                dwInfoLevel: dwInfoLevel,
                lpInfo: lpInfo);

        // --- QueryServiceConfig Overloads ---

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool QueryServiceConfig(
            SafeServiceHandle hService,
            IntPtr lpServiceConfig,
            int cbBufSize,
            out int pcbBytesNeeded)
            => NativeMethods.QueryServiceConfig(
                hService: hService,
                lpServiceConfig: lpServiceConfig,
                cbBufSize: cbBufSize,
                pcbBytesNeeded: out pcbBytesNeeded);

        // --- QueryServiceConfig2 Overloads ---

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            ref SERVICE_DELAYED_AUTO_START_INFO lpBuffer,
            int cbBufSize,
            out int pcbBytesNeeded)
            => NativeMethods.QueryServiceConfig2(
                hService: hService,
                dwInfoLevel: dwInfoLevel,
                lpBuffer: ref lpBuffer,
                cbBufSize: cbBufSize,
                pcbBytesNeeded: out pcbBytesNeeded);

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public bool QueryServiceConfig2(
            SafeServiceHandle hService,
            uint dwInfoLevel,
            IntPtr lpBuffer,
            int cbBufSize,
            out int pcbBytesNeeded)
            => NativeMethods.QueryServiceConfig2(
                hService: hService,
                dwInfoLevel: dwInfoLevel,
                lpBuffer: lpBuffer,
                cbBufSize: cbBufSize,
                pcbBytesNeeded: out pcbBytesNeeded);

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
