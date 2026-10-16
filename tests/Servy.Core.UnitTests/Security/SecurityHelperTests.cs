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
    // Teardown deletes the tree either way: elevated runs inherit access from the mandatory
    // Administrators ACE, non-elevated runs get the explicit current-user ACE
    // ApplySecurityRules adds when IsAdministrator() is false.
    public class SecurityHelperTests : TempDirectoryTestBase
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateSecureDirectory_PathIsNullOrWhiteSpace_ThrowsArgumentException(string invalidPath)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => SecurityHelper.CreateSecureDirectory(invalidPath));
        }

        [Theory]
        [InlineData(WellKnownSidType.BuiltinUsersSid, true)]
        [InlineData(WellKnownSidType.AuthenticatedUserSid, true)]
        [InlineData(WellKnownSidType.WorldSid, true)]
        [InlineData(WellKnownSidType.BuiltinAdministratorsSid, false)]
        [InlineData(WellKnownSidType.LocalSystemSid, false)]
        public void IsBroadUnprivilegedSid_KnownSid_ReturnsWhetherItIsABroadGroup(WellKnownSidType sidType, bool expected)
        {
            // Arrange
            var sid = new SecurityIdentifier(sidType, null);

            // Act
            var result = SecurityHelper.IsBroadUnprivilegedSid(sid);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CreateSecureDirectory_ExistingDirectory_UpgradesSecurity()
        {
            // Arrange
            var path = Path.Combine(TempDirectory, "UpgradeDir");
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
            var path = Path.Combine(TempDirectory, "PurgeUsersDir");
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

        [Theory]
        [InlineData(WellKnownSidType.AuthenticatedUserSid)]
        [InlineData(WellKnownSidType.WorldSid)]
        public void CreateSecureDirectory_PurgesOtherBroadUnprivilegedSids(WellKnownSidType sidType)
        {
            // Arrange
            // BroadUnprivilegedSids holds three principals and CreateSecureDirectory's purge step
            // rejects an explicit Allow rule for any of them, but the sibling test above only ever
            // drives the purge with the BuiltinUsers SID, so the other two arms have no coverage.
            var path = Path.Combine(TempDirectory, "PurgeOtherSidDir_" + sidType);
            Directory.CreateDirectory(path);
            var dirInfo = new DirectoryInfo(path);

            var sid = new SecurityIdentifier(sidType, null);
            var acl = dirInfo.GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Read, AccessControlType.Allow));
            dirInfo.SetAccessControl(acl);

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var finalAcl = dirInfo.GetAccessControl();
            var rules = finalAcl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                               .Cast<FileSystemAccessRule>();

            Assert.DoesNotContain(rules, r => r.IdentityReference == sid);
        }

        [Fact]
        public void CreateSecureDirectory_PurgesEveryNonAdministrativeExplicitRule()
        {
            // Arrange
            // A standard user who created the directory before Servy did can add explicit rules for any principal,
            // not only for the broad groups, so none of them may survive (#7526).
            var path = Path.Combine(TempDirectory, "PurgeEveryExplicitDir");
            Directory.CreateDirectory(path);
            var dirInfo = new DirectoryInfo(path);

            var localServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
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

            Assert.DoesNotContain(rules, r => r.IdentityReference == localServiceSid);
            Assert.DoesNotContain(rules, r => r.IdentityReference == everyoneSid);

            // Verify standard high-privilege accounts exist
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            Assert.Contains(rules, r => r.IdentityReference == adminSid);
        }

        [Fact]
        public void CreateSecureDirectory_ExistingDirectoryOwnedByAnotherPrincipal_HandsItToAdministrators()
        {
            // Only an elevated process can make Administrators the owner
            if (!SecurityHelper.IsAdministrator())
            {
                return; // Not elevated: only an elevated process can hand the directory to Administrators.
            }

            // Arrange
            var path = Path.Combine(TempDirectory, "ForeignOwnerDir");
            Directory.CreateDirectory(path);
            var dirInfo = new DirectoryInfo(path);

            SecurityIdentifier currentUserSid;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                currentUserSid = identity.User;
            }

            // The owner keeps an implicit right to rewrite the DACL, so a directory owned by its creator is not hardened
            var ownerAcl = dirInfo.GetAccessControl(AccessControlSections.Owner);
            ownerAcl.SetOwner(currentUserSid);
            dirInfo.SetAccessControl(ownerAcl);
            Assert.Equal(currentUserSid, dirInfo.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier)));

            // Act
            SecurityHelper.CreateSecureDirectory(path);

            // Assert
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            Assert.Equal(adminSid, dirInfo.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier)));
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
            var path = Path.Combine(TempDirectory, "CurrentUserDir");
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
            var path = Path.Combine(TempDirectory, "NewSecureDir");

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

        [Fact]
        public void ApplySecurityRules_ExplicitDenyRulesForMandatoryPrincipals_ArePurged()
        {
            // Arrange
            // The anti-squatting purge drops explicit Deny rules for Administrators, Local System
            // and the current user; every other ApplySecurityRules_* test seeds Allow rules only,
            // so the Deny arm of the purge loop is evaluated but never taken.
            var security = new DirectorySecurity();
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var currentUserSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

            security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, AccessControlType.Deny));
            security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Deny));
            security.AddAccessRule(new FileSystemAccessRule(currentUserSid, FileSystemRights.FullControl, AccessControlType.Deny));

            // Act
            InvokeApplySecurityRules(security, currentUserSid);

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
                                .Cast<FileSystemAccessRule>()
                                .ToList();

            // One assertion per arm of the purge condition, so a regression that narrows which
            // principals are covered names the principal it dropped.
            Assert.DoesNotContain(rules, r => r.AccessControlType == AccessControlType.Deny && r.IdentityReference == adminSid);
            Assert.DoesNotContain(rules, r => r.AccessControlType == AccessControlType.Deny && r.IdentityReference == systemSid);
            Assert.DoesNotContain(rules, r => r.AccessControlType == AccessControlType.Deny && r.IdentityReference == currentUserSid);
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
            var path = Path.Combine(TempDirectory, "HealedInheritanceDir");

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
                return; // Elevated run: SeTakeOwnershipPrivilege / SeBackupPrivilege bypass the OWNER RIGHTS restriction.
            }

            // Arrange
            var path = Path.Combine(TempDirectory, "ExistingRootVaultDir");
            Directory.CreateDirectory(path);

            var dirInfo = new DirectoryInfo(path);
            var acl = dirInfo.GetAccessControl(AccessControlSections.Access);

            // OWNER RIGHTS (S-1-3-4) replaces the owner's implicit READ_CONTROL + WRITE_DAC with
            // exactly what this ACE grants. Withholding ChangePermissions is what makes
            // SetAccessControl fail; everything else is kept so Dispose can still clean up.
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
            acl.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier("S-1-3-4"),
                FileSystemRights.FullControl & ~FileSystemRights.ChangePermissions,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            dirInfo.SetAccessControl(acl);

            // Act & Assert
            // The hardening pass must hit UnauthorizedAccessException on SetAccessControl and be
            // swallowed by the non-admin fallback rather than surfacing to the caller.
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
    }

    /// <summary>
    /// Names the xUnit collection of the tests that swap <see cref="SecurityHelper.IsAdministratorCore"/>. The seam is
    /// process-global and other classes read <see cref="SecurityHelper.IsAdministrator"/>, so the swapping tests must
    /// not run in parallel with the rest of the assembly.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ElevationSeamCollection
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "ElevationSeamSequential";
    }

    /// <summary>
    /// Pins the current-user ACE of <see cref="SecurityHelper.ApplySecurityRules"/> against a synthetic elevation state,
    /// so it is checked on elevated runners too, where the tests that read the real token skip themselves.
    /// </summary>
    [Collection(ElevationSeamCollection.Name)]
    public class SecurityHelperElevationTests
    {
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void ApplySecurityRules_OrdinaryUser_GetsTheCurrentUserAceOnlyWhenNotElevated(bool elevated, bool expectAce)
        {
            // Arrange
            var security = new DirectorySecurity();
            var ordinarySid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            var originalSeam = SecurityHelper.IsAdministratorCore;
            SecurityHelper.IsAdministratorCore = () => elevated;

            try
            {
                // Act
                SecurityHelper.ApplySecurityRules(security, ordinarySid);
            }
            finally
            {
                SecurityHelper.IsAdministratorCore = originalSeam;
            }

            // Assert: Full Control for the current user only without elevation, next to the two mandatory ACEs
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
                                .Cast<FileSystemAccessRule>()
                                .ToList();
            Assert.Equal(expectAce, rules.Any(r => r.IdentityReference.Equals(ordinarySid) && r.FileSystemRights == FileSystemRights.FullControl));
            Assert.Equal(expectAce ? 3 : 2, rules.Count);
        }

        [Fact]
        public void ApplySecurityRules_ExplicitRulesForOtherPrincipals_LeavesOnlyTheMandatoryAces()
        {
            // Arrange
            // Every explicit rule goes, an Allow for a principal outside the broad groups included (#7526)
            var security = new DirectorySecurity();
            var localServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var networkServiceSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            security.AddAccessRule(new FileSystemAccessRule(localServiceSid, FileSystemRights.Modify, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(networkServiceSid, FileSystemRights.Read, AccessControlType.Deny));

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var originalSeam = SecurityHelper.IsAdministratorCore;
            SecurityHelper.IsAdministratorCore = () => true;

            try
            {
                // Act
                SecurityHelper.ApplySecurityRules(security, null);
            }
            finally
            {
                SecurityHelper.IsAdministratorCore = originalSeam;
            }

            // Assert
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
                                .Cast<FileSystemAccessRule>()
                                .ToList();
            Assert.DoesNotContain(rules, r => r.IdentityReference.Equals(localServiceSid));
            Assert.DoesNotContain(rules, r => r.IdentityReference.Equals(networkServiceSid));
            Assert.Contains(rules, r => r.IdentityReference.Equals(adminSid) && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Contains(rules, r => r.IdentityReference.Equals(systemSid) && r.FileSystemRights == FileSystemRights.FullControl);
            Assert.Equal(2, rules.Count);
        }
    }
}
