using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Servy.Core.Security;
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
            Assert.Throws<ArgumentException>(() => SecurityHelper.CreateSecureDirectory(invalidPath));
        }

        [Fact]
        public void CreateSecureDirectory_ExistingDirectory_UpgradesSecurity()
        {
            var path = Path.Combine(_testBaseDir, "UpgradeDir");
            Directory.CreateDirectory(path);

            var initialAcl = new DirectoryInfo(path).GetAccessControl();
            Assert.False(initialAcl.AreAccessRulesProtected);

            SecurityHelper.CreateSecureDirectory(path);

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

            // Verify purge: Everyone should be GONE (This was the fix)
            Assert.DoesNotContain(rules, r => r.IdentityReference == everyoneSid);

            // Verify standard high-privilege accounts exist
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            Assert.Contains(rules, r => r.IdentityReference == adminSid);
        }

        [Fact]
        public void CreateSecureDirectory_EnsuresCurrentUserHasAccess()
        {
            // Arrange
            var path = Path.Combine(_testBaseDir, "CurrentUserDir");
            var currentUserSid = WindowsIdentity.GetCurrent().User;
            bool isAdmin = SecurityHelper.IsAdministrator(); // Get the state of the runner

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var acl = new DirectoryInfo(path).GetAccessControl();
            var rules = acl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                           .Cast<FileSystemAccessRule>();

            // The Admin Group SID
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            if (isAdmin)
            {
                // If we are an Admin, we expect the Admin Group to have access, 
                // and the explicit User SID to be MISSING (optimized out).
                Assert.Contains(rules, r => r.IdentityReference == adminSid && r.FileSystemRights == FileSystemRights.FullControl);
                Assert.DoesNotContain(rules, r => r.IdentityReference == currentUserSid);
            }
            else
            {
                // If we were a standard user (non-admin), we would expect the explicit rule.
                Assert.Contains(rules, r => r.IdentityReference == currentUserSid && r.FileSystemRights == FileSystemRights.FullControl);
            }
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

        [Fact]
        public void ApplySecurityRules_WhenUserIsSystem_DoesNotAddDuplicateRule()
        {
            // Arrange
            var security = new DirectorySecurity();
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            // Act: Simulate running as SYSTEM
            InvokeApplySecurityRules(security, systemSid);

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
            // Should only have 2 rules (Admin and System). The "CurrentUser" rule was skipped.
            Assert.Equal(2, rules.Count);
        }

        [Fact]
        public void ApplySecurityRules_WhenUserIsAdmin_DoesNotAddDuplicateRule()
        {
            // Arrange
            var security = new DirectorySecurity();
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            // Act: Simulate running as the Administrators group itself
            InvokeApplySecurityRules(security, adminSid);

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
            Assert.Equal(2, rules.Count);
        }

        [Fact]
        public void ApplySecurityRules_WhenUserIsNull_DoesNotThrow()
        {
            // Arrange
            var security = new DirectorySecurity();

            // Act: Simulate null SID (edge case)
            InvokeApplySecurityRules(security, null);

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
            Assert.Equal(2, rules.Count);
        }

        // Helper to invoke the internal method via reflection
        private void InvokeApplySecurityRules(DirectorySecurity security, IdentityReference sid, bool breakInheritance = true)
        {
            var method = typeof(SecurityHelper).GetMethod("ApplySecurityRules",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Invoke(null, new object[] { security, sid, breakInheritance });
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBaseDir))
            {
                try
                {
                    // This should now succeed because the CurrentUser is added to the ACL
                    Directory.Delete(_testBaseDir, true);
                }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}