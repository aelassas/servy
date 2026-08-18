using Servy.Core.Security;
using Servy.Testing;
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Servy.Core.UnitTests.Security
{
    public class SecurityHelperTests : IDisposable
    {
        private readonly string _testBaseDir;

        public SecurityHelperTests()
        {
            _testBaseDir = Path.Combine(Path.GetTempPath(), "SecurityHelperTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testBaseDir);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateSecureDirectory_PathIsNullOrWhiteSpace_ThrowsArgumentException(string invalidPath)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => SecurityHelper.CreateSecureDirectory(invalidPath));
        }

        [Fact]
        public void CreateSecureDirectory_ExistingDirectory_UpgradesSecurity()
        {
            // Arrange
            var path = Path.Combine(_testBaseDir, "UpgradeDir");
            Directory.CreateDirectory(path);

            var initialAcl = new DirectoryInfo(path).GetAccessControl();
            Assert.False(initialAcl.AreAccessRulesProtected);

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var finalAcl = new DirectoryInfo(path).GetAccessControl();
            Assert.True(finalAcl.AreAccessRulesProtected);
        }

        [Fact]
        public void CreateSecureDirectory_PurgesExplicitUsersGroupRules()
        {
            // Arrange
            var path = Path.Combine(_testBaseDir, "PurgeUsersDir");
            Directory.CreateDirectory(path);
            var dirInfo = new DirectoryInfo(path);

            // Manually add an EXPLICIT rule for the 'Users' group
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var acl = dirInfo.GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(usersSid, FileSystemRights.Read, AccessControlType.Allow));
            dirInfo.SetAccessControl(acl);

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var finalAcl = dirInfo.GetAccessControl();
            var rules = finalAcl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                               .Cast<FileSystemAccessRule>();

            // The 'Users' group rule should be gone, even if it was explicit
            Assert.DoesNotContain(rules, r => r.IdentityReference == usersSid);
        }

        [Fact]
        public void CreateSecureDirectory_PreservesSpecificExplicitRulesWhilePurgingBroadGroups()
        {
            // Arrange
            var path = Path.Combine(_testBaseDir, "PreserveExplicitDir");
            Directory.CreateDirectory(path);
            var dirInfo = new DirectoryInfo(path);

            // 1. Use LocalService as a "legitimate" manual rule that SHOULD be kept
            var localServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);

            // 2. Use Everyone as a broad rule that SHOULD be purged
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

            var acl = dirInfo.GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(localServiceSid, FileSystemRights.Read, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(everyoneSid, FileSystemRights.Read, AccessControlType.Allow));
            dirInfo.SetAccessControl(acl);

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var finalAcl = dirInfo.GetAccessControl();
            var rules = finalAcl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                               .Cast<FileSystemAccessRule>()
                               .ToList();

            // Verify preservation: LocalService should still be there
            Assert.Contains(rules, r => r.IdentityReference == localServiceSid);

            // Verify purge: Everyone should be GONE
            Assert.DoesNotContain(rules, r => r.IdentityReference == everyoneSid);

            // Verify standard high-privilege accounts exist
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            Assert.Contains(rules, r => r.IdentityReference == adminSid);
        }

        [Fact]
        public void CreateSecureDirectory_EnsuresCurrentUserHasAccess()
        {
            // Skip on elevated runs: when elevated, current user access is covered transitively
            // by the Administrators group ACE rather than writing a distinct user ACE.
            if (SecurityHelper.IsAdministrator())
            {
                return; // Elevated run: the current user is covered transitively by the Administrators ACE, so no distinct current-user ACE is written.
            }

            // Arrange
            var path = Path.Combine(_testBaseDir, "CurrentUserDir");
            SecurityIdentifier currentUserSid;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                currentUserSid = identity.User;
            }

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var acl = new DirectoryInfo(path).GetAccessControl();
            var rules = acl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                           .Cast<FileSystemAccessRule>()
                           .ToList();

            Assert.Contains(rules, r => r.IdentityReference == currentUserSid && r.FileSystemRights == FileSystemRights.FullControl);
        }

        [Fact]
        public void CreateSecureDirectory_NewDirectory_SetsStandardMandatoryAcls()
        {
            // Arrange
            var path = Path.Combine(_testBaseDir, "NewSecureDir");

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var acl = new DirectoryInfo(path).GetAccessControl();
            var rules = acl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                           .Cast<FileSystemAccessRule>()
                           .ToList();

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            Assert.Contains(rules, r => r.IdentityReference == adminSid && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Contains(rules, r => r.IdentityReference == systemSid && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.True(acl.AreAccessRulesProtected);
        }

        [Theory]
        [InlineData(WellKnownSidType.LocalSystemSid)]
        [InlineData(null)]
        public void ApplySecurityRules_HighPrivilegeOrNullUser_SkipsDuplicateOrEmptyAclEntry(WellKnownSidType? wellKnownSidType)
        {
            // Arrange
            var security = new DirectorySecurity();
            SecurityIdentifier sidToTest = wellKnownSidType.HasValue
                ? new SecurityIdentifier(wellKnownSidType.Value, null)
                : null;

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            // Act
            InvokeApplySecurityRules(security, sidToTest);

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
                                .Cast<FileSystemAccessRule>()
                                .ToList();

            // Core logic verification: Validate exact mandatory ACEs (Local System and Administrators),
            // verifying that duplicate LocalSystem or null user assignments do not create additional ACE entries.
            Assert.Contains(rules, r => r.IdentityReference == adminSid && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Contains(rules, r => r.IdentityReference == systemSid && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Equal(2, rules.Count);
        }

        [Fact]
        public void ApplySecurityRules_NonPrivilegedUser_AddsCurrentUserAce()
        {
            // Skip on elevated runs: when elevated, the current-user ACE is intentionally skipped via IsAdministrator().
            if (SecurityHelper.IsAdministrator()) return;

            // Arrange
            var security = new DirectorySecurity();
            var nonPrivilegedSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            // Act
            InvokeApplySecurityRules(security, nonPrivilegedSid);

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
                                .Cast<FileSystemAccessRule>()
                                .ToList();

            Assert.Contains(rules, r => r.IdentityReference == adminSid && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Contains(rules, r => r.IdentityReference == systemSid && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Contains(rules, r => r.IdentityReference == nonPrivilegedSid && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Equal(3, rules.Count);
        }

        #region breakInheritance:false Branch Coverage Tests

        [Fact]
        public void ApplySecurityRules_WhenBreakInheritanceIsFalse_PreservesInheritanceAndHealsAcl()
        {
            // Arrange
            var security = new DirectorySecurity();

            // Protect access rules upfront to create an inverted state for the test rule setup
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            Assert.True(security.AreAccessRulesProtected);

            // Act
            // Pass breakInheritance: false explicitly to traverse the target code path
            InvokeApplySecurityRules(security, null, breakInheritance: false);

            // Assert
            // Validate that protection is un-set, allowing parent DACL rules to cascade
            Assert.False(security.AreAccessRulesProtected, "DACL protection rules must be false when inheritance healing is requested.");
        }

        [Fact]
        public void CreateSecureDirectory_WithBreakInheritanceFalse_LeavesDirectoryInheritanceEnabled()
        {
            // Arrange
            var path = Path.Combine(_testBaseDir, "HealedInheritanceDir");

            // Act
            // Trigger public overload configuration with breakInheritance: false parameter assignment
            SecurityHelper.CreateSecureDirectory(path, breakInheritance: false);

            // Assert
            var acl = new DirectoryInfo(path).GetAccessControl();

            // Pin down behavior of directory initialization when skipping inheritance blockades
            Assert.False(acl.AreAccessRulesProtected, "Public creation overload using breakInheritance:false must preserve standard cascading inheritance maps.");
        }

        #endregion

        #region Non-Admin Graceful Fallback Coverage Tests

        [Fact]
        public void HandleNonAdminFallback_LogsWarningMessageWithoutThrowing()
        {
            // Arrange
            var ex = new UnauthorizedAccessException("Access to the path is denied.");
            string logMessage = "Test non-admin fallback message.";

            // Act & Assert
            // Invoke internal private fallback handler via reflection to ensure it executes safely
            var exception = Record.Exception(() =>
                TestReflection.InvokeNonPublicStatic(typeof(SecurityHelper), "HandleNonAdminFallback", ex, logMessage));

            Assert.Null(exception);
        }

        [Fact]
        public void CreateSecureDirectory_ExistingDirectoryWithBreakInheritanceTrue_GracefullyHandlesNonAdminFailure()
        {
            // Skip on elevated runs
            if (SecurityHelper.IsAdministrator())
            {
                return; // Elevated run: SetAccessControl succeeds, so the non-admin fallback branch is never reached.
            }

            // Arrange
            var path = Path.Combine(_testBaseDir, "ExistingRootVaultDir");
            Directory.CreateDirectory(path);

            // Act & Assert
            // When executing on existing directories, CreateSecureDirectory handles ACL re-hardening gracefully
            // for non-admin contexts if access is denied on SetAccessControl.
            var exception = Record.Exception(() => SecurityHelper.CreateSecureDirectory(path, breakInheritance: true));
            Assert.Null(exception);
        }

        #endregion

        /// <summary>
        /// Helper to invoke the internal method via reflection.
        /// </summary>
        /// <param name="security">The security descriptor context.</param>
        /// <param name="sid">The identity reference SID target.</param>
        /// <param name="breakInheritance"><c>true</c> to break DACL cascading.</param>
        private void InvokeApplySecurityRules(DirectorySecurity security, IdentityReference sid, bool breakInheritance = true)
            => TestReflection.InvokePublicStatic(typeof(SecurityHelper), "ApplySecurityRules", security, sid, breakInheritance);

        public void Dispose()
        {
            if (Directory.Exists(_testBaseDir))
            {
                try
                {
                    // This should now succeed because the CurrentUser is added to the ACL
                    Directory.Delete(_testBaseDir, true);
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
        }
    }
}
