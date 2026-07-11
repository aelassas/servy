using Servy.Core.Security;
using System.Security.AccessControl;
using System.Security.Principal;

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
        public void CreateSecureDirectory_PathIsNullOrWhiteSpace_ThrowsArgumentException(string? invalidPath)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => SecurityHelper.CreateSecureDirectory(invalidPath!));
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
            // Arrange
            var path = Path.Combine(_testBaseDir, "CurrentUserDir");
            SecurityIdentifier currentUserSid;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                currentUserSid = identity.User!;
            }
            bool isAdmin = SecurityHelper.IsAdministrator(); // Get the state of the runner

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var acl = new DirectoryInfo(path).GetAccessControl();
            var rules = acl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                           .Cast<FileSystemAccessRule>()
                           .ToList();

            // If running as Administrator, current user is already covered via the broad group ACE block
            if (isAdmin)
            {
                var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                Assert.Contains(rules, r => r.IdentityReference == adminSid && r.FileSystemRights == FileSystemRights.FullControl);
            }
            else
            {
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

        [Theory]
        [InlineData(WellKnownSidType.LocalSystemSid)]
        [InlineData(WellKnownSidType.BuiltinAdministratorsSid)]
        [InlineData(null)]
        public void ApplySecurityRules_HighPrivilegeOrNullUser_SkipsDuplicateOrEmptyAclEntry(WellKnownSidType? wellKnownSidType)
        {
            // Arrange
            var security = new DirectorySecurity();
            SecurityIdentifier? sidToTest = wellKnownSidType.HasValue
                ? new SecurityIdentifier(wellKnownSidType.Value, null)
                : null;

            // Act
            InvokeApplySecurityRules(security, sidToTest);

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));

            // Core logic verification: Check that the total ACL count evaluates cleanly to 
            // exactly 2 rules (Local System and Administrators), verifying that duplicate 
            // assignments or null objects are cleanly skipped.
            Assert.Equal(2, rules.Count);
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

        /// <summary>
        /// Helper to invoke the internal method via reflection.
        /// </summary>
        /// <param name="security">The security descriptor context.</param>
        /// <param name="sid">The identity reference SID target.</param>
        /// <param name="breakInheritance"><c>true</c> to break DACL cascading.</param>
        private void InvokeApplySecurityRules(DirectorySecurity security, IdentityReference? sid, bool breakInheritance = true)
        {
            // Arrange & Act: Search across both Public and NonPublic bindings to ensure resolution matches the public core
            var method = typeof(SecurityHelper)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "ApplySecurityRules" && m.GetParameters().Length == 3);

            if (method == null)
            {
                throw new InvalidOperationException("Could not locate the 'ApplySecurityRules' method on SecurityHelper via reflection hooks.");
            }

            // Assert & Execute
            method.Invoke(null, new object?[] { security, sid, breakInheritance });
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
                catch
                {
                    // Best-effort cleanup
                }
            }
        }
    }
}