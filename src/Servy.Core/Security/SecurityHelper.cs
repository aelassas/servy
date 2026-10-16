using Servy.Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides utility methods for managing filesystem security and access control lists (ACLs).
    /// </summary>
    public static class SecurityHelper
    {
        /// <summary>
        /// The well-known principals that stand for "any standard user" in Servy's DACL audits.
        /// </summary>
        /// <remarks>
        /// Both the file hardening in <see cref="ServyExePermissionsHardener"/> and the install-directory audit in
        /// <c>PathSecurityGuard.WarnIfDirectoryAclNotHardened</c> decide who counts as a standard user, so the
        /// set is declared once here rather than rebuilt at each site.
        /// </remarks>
        public static readonly IReadOnlyList<SecurityIdentifier> BroadUnprivilegedSids = new[]
        {
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),       // Users
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), // Authenticated Users
            new SecurityIdentifier(WellKnownSidType.WorldSid, null)              // Everyone
        };

        /// <summary>
        /// Determines whether a SID is Everyone, Users or Authenticated Users.
        /// </summary>
        /// <param name="sid">The SID to test.</param>
        /// <returns><see langword="true"/> for one of the <see cref="BroadUnprivilegedSids"/>.</returns>
        public static bool IsBroadUnprivilegedSid(SecurityIdentifier sid)
            => BroadUnprivilegedSids.Any(broad => broad.Equals(sid));

        /// <summary>
        /// Ensures a directory exists and applies a restrictive security descriptor to mitigate
        /// local privilege escalation (LPE) risks by limiting access to high-privileged accounts.
        /// </summary>
        /// <param name="path">The full filesystem path of the directory to secure.</param>
        /// <param name="breakInheritance">
        /// <see langword="true"/> to sever inheritance from the parent directory (establishing a Root Vault).
        /// <see langword="false"/> to ensure inheritance remains active, allowing parent ACLs to cascade down before optimization.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method acts as a security enforcement point with dual initialization strategies based on the inheritance boundary:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <b>Atomic Creation (<c>breakInheritance: true</c>):</b> If the directory is missing, it is created atomically
        /// with the severe security descriptor pre-applied. This eliminates race windows where the folder could briefly
        /// exist with loose inherited permissions.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Progressive Initialization (<c>breakInheritance: false</c>):</b> If the directory is missing, it is initialized
        /// normally first. This allows the Windows kernel to naturally compute and bind the parent directory's cascading inheritance
        /// maps prior to executing the in-place DACL optimization pass.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// For existing directories, or immediately following initialization, the DACL is modified in-place to purge every
        /// explicit entry for a principal other than <c>Administrators</c> and <c>Local System</c> via
        /// <see cref="ApplySecurityRules"/>. An elevated process also hands the directory to <c>BUILTIN\Administrators</c>
        /// when another principal owns it, because an owner keeps an implicit right to rewrite the DACL and could grant
        /// itself access again (#7526).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the process lacks sufficient privileges, in two cases:
        /// (a) Root Vault creation (breakInheritance: true on a missing directory), which is never
        /// caught, and (b) any failure in an elevated process, since both fallbacks are gated on
        /// !IsAdministrator(). A non-administrator gets a logged warning instead of an exception for
        /// a failed non-Root-Vault directory creation and for a failed in-place ACL write on an
        /// existing directory, whatever the value of breakInheritance.
        /// </exception>
        /// <exception cref="IOException">Thrown when a general I/O error occurs during directory access.</exception>
        public static void CreateSecureDirectory(string path, bool breakInheritance = true)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            SecurityIdentifier? currentUserSid = null;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                currentUserSid = identity.User;
            }

            if (!Directory.Exists(path))
            {
                // ATOMIC CREATION BOUNDARY: Only enforce atomic security descriptors upfront
                // if we are actively building a Root Vault (breaking inheritance).
                if (breakInheritance)
                {
                    var ds = new DirectorySecurity();

                    // Apply the hardened rules to the descriptor before creation.
                    ApplySecurityRules(ds, currentUserSid, breakInheritance: true);

                    // Create the directory with the hardened security descriptor in a single operation.
                    FileSystemAclExtensions.CreateDirectory(ds, path);
                    return;
                }

                // INHERITANCE: If breakInheritance is false, create a standard directory first.
                // This allows Windows to naturally bind and compute the parent's cascading inheritance maps
                // correctly before we perform our subsequent in-place DACL optimization pass.
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (UnauthorizedAccessException ex) when (!IsAdministrator())
                {
                    HandleNonAdminFallback(ex, $"Could not create directory '{path}' as non-admin; it does not exist and no ACL was applied. Create it from an elevated process.");

                    // Verify whether the directory was created before deciding to throw
                    if (!Directory.Exists(path))
                    {
                        throw;
                    }
                }
            }

            // EXISTING OR INHERITANCE-INITIALIZED DIRECTORY: Harden in-place.
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(path);

                // CRITICAL: Explicitly request only the Access rules (DACL), plus the owner when elevated.
                bool isAdmin = IsAdministrator();
                var security = dirInfo.GetAccessControl(isAdmin ? AccessControlSections.Access | AccessControlSections.Owner : AccessControlSections.Access);

                // A directory a standard user created before Servy did stays theirs, and its owner can rewrite the DACL
                if (isAdmin)
                {
                    var owner = security.GetOwner(typeof(SecurityIdentifier));
                    if (owner == null || !(owner.Equals(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)) ||
                                           owner.Equals(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null))))
                    {
                        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
                    }
                }

                // Apply the hardened rules.
                ApplySecurityRules(security, currentUserSid, breakInheritance);

                dirInfo.SetAccessControl(security);
            }
            catch (UnauthorizedAccessException ex) when (!IsAdministrator())
            {
                HandleNonAdminFallback(ex, $"Could not write hardened ACL on '{path}' as non-admin. Assuming root/parent vault was previously secured by Administrator.");
            }
        }

        /// <summary>
        /// Configures a <see cref="FileSystemSecurity"/> object with restrictive rules,
        /// purging every non-administrative explicit entry and optionally managing inheritance boundaries.
        /// </summary>
        /// <param name="security">The file or directory security descriptor to modify.</param>
        /// <param name="currentUserSid">
        /// The SID of the current user to conditionally grant access to.
        /// Usually retrieved via <see cref="WindowsIdentity.User"/>.
        /// </param>
        /// <param name="breakInheritance">
        /// If <see langword="true"/>, severs the link to the parent directory's permissions.
        /// If <see langword="false"/>, restores inheritance to allow parent ACLs to flow down.
        /// </param>
        /// <remarks>
        /// This method performs the following security operations:
        /// <list type="number">
        /// <item>
        /// <description>
        /// <b>Inheritance Management:</b> Either blocks inheritance (Root Vault) or enables it (Child Folder)
        /// to support multi-account setups.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Full Purge:</b> Removes every explicit <c>Allow</c> and <c>Deny</c> entry, whatever its principal, so an entry
        /// a standard user added to a directory it created before Servy did cannot survive (#7526). The service
        /// accounts' own grants are written by <see cref="ServyExePermissionsHardener"/> on the files it hardens and on
        /// each service's log folder, which it grants after securing it with this method.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Mandatory Access:</b> Grants <c>Full Control</c> to <c>Administrators</c> and <c>Local System</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Operational Continuity:</b> Grants <c>Full Control</c> to the current process user when the
        /// process is not running elevated and the user is not the LocalSystem account. This is an
        /// elevation check (<see cref="IsAdministrator"/>), not a group-membership check: an administrator
        /// running unelevated has a filtered token and therefore does receive an explicit ACE.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static void ApplySecurityRules(FileSystemSecurity security, SecurityIdentifier? currentUserSid, bool breakInheritance = true)
        {
            // 1. Manage Inheritance Boundaries
            if (breakInheritance)
            {
                // Root Vault: Block permissions from flowing down from %ProgramData%
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            }
            else
            {
                // Child Folder: Re-enable inheritance to heal folders broken by older versions
                // and allow custom service accounts to flow down from the root vault.
                security.SetAccessRuleProtection(isProtected: false, preserveInheritance: true);
            }

            // 2. Define SIDs
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            // 3. PURGE: Replace the DACL entirely. Every explicit rule goes, Allow or Deny, whatever its principal;
            // the mandatory rules are added back below. The inherited rules are left to the inheritance setting above.
            var explicitRules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule rule in explicitRules)
            {
                security.RemoveAccessRuleSpecific(rule);
            }

            // Determine inheritance flags safely based on object descriptor target type
            bool isDirectory = security is DirectorySecurity;
            var inheritanceFlags = isDirectory ? (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit) : InheritanceFlags.None;
            const PropagationFlags propagationFlags = PropagationFlags.None;

            // 4. Add mandatory high-privilege rules
            security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, inheritanceFlags, propagationFlags, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, inheritanceFlags, propagationFlags, AccessControlType.Allow));

            // 5. Add current user key
            // We grant explicit Full Control to the current user unless they are the
            // LocalSystem account (which is already covered in Step 4) or have administrator privileges.
            bool isSystem = currentUserSid != null && currentUserSid.Equals(systemSid);
            bool isAdmin = IsAdministrator();

            if (currentUserSid != null && !isSystem && !isAdmin)
            {
                security.AddAccessRule(new FileSystemAccessRule(currentUserSid, FileSystemRights.FullControl, inheritanceFlags, propagationFlags, AccessControlType.Allow));
            }
        }

        /// <summary>
        /// Seam for unit testing to supply a synthetic elevation state.
        /// Production code defaults to the current Windows principal's administrator role.
        /// </summary>
        internal static Func<bool> IsAdministratorCore = () =>
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        };

        /// <summary>
        /// Determines whether the current process is running with administrative privileges.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current user has the <see cref="WindowsBuiltInRole.Administrator"/> role;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        [ExcludeFromCodeCoverage]
        public static bool IsAdministrator() => IsAdministratorCore();

        /// <summary>
        /// Validates that the current process is running as an administrator.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the current process does not have administrative privileges.
        /// </exception>
        /// <remarks>
        /// Use this as a pre-flight check before performing service management operations
        /// to avoid opaque Win32 errors during service creation or deletion.
        /// </remarks>
        [ExcludeFromCodeCoverage]
        public static void EnsureAdministrator()
        {
            if (!IsAdministrator())
            {
                throw new UnauthorizedAccessException("This operation requires administrator privileges.");
            }
        }

        /// <summary>
        /// Handles non-administrator filesystem operational fallback conditions safely when modifying ACLs.
        /// </summary>
        /// <param name="ex">The underlying unauthorized access exception intercepted during directory configuration routines.</param>
        /// <param name="message">The warning message to log.</param>
        private static void HandleNonAdminFallback(UnauthorizedAccessException ex, string message)
        {
            // GRACEFUL FALLBACK:
            // If a non-admin process cannot modify an existing directory's security descriptor,
            // we assume the vault/directory was previously created and secured by an Administrator.
            Logger.Warn($"{message} ({ex.Message})");
        }
    }
}
