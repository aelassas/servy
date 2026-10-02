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

            if (!ServiceAccounts.IsEligibleForServiceControlGrant(accountName))
            {
                return;
            }

            try
            {
                var sid = LogonAsServiceGrant.AccountToSidOrThrow(accountName);

                bool updated = EditServiceDacl(serviceHandle, acl => BuildGrantedDacl(acl, sid, accountName));

                if (updated)
                {
                    Logger.Info($"Successfully granted service control & status rights to account '{accountName}'.");
                }
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

            if (!ServiceAccounts.IsEligibleForServiceControlGrant(accountName))
            {
                return;
            }

            try
            {
                var sid = LogonAsServiceGrant.AccountToSidOrThrow(accountName);

                bool updated = EditServiceDacl(serviceHandle, acl => BuildRevokedDacl(acl, sid));

                if (updated)
                {
                    Logger.Info($"Successfully revoked service control & status rights from account '{accountName}'.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"CRITICAL: Failed to revoke service control rights for account '{accountName}'.", ex);
            }
        }

        /// <summary>
        /// Reads the service's security descriptor, applies <paramref name="edit"/> to its discretionary access control list (DACL),
        /// and writes the updated descriptor back to the Service Control Manager when <paramref name="edit"/> returns a non-null ACL.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target service opened with <c>READ_CONTROL</c> and <c>WRITE_DAC</c> rights.</param>
        /// <param name="edit">A delegate that modifies the DACL in place (or produces a new one) and returns the ACL to write, or <see langword="null"/> to cancel the write operation.</param>
        /// <returns><see langword="true"/> if an updated security descriptor was written to the service; otherwise <see langword="false"/>.</returns>
        /// <exception cref="Win32Exception">Thrown when querying or setting the service security descriptor fails.</exception>
        [ExcludeFromCodeCoverage]
        private static bool EditServiceDacl(SafeServiceHandle serviceHandle, Func<RawAcl?, RawAcl?> edit)
        {
            // 1. Get required buffer size
            uint bytesNeeded = 0;
            QueryServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, null, 0, out bytesNeeded);

            if (bytesNeeded == 0)
            {
                int err = Marshal.GetLastWin32Error();
                Logger.Warn($"QueryServiceObjectSecurity buffer check returned 0 bytes needed. Win32 error: {err}");
                return false;
            }

            byte[] psd = new byte[bytesNeeded];
            if (!QueryServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, psd, bytesNeeded, out _))
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, $"Failed to query service security descriptor. Win32 error: {err}");
            }

            // 2. Parse Self-Relative Security Descriptor
            var rawSd = new RawSecurityDescriptor(psd, 0);

            // 3. Perform caller-specified DACL modification
            var updatedAcl = edit(rawSd.DiscretionaryAcl);
            if (updatedAcl == null)
            {
                return false;
            }

            rawSd.DiscretionaryAcl = updatedAcl;

            // 4. Convert back to binary self-relative form and apply
            byte[] updatedPsd = new byte[rawSd.BinaryLength];
            rawSd.GetBinaryForm(updatedPsd, 0);

            if (!SetServiceObjectSecurity(serviceHandle, DACL_SECURITY_INFORMATION, updatedPsd))
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, $"Failed to set service security descriptor. Win32 error: {err}");
            }

            return true;
        }

        /// <summary>
        /// Computes the discretionary access control list (DACL) that grants an account the service control and
        /// status rights, by replacing the account's existing Allow entries with a single entry for
        /// <c>SERVICE_CONTROL_AND_STATUS_ACCESS</c>.
        /// </summary>
        /// <param name="acl">
        /// The service's current DACL, edited in place, or <see langword="null"/> when the security descriptor has a
        /// NULL DACL.
        /// </param>
        /// <param name="sid">The account the rights are granted to.</param>
        /// <param name="accountName">The account name, used only in the log message of the NULL DACL case.</param>
        /// <returns>
        /// <paramref name="acl"/> with the account's previous Allow entries replaced by one entry for
        /// <c>SERVICE_CONTROL_AND_STATUS_ACCESS</c>; or <see langword="null"/> when <paramref name="acl"/> is
        /// <see langword="null"/>, so that nothing is written back to the service.
        /// </returns>
        /// <remarks>
        /// This is the body of the edit delegate <see cref="GrantServiceControlRights"/> passes to
        /// <see cref="EditServiceDacl"/>, extracted so that it can be exercised without a live service handle. It is
        /// pure managed code over <see cref="RawAcl"/> and forwards nothing: the grant path calls it with exactly the
        /// ACL the Service Control Manager returned and writes back exactly what it returns.
        /// </remarks>
        internal static RawAcl? BuildGrantedDacl(RawAcl? acl, SecurityIdentifier sid, string accountName)
        {
            if (acl == null)
            {
                // A missing (NULL) DACL already grants every principal full access.
                // Replacing it with a one-entry DACL would inadvertently lock out Administrators and SYSTEM.
                Logger.Warn($"Service security descriptor has a NULL DACL; account '{accountName}' already possesses implicit full control.");
                return null;
            }

            // Remove this SID's existing Allow ACEs if present (prevents duplicate ACE bloat on updates).
            // A Deny ACE an administrator added is left in place, but every Allow ACE for this SID is replaced,
            // including one an administrator added.
            RemoveAllowAces(acl, sid);

            // Insert new explicit Allow ACE
            acl.InsertAce(
                acl.Count,
                new CommonAce(
                    AceFlags.None,
                    AceQualifier.AccessAllowed,
                    (int)SERVICE_CONTROL_AND_STATUS_ACCESS,
                    sid,
                    false,
                    null));

            return acl;
        }

        /// <summary>
        /// Computes the discretionary access control list (DACL) that revokes an account's service control and status
        /// rights, by removing the account's Allow entries and leaving its Deny entries in place.
        /// </summary>
        /// <param name="acl">
        /// The service's current DACL, edited in place, or <see langword="null"/> when the security descriptor has a
        /// NULL DACL.
        /// </param>
        /// <param name="sid">The account whose rights are revoked.</param>
        /// <returns>
        /// <paramref name="acl"/> without the account's Allow entries; or <see langword="null"/>, so that nothing is
        /// written back, when <paramref name="acl"/> is <see langword="null"/> (a NULL DACL grants everyone full access
        /// and must not be replaced by an empty list, which grants nobody anything) or when the account had no Allow
        /// entry to remove.
        /// </returns>
        /// <remarks>
        /// This is the body of the edit delegate <see cref="RevokeServiceControlRights"/> passes to
        /// <see cref="EditServiceDacl"/>, extracted so that it can be exercised without a live service handle. It is
        /// pure managed code over <see cref="RawAcl"/> and forwards nothing: the revocation path calls it with exactly
        /// the ACL the Service Control Manager returned and writes back exactly what it returns.
        /// </remarks>
        internal static RawAcl? BuildRevokedDacl(RawAcl? acl, SecurityIdentifier sid)
        {
            if (acl == null)
            {
                return null;
            }

            // Remove the account's Allow ACEs only, so an administrator's Deny ACE survives the revocation
            return RemoveAllowAces(acl, sid) == 0 ? null : acl;
        }

        /// <summary>
        /// Removes every explicit Allow entry for an account from a discretionary access control list, leaving its
        /// Deny entries in place.
        /// </summary>
        /// <param name="acl">The list to edit in place.</param>
        /// <param name="sid">The account whose Allow entries are removed.</param>
        /// <returns>The number of entries removed.</returns>
        /// <remarks>
        /// Pure managed code over <see cref="RawAcl"/>, so it is <see langword="internal"/> rather than
        /// <see langword="private"/> and carries no <see cref="ExcludeFromCodeCoverageAttribute"/>: it holds the
        /// Allow-only rule that keeps an administrator's Deny entry alive across a grant or a revocation, and both
        /// callers around it need a live service handle.
        /// </remarks>
        internal static int RemoveAllowAces(RawAcl acl, SecurityIdentifier sid)
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
