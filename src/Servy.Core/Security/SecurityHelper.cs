using System;
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
        /// <remarks>
        /// <para>
        /// This method acts as a wrapper that ensures the directory is created and then 
        /// delegates the security configuration to <see cref="ApplySecurityRules"/>.
        /// </para>
        /// <para>
        /// The current process identity is retrieved and passed to the rule engine to ensure 
        /// that the identity performing the operation (e.g., an installer or test runner) 
        /// maintains access to the folder it just secured.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the process lacks sufficient privileges to modify security descriptors.</exception>
        /// <exception cref="IOException">Thrown when a general I/O error occurs during directory access.</exception>
        public static void CreateSecureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            DirectoryInfo dirInfo = !Directory.Exists(path)
                ? Directory.CreateDirectory(path)
                : new DirectoryInfo(path);

            var security = dirInfo.GetAccessControl();

            // Get the actual identity running the code
            var currentUserSid = WindowsIdentity.GetCurrent().User;

            // Apply the security configuration logic
            ApplySecurityRules(security, currentUserSid);

            dirInfo.SetAccessControl(security);
        }

        /// <summary>
        /// Configures a <see cref="DirectorySecurity"/> object with restrictive rules, 
        /// including breaking inheritance and purging broad group access.
        /// </summary>
        /// <param name="security">The security descriptor to modify.</param>
        /// <param name="currentUserSid">
        /// The SID of the current user to conditionally grant access to. 
        /// Usually retrieved via <see cref="WindowsIdentity.User"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method is marked <c>internal</c> to allow unit tests to exercise all logical 
        /// branches (including System and Admin identity checks) using reflection without 
        /// requiring the tests to run as those specific accounts.
        /// </para>
        /// <para>
        /// Logic performed:
        /// <list type="number">
        /// <item>
        /// <description>Breaks inheritance and strips inherited rules (<c>preserveInheritance: false</c>).</description>
        /// </item>
        /// <item>
        /// <description>Explicitly purges any explicit ACEs for "Users", "Authenticated Users", and "Everyone".</description>
        /// </item>
        /// <item>
        /// <description>Grants Full Control to Administrators and the Local System account.</description>
        /// </item>
        /// <item>
        /// <description>Conditionally grants Full Control to the <paramref name="currentUserSid"/> to ensure operational continuity.</description>
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        internal static void ApplySecurityRules(DirectorySecurity security, IdentityReference currentUserSid)
        {
            // 1. Break inheritance (removes rules flowing from %ProgramData%)
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // 2. Define the broad groups that cause the LPE vulnerability
            var builtinUsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);            // Users
            var authenticatedUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null); // Authenticated Users
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);                       // Everyone

            // 3. PURGE: Remove these groups even if they were added explicitly/manually by the OS
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
    }
}