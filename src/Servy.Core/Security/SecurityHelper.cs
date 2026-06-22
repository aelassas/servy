using Servy.Core.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
        /// Ensures a directory exists and applies a restrictive security descriptor to mitigate 
        /// local privilege escalation (LPE) risks by limiting access to high-privileged accounts.
        /// </summary>
        /// <param name="path">The full filesystem path of the directory to secure.</param>
        /// <param name="breakInheritance">
        /// <see langword="true"/> to sever inheritance from the parent directory (establishing a Root Vault). 
        /// <see langword="false"/> to ensure inheritance is active, allowing custom ACLs to cascade from a secured parent.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method acts as a security enforcement point. If the directory does not exist, it is created
        /// atomically with the intended security descriptor to eliminate race conditions.
        /// It then retrieves the current access control list (ACL) and applies the logic defined in 
        /// <see cref="ApplySecurityRules"/>.
        /// </para>
        /// <para>
        /// By default, it breaks inheritance to ensure that broad permissions from parent folders (like <c>%ProgramData%</c>) 
        /// do not compromise the Servy data store.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the process lacks sufficient privileges to modify security descriptors.</exception>
        /// <exception cref="IOException">Thrown when a general I/O error occurs during directory access.</exception>
        public static void CreateSecureDirectory(string path, bool breakInheritance = true)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            SecurityIdentifier currentUserSid = null;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                currentUserSid = identity.User;
            }

            if (!Directory.Exists(path))
            {
                // ATOMIC CREATION: Create the directory securely with the descriptor applied on execution.
                // Using atomic creation eliminates the race window between directory creation 
                // and security enforcement. The directory never exists with inherited broad permissions.
                try
                {
                    var ds = new DirectorySecurity();

                    // Apply the hardened rules to the descriptor before creation.
                    ApplySecurityRules(ds, currentUserSid, breakInheritance);

                    // Create the directory with the hardened security descriptor in a single operation.
                    Directory.CreateDirectory(path, ds);
                }
                catch (UnauthorizedAccessException ex) when (!IsAdministrator() && !breakInheritance)
                {
                    // GRACEFUL FALLBACK: 
                    // If we are a non-admin service account managing a child folder, the Root Vault 
                    // (parent folder) was already secured by the Administrator during installation.
                    // Because breakInheritance is false, the OS is already enforcing security via 
                    // inheritance, making it safe to proceed without crashing the service.
                    Logger.Warn(
                        $"Could not atomically create hardened directory '{path}' as non-admin. " +
                        $"Falling back to standard environmental creation rules. Verify parent vault is secured. ({ex.Message})");
                }
            }
            else
            {
                // EXISTING DIRECTORY: Reference the existing one and harden in-place.
                // Note: The hardening pass purges Allow ACEs for broad groups and Deny ACEs 
                // for required accounts, neutralizing malicious squatting attempts.
                try
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(path);

                    // CRITICAL: Explicitly request only the Access rules (DACL).
                    // Default behavior fetches Owner and Group. A non-admin account cannot 
                    // persist Owner/Group back to the filesystem, causing an UnauthorizedAccessException.
                    var security = dirInfo.GetAccessControl(AccessControlSections.Access);

                    // Apply the hardened rules.
                    ApplySecurityRules(security, currentUserSid, breakInheritance);

                    dirInfo.SetAccessControl(security);
                }
                catch (UnauthorizedAccessException ex) when (!IsAdministrator() && !breakInheritance)
                {
                    // GRACEFUL FALLBACK: 
                    // If we are a non-admin service account managing a child folder, the Root Vault 
                    // (parent folder) was already secured by the Administrator during installation.
                    // Because breakInheritance is false, the OS is already enforcing security via 
                    // inheritance, making it safe to proceed without crashing the service.
                    Logger.Warn(
                        $"Could not write hardened ACL on '{path}' as non-admin. " +
                        $"Falling back to inherited permissions from parent. Verify parent vault is secured. ({ex.Message})");
                }
            }
        }

        /// <summary>
        /// Configures a <see cref="FileSystemSecurity"/> object with restrictive rules, 
        /// purging broad group access and optionally managing inheritance boundaries.
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
        /// <b>Dangerous Group Purge:</b> Explicitly removes access for <c>Users</c>, <c>Authenticated Users</c>, 
        /// and <c>Everyone</c> to close LPE vectors.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Anti-Squatting Purge:</b> Explicitly removes any <c>Deny</c> rules for <c>Administrators</c> and 
        /// <c>Local System</c> (and the current user) to prevent Denial-of-Service via directory squatting.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Mandatory Access:</b> Grants <c>Full Control</c> to <c>Administrators</c> and <c>Local System</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Operational Continuity:</b> Grants <c>Full Control</c> to the current process user if they 
        /// are not a member of the Administrators group and are not the LocalSystem account.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static void ApplySecurityRules(FileSystemSecurity security, IdentityReference currentUserSid, bool breakInheritance = true)
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
            var builtinUsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);            // Users
            var authenticatedUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null); // Authenticated Users
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);                       // Everyone

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            // 3. PURGE: Remove explicit/manual GRANTS for dangerous groups AND explicit DENY rules for critical accounts
            // Extract only explicit, non-inherited access rules to audit the DACL modifications safely
            var explicitRules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule rule in explicitRules)
            {
                // Purge Allow rules for broad, unprivileged groups. 
                // NOTE: We intentionally preserve explicit 'Allow' rules for other principals (e.g., custom service accounts),
                // otherwise a user could never run a service under a custom account, as CreateSecureDirectory
                // is invoked during service start, desktop app and manager startup, and CLI operations.
                if (rule.AccessControlType == AccessControlType.Allow &&
                    (rule.IdentityReference.Equals(builtinUsersSid) ||
                     rule.IdentityReference.Equals(authenticatedUsersSid) ||
                     rule.IdentityReference.Equals(everyoneSid)))
                {
                    security.RemoveAccessRule(rule);
                }

                // Purge Deny rules targeting required operational accounts (Anti-squatting)
                if (rule.AccessControlType == AccessControlType.Deny &&
                    (rule.IdentityReference.Equals(adminSid) ||
                     rule.IdentityReference.Equals(systemSid) ||
                     (currentUserSid != null && rule.IdentityReference.Equals(currentUserSid))))
                {
                    security.RemoveAccessRule(rule);
                }
            }

            // Determine inheritance flags safely based on object descriptor target type
            bool isDirectory = security is DirectorySecurity;
            var inheritanceFlags = isDirectory ? (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit) : InheritanceFlags.None;
            var propagationFlags = isDirectory ? PropagationFlags.None : PropagationFlags.None;

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
        /// Determines whether the current process is running with administrative privileges.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current user has the <see cref="WindowsBuiltInRole.Administrator"/> role; 
        /// otherwise, <see langword="false"/>.
        /// </returns>
        [ExcludeFromCodeCoverage]
        public static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

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
    }
}