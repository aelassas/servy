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
        /// This method acts as a security enforcement point. If the directory does not exist, it is created.
        /// It then retrieves the current access control list (ACL) and applies the logic defined in 
        /// <see cref="ApplySecurityRules"/>.
        /// </para>
        /// <para>
        /// By default, it breaks inheritance to ensure that broad permissions from parent folders (like <c>C:\ProgramData</c>) 
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

            DirectoryInfo dirInfo = !Directory.Exists(path)
                ? Directory.CreateDirectory(path)
                : new DirectoryInfo(path);

            try
            {
                // CRITICAL: Explicitly request only the Access rules (DACL).
                // Default behavior fetches Owner and Group. A non-admin account cannot 
                // persist Owner/Group back to the filesystem, causing an UnauthorizedAccessException.
                var security = dirInfo.GetAccessControl(AccessControlSections.Access);
                SecurityIdentifier currentUserSid;
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    currentUserSid = identity.User;
                }

                ApplySecurityRules(security, currentUserSid, breakInheritance);

                dirInfo.SetAccessControl(security);
            }
            catch (UnauthorizedAccessException) when (!IsAdministrator() && !breakInheritance)
            {
                // GRACEFUL FALLBACK: 
                // If we are a non-admin service account managing a child folder, the Root Vault 
                // (parent folder) was already secured by the Administrator during installation.
                // Because breakInheritance is false, the OS is already enforcing security via 
                // inheritance, making it safe to proceed without crashing the service.
            }
        }

        /// <summary>
        /// Configures a <see cref="DirectorySecurity"/> object with restrictive rules, 
        /// purging broad group access and optionally managing inheritance boundaries.
        /// </summary>
        /// <param name="security">The security descriptor to modify.</param>
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
        /// **Inheritance Management:** Either blocks inheritance (Root Vault) or enables it (Child Folder) 
        /// to support multi-account setups.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// **Dangerous Group Purge:** Explicitly removes access for <c>Users</c>, <c>Authenticated Users</c>, 
        /// and <c>Everyone</c> to close LPE vectors.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// **Mandatory Access:** Grants <c>Full Control</c> to <c>Administrators</c> and <c>Local System</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// **Operational Continuity:** Grants <c>Full Control</c> to the current process user if they 
        /// are not already part of the System or Admin groups.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        internal static void ApplySecurityRules(DirectorySecurity security, IdentityReference currentUserSid, bool breakInheritance = true)
        {
            // 1. Manage Inheritance Boundaries
            if (breakInheritance)
            {
                // Root Vault: Block permissions from flowing down from C:\ProgramData
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            }
            else
            {
                // Child Folder: Re-enable inheritance to heal folders broken by older versions
                // and allow custom service accounts to flow down from the root vault.
                security.SetAccessRuleProtection(isProtected: false, preserveInheritance: true);
            }

            // 2. Define the broad groups that cause the LPE vulnerability
            var builtinUsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);            // Users
            var authenticatedUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null); // Authenticated Users
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);                       // Everyone

            // 3. PURGE: Remove explicit/manual grants for dangerous groups
            security.PurgeAccessRules(builtinUsersSid);
            security.PurgeAccessRules(authenticatedUsersSid);
            security.PurgeAccessRules(everyoneSid);

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var accessFlags = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

            // 4. Add mandatory high-privilege rules
            security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, accessFlags, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, accessFlags, PropagationFlags.None, AccessControlType.Allow));

            // 5. Add current user key
            if (currentUserSid != null && !currentUserSid.Equals(systemSid) && !currentUserSid.Equals(adminSid))
            {
                security.AddAccessRule(new FileSystemAccessRule(currentUserSid, FileSystemRights.FullControl, accessFlags, PropagationFlags.None, AccessControlType.Allow));
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