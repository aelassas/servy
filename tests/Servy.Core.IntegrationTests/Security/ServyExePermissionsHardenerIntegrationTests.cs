using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Testing;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.IntegrationTests.Security
{
    /// <summary>
    /// Runs <see cref="ServyExePermissionsHardener"/> against a real, temporary vault and reads the resulting ACLs back.
    /// The target account is <c>NT AUTHORITY\LocalService</c>; <c>NT AUTHORITY\NetworkService</c> plays another
    /// service account. Rewriting owners and DACLs needs an elevated process, as the product does, so every test that
    /// hardens a vault or creates a link is skipped when the run is not elevated (CI runners are). The account-resolution
    /// and LocalService membership probes only read the system, and run either way.
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class ServyExePermissionsHardenerIntegrationTests : TempDirectoryTestBase
    {
        private const string NotElevatedSkipReason = "Rewriting file owners and DACLs requires an elevated process.";
        private const string TargetAccount = @"NT AUTHORITY\LocalService";

        private static readonly SecurityIdentifier TargetSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
        private static readonly SecurityIdentifier OtherSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
        private static readonly SecurityIdentifier AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier UsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        private static readonly SecurityIdentifier EveryoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        private static readonly string DbFile = Path.Combine(AppConfig.DbFolderName, AppConfig.DatabaseFileName);
        private static readonly string KeyFile = Path.Combine(AppConfig.SecurityFolderName, AppConfig.AESKeyFileName);
        private static readonly string HandleFile = AppConfig.HandleExeX64FileName + ".exe";

        private readonly bool _isElevated = SecurityHelper.IsAdministrator();
        private readonly string _vault;
        private readonly ServyExePermissionsHardener _sut;

        public ServyExePermissionsHardenerIntegrationTests()
        {
            _vault = Path.Combine(TempDirectory, "Servy");
            _sut = new ServyExePermissionsHardener(_vault);
        }

        #region Full vault

        [Fact]
        public void Harden_FullVault_GrantsTheWritableFoldersAndLocksEveryFileDown()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            GrantInheritedModify(OtherSid);

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Equal(ServyExePermissionsHardener.GetWritableFolders(), result.GrantedFolders);
            Assert.Empty(result.Failed);
            Assert.Empty(result.Missing);
            Assert.Equal(8, result.Hardened.Count);

            // 1. Nothing on the vault root; List and Create Files on each writable folder, Modify on the files created in it
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Deny));
            foreach (var folder in ServyExePermissionsHardener.GetWritableFolders())
            {
                AssertWritableFolder(Path.Combine(_vault, folder));
            }

            // 2. Binaries: Read & Execute, no write, no Delete
            foreach (var exe in new[] { AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, HandleFile })
            {
                var rights = AllowedRights(Path.Combine(_vault, exe), TargetSid);
                Assert.True(Has(rights, FileSystemRights.ReadAndExecute), $"{exe} is Read & Execute");
                Assert.False(Has(rights, FileSystemRights.WriteData), $"{exe} is not writable");
                Assert.False(Has(rights, FileSystemRights.Delete), $"{exe} is not deletable");
            }

            // 3. Settings files: Read only
            foreach (var settings in new[] { ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName })
            {
                var rights = AllowedRights(Path.Combine(_vault, settings), TargetSid);
                Assert.True(Has(rights, FileSystemRights.Read), $"{settings} is readable");
                Assert.False(Has(rights, FileSystemRights.WriteData), $"{settings} is not writable");
            }

            // 4. The database: Read and Write, no Delete. Another account's vault-wide grant from a previous version
            // is not carried onto it: that account gets its own grant when it is hardened.
            var db = Path.Combine(_vault, DbFile);
            var dbRights = AllowedRights(db, TargetSid);
            Assert.True(Has(dbRights, FileSystemRights.Read) && Has(dbRights, FileSystemRights.Write));
            Assert.False(Has(dbRights, FileSystemRights.Delete));
            Assert.Equal(0, AllowedRights(db, OtherSid));

            // 5. The encryption key: Read only, and no inherited grant of another account is carried onto it
            var key = Path.Combine(_vault, KeyFile);
            var keyRights = AllowedRights(key, TargetSid);
            Assert.True(Has(keyRights, FileSystemRights.Read));
            Assert.False(Has(keyRights, FileSystemRights.WriteData));
            Assert.False(Has(keyRights, FileSystemRights.AppendData));
            Assert.False(Has(keyRights, FileSystemRights.Delete));
            Assert.Equal(0, AllowedRights(key, OtherSid));

            // 6. Every hardened file stops inheriting, is owned by Administrators and keeps Full Control for it
            foreach (var file in result.Hardened)
            {
                var acl = new FileInfo(Path.Combine(_vault, file)).GetAccessControl();
                Assert.True(acl.AreAccessRulesProtected, $"{file} no longer inherits");
                Assert.Equal(AdministratorsSid, acl.GetOwner(typeof(SecurityIdentifier)));
                Assert.True(Has(AllowedRights(Path.Combine(_vault, file), AdministratorsSid), FileSystemRights.FullControl));
            }
        }

        [Fact]
        public void Harden_FilesCreatedAfterwards_AreWritableOnlyInTheWritableFolders()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            _sut.Harden(TargetAccount, CancellationToken.None);
            var wal = Path.Combine(_vault, AppConfig.DbFolderName, "Servy.db-wal");
            var log = Path.Combine(_vault, AppConfig.LogsFolderName, "Servy.Service.log");
            var recovery = Path.Combine(_vault, AppConfig.RecoveryFolderName, "svc_restartAttempts.dat");
            var planted = Path.Combine(_vault, "planted.exe");
            var securityFile = Path.Combine(_vault, AppConfig.SecurityFolderName, "planted.dat");
            foreach (var file in new[] { wal, log, recovery, planted, securityFile })
            {
                File.WriteAllText(file, string.Empty);
            }

            // Assert: SQLite's side files and the logs are writable and deletable...
            Assert.True(Has(AllowedRights(wal, TargetSid), FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete));
            Assert.True(Has(AllowedRights(log, TargetSid), FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete));

            // ...the recovery state is writable but not deletable, since it is rewritten in place (#7241)...
            Assert.True(Has(AllowedRights(recovery, TargetSid), FileSystemRights.Read | FileSystemRights.Write));
            Assert.False(Has(AllowedRights(recovery, TargetSid), FileSystemRights.Delete), "the recovery state is not deletable");

            // ...and a file anywhere else carries no grant at all
            Assert.Equal(0, AllowedRights(planted, TargetSid));
            Assert.Equal(0, AllowedRights(securityFile, TargetSid));
        }

        [Fact]
        public void Harden_VaultGrantOfAPreviousVersion_IsRevoked()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: the grant and the denial the vault root carried before
            CreateVault();
            var vault = new DirectoryInfo(_vault);
            var acl = vault.GetAccessControl(AccessControlSections.Access);
            acl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.Delete,
                InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Deny));
            vault.SetAccessControl(acl);
            var pdb = Path.Combine(_vault, "Servy.Service.pdb");
            File.WriteAllText(pdb, "symbols");

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Deny));
            Assert.Equal(0, AllowedRights(pdb, TargetSid));
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, AppConfig.SecurityFolderName), TargetSid));
        }

        [Fact]
        public void Harden_WritableFolderIsAJunction_IsNotGrantedAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var outside = Path.Combine(TempDirectory, "outside-logs");
            Directory.CreateDirectory(outside);
            RunCmd($"mklink /J \"{Path.Combine(_vault, AppConfig.LogsFolderName)}\" \"{outside}\"");

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Equal(new[] { AppConfig.LogsFolderName }, result.Failed);
            Assert.DoesNotContain(AppConfig.LogsFolderName, result.GrantedFolders);
            Assert.Empty(ExplicitRules(outside, TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public void Harden_RunTwice_IsIdempotent()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            var first = _sut.Harden(TargetAccount, CancellationToken.None);
            var second = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, first.Status);
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, second.Status);
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Allow));
            Assert.Equal(2, ExplicitRules(Path.Combine(_vault, AppConfig.LogsFolderName), TargetSid, AccessControlType.Allow).Count);
            Assert.Single(ExplicitRules(Path.Combine(_vault, AppConfig.ServyServiceUIExe), TargetSid, AccessControlType.Allow));
            Assert.Single(ExplicitRules(Path.Combine(_vault, KeyFile), TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public void Harden_TwoAccounts_EachKeepsItsOwnAccess()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            _sut.Harden(TargetAccount, CancellationToken.None);
            var second = _sut.Harden(@"NT AUTHORITY\NetworkService", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, second.Status);
            var exe = Path.Combine(_vault, AppConfig.ServyServiceUIExe);
            Assert.True(Has(AllowedRights(exe, TargetSid), FileSystemRights.ReadAndExecute));
            Assert.True(Has(AllowedRights(exe, OtherSid), FileSystemRights.ReadAndExecute));
            var key = Path.Combine(_vault, KeyFile);
            Assert.True(Has(AllowedRights(key, TargetSid), FileSystemRights.Read));
            Assert.True(Has(AllowedRights(key, OtherSid), FileSystemRights.Read));
            Assert.False(Has(AllowedRights(key, OtherSid), FileSystemRights.WriteData));
            var db = Path.Combine(_vault, DbFile);
            Assert.True(Has(AllowedRights(db, TargetSid), FileSystemRights.Write));
            Assert.True(Has(AllowedRights(db, OtherSid), FileSystemRights.Write));
            Assert.Equal(2, ExplicitRules(Path.Combine(_vault, AppConfig.LogsFolderName), OtherSid, AccessControlType.Allow).Count);
        }

        #endregion

        #region Existing entries

        [Fact]
        public void Harden_ExplicitEntries_PurgesBroadGroupsAndTheTargetAndKeepsThirdParties()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var exe = Path.Combine(_vault, AppConfig.ServyServiceUIExe);
            var acl = new FileInfo(exe).GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(UsersSid, FileSystemRights.Modify, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.Write, AccessControlType.Deny));
            acl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.FullControl, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(OtherSid, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
            new FileInfo(exe).SetAccessControl(acl);

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Empty(ExplicitRules(exe, UsersSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(exe, TargetSid, AccessControlType.Deny));
            var target = Assert.Single(ExplicitRules(exe, TargetSid, AccessControlType.Allow));
            Assert.False(Has((int)target.FileSystemRights, FileSystemRights.WriteData));
            Assert.True(Has(AllowedRights(exe, OtherSid), FileSystemRights.ReadAndExecute));
        }

        [Fact]
        public void Harden_BroadGroupDeny_IsKeptWhileItsGrantIsPurged()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var exe = Path.Combine(_vault, AppConfig.ServyServiceUIExe);
            var acl = new FileInfo(exe).GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(EveryoneSid, FileSystemRights.WriteData, AccessControlType.Deny));
            acl.AddAccessRule(new FileSystemAccessRule(EveryoneSid, FileSystemRights.Read, AccessControlType.Allow));
            new FileInfo(exe).SetAccessControl(acl);

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Empty(ExplicitRules(exe, EveryoneSid, AccessControlType.Allow));
            var deny = Assert.Single(ExplicitRules(exe, EveryoneSid, AccessControlType.Deny));
            Assert.True(Has((int)deny.FileSystemRights, FileSystemRights.WriteData));
        }

        [Fact]
        public void Harden_FileOwnedByAnotherAccount_IsReownedByAdministrators()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange - an elevated process creates files owned by BUILTIN\Administrators already, so hand one file a
            //           different owner first: the current user's own SID, which any token may set as owner.
            CreateVault();
            var exe = Path.Combine(_vault, AppConfig.ServyServiceUIExe);
            using var identity = WindowsIdentity.GetCurrent();
            var acl = new FileInfo(exe).GetAccessControl();
            acl.SetOwner(identity.User!);
            new FileInfo(exe).SetAccessControl(acl);
            Assert.Equal(identity.User, new FileInfo(exe).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Contains(AppConfig.ServyServiceUIExe, result.Hardened);
            Assert.Equal(AdministratorsSid, new FileInfo(exe).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
        }

        #endregion

        #region Missing and optional files

        [Fact]
        public void Harden_BinariesNotExtractedYet_HardensTheRestAndReportsThem()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            File.Delete(Path.Combine(_vault, AppConfig.ServyServiceCLIExe));
            File.Delete(Path.Combine(_vault, AppConfig.ServyRestarterExe));

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Incomplete, result.Status);
            Assert.Equal(new[] { AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe }, result.Missing);
            Assert.Contains(AppConfig.ServyServiceUIExe, result.Hardened);
            Assert.True(Has(AllowedRights(Path.Combine(_vault, AppConfig.ServyServiceUIExe), TargetSid), FileSystemRights.ReadAndExecute));
            Assert.False(Has(AllowedRights(Path.Combine(_vault, AppConfig.ServyServiceUIExe), TargetSid), FileSystemRights.WriteData));
        }

        [Fact]
        public void Harden_SettingsFilesAbsent_AreSkippedWithoutFailing()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            File.Delete(Path.Combine(_vault, ServyExePermissionsHardener.ServiceSettingsFileName));
            File.Delete(Path.Combine(_vault, ServyExePermissionsHardener.RestarterSettingsFileName));

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Equal(2, result.Skipped.Count);
            Assert.Empty(result.Missing);
        }

        #endregion

        #region Links

        [Fact]
        public void Harden_FileIsASymbolicLink_IsNotTouchedAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var outside = Path.Combine(TempDirectory, "outside.exe");
            File.WriteAllText(outside, "outside");
            var link = Path.Combine(_vault, AppConfig.ServyRestarterExe);
            File.Delete(link);
            RunCmd($"mklink \"{link}\" \"{outside}\"");
            var before = new FileInfo(outside).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Equal(new[] { AppConfig.ServyRestarterExe }, result.Failed);
            Assert.Equal(before, new FileInfo(outside).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
        }

        [Fact]
        public void Harden_FileHasASecondHardLink_IsNotTouchedAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var exe = Path.Combine(_vault, AppConfig.ServyServiceCLIExe);
            RunCmd($"mklink /H \"{Path.Combine(TempDirectory, "second-link.exe")}\" \"{exe}\"");

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Equal(new[] { AppConfig.ServyServiceCLIExe }, result.Failed);
            Assert.Empty(ExplicitRules(exe, TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public void Harden_VaultIsAJunction_GrantsNothingOnItAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var realVault = Path.Combine(TempDirectory, "real-vault");
            Directory.CreateDirectory(realVault);
            RunCmd($"mklink /J \"{_vault}\" \"{realVault}\"");

            // Act
            var result = _sut.Harden(TargetAccount, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Empty(result.GrantedFolders);
            Assert.Contains(_vault, result.Failed);
            Assert.Empty(ExplicitRules(realVault, TargetSid, AccessControlType.Allow));
        }

        #endregion

        #region Accounts that are not hardened

        [Theory]
        [ClassData(typeof(AdministrativePrincipalsData))]
        public void Harden_AdministrativePrincipal_ChangesNothing(string account)
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var exe = Path.Combine(_vault, AppConfig.ServyServiceUIExe);
            var vaultBefore = Sddl(_vault);
            var exeBefore = Sddl(exe);

            // Act
            var result = _sut.Harden(account, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Skipped, result.Status);
            Assert.Equal(vaultBefore, Sddl(_vault));
            Assert.Equal(exeBefore, Sddl(exe));
        }

        [Theory]
        [InlineData("Everyone")]
        [InlineData(@"BUILTIN\Users")]
        [InlineData("definitely-not-an-account-7c1f")]
        public void Harden_InvalidAccount_ChangesNothing(string account)
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var vaultBefore = Sddl(_vault);

            // Act
            var result = _sut.Harden(account, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InvalidAccount, result.Status);
            Assert.Equal(vaultBefore, Sddl(_vault));
        }

        #endregion

        #region Entry points

        [Fact]
        public async Task HardenAsync_FullVault_ReturnsTrue()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            var hardened = await _sut.HardenAsync(TargetAccount, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(hardened);
            Assert.False(Has(AllowedRights(Path.Combine(_vault, KeyFile), TargetSid), FileSystemRights.WriteData));
        }

        [Fact]
        public async Task HardenServiceAccountsAsync_HardensTheAccountsOfInstalledServices()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
            {
                new ServiceDto { Name = "local-system", RunAsLocalSystem = true },
                new ServiceDto { Name = "local-service", RunAsLocalSystem = false, UserAccount = TargetAccount },
            });

            // Act
            await _sut.HardenServiceAccountsAsync(repository.Object, TestContext.Current.CancellationToken);

            // Assert
            var restarter = Path.Combine(_vault, AppConfig.ServyRestarterExe);
            Assert.True(Has(AllowedRights(restarter, TargetSid), FileSystemRights.ReadAndExecute));
            Assert.False(Has(AllowedRights(restarter, TargetSid), FileSystemRights.Delete));
            Assert.Equal(2, ExplicitRules(Path.Combine(_vault, AppConfig.RecoveryFolderName), TargetSid, AccessControlType.Allow).Count);
        }

        #endregion

        #region Revocation (#7161)

        [Fact]
        public void RevokeIfUnused_AfterHardening_RemovesEveryEntryOfTheAccountAndKeepsTheOtherAccount()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            GrantInheritedModify(TargetSid);
            _sut.Harden(TargetAccount, CancellationToken.None);
            _sut.Harden(@"NT AUTHORITY\NetworkService", CancellationToken.None);

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, new List<string> { @"NT AUTHORITY\NetworkService" }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Revoked, result.Status);
            Assert.Empty(result.Failed);
            var items = new List<string> { _vault };
            foreach (var folder in ServyExePermissionsHardener.GetWritableFolders())
                items.Add(Path.Combine(_vault, folder));
            foreach (var file in new[] { AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, HandleFile,
                ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName, DbFile, KeyFile })
                items.Add(Path.Combine(_vault, file));
            foreach (var item in items)
            {
                Assert.Empty(ExplicitRules(item, TargetSid, AccessControlType.Allow));
                Assert.Empty(ExplicitRules(item, TargetSid, AccessControlType.Deny));
                Assert.Equal(0, AllowedRights(item, TargetSid));
            }

            // The key and the database are no longer readable, a file the service writes later inherits nothing,
            // and the account that still runs a service keeps exactly what it had
            var laterLog = Path.Combine(_vault, AppConfig.LogsFolderName, "later.log");
            File.WriteAllText(laterLog, "later");
            Assert.Equal(0, AllowedRights(laterLog, TargetSid));
            Assert.True(Has(AllowedRights(laterLog, OtherSid), FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete));
            Assert.True(Has(AllowedRights(Path.Combine(_vault, KeyFile), OtherSid), FileSystemRights.Read));
            Assert.True(Has(AllowedRights(Path.Combine(_vault, DbFile), OtherSid), FileSystemRights.Write));
            Assert.Equal(2, ExplicitRules(Path.Combine(_vault, AppConfig.RecoveryFolderName), OtherSid, AccessControlType.Allow).Count);
        }

        [Fact]
        public void RevokeIfUnused_AccountStillRunsAService_KeepsEverything()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            _sut.Harden(TargetAccount, CancellationToken.None);
            var before = Sddl(Path.Combine(_vault, KeyFile));

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, new List<string> { @"nt authority\localservice" }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InUse, result.Status);
            Assert.Equal(before, Sddl(Path.Combine(_vault, KeyFile)));
            foreach (var folder in ServyExePermissionsHardener.GetWritableFolders())
                AssertWritableFolder(Path.Combine(_vault, folder));
        }

        [Fact]
        public void RevokeIfUnused_FileIsASymbolicLink_IsNotTouchedAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            _sut.Harden(TargetAccount, CancellationToken.None);
            var outside = Path.Combine(TempDirectory, "outside.exe");
            File.WriteAllText(outside, "outside");
            var outsideAcl = new FileInfo(outside).GetAccessControl(AccessControlSections.Access);
            outsideAcl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.Read, AccessControlType.Allow));
            new FileInfo(outside).SetAccessControl(outsideAcl);
            var link = Path.Combine(_vault, AppConfig.ServyRestarterExe);
            File.Delete(link);
            RunCmd($"mklink \"{link}\" \"{outside}\"");
            var before = new FileInfo(outside).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, new List<string>(), CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Equal(new[] { AppConfig.ServyRestarterExe }, result.Failed);
            Assert.Equal(before, new FileInfo(outside).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
            Assert.Empty(ExplicitRules(Path.Combine(_vault, KeyFile), TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public async Task RevokeIfUnusedAsync_LastServiceOfTheAccountRemoved_RevokesAndReturnsTrue()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            _sut.Harden(TargetAccount, CancellationToken.None);
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
            {
                new ServiceDto { Name = "local-system", RunAsLocalSystem = true },
            });

            // Act
            var revoked = await _sut.RevokeIfUnusedAsync(TargetAccount, repository.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(revoked);
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, KeyFile), TargetSid));
            Assert.Empty(ExplicitRules(Path.Combine(_vault, AppConfig.DbFolderName), TargetSid, AccessControlType.Allow));
        }

        #endregion

        #region Seams against the real system

        [Theory]
        [ClassData(typeof(WellKnownAccountData))]
        public void ResolveAccount_WellKnownNames_ResolveToTheirSid(string account, WellKnownSidType expected)
        {
            // Arrange
            var probe = new SeamProbe(_vault);

            // Act
            var sid = probe.Resolve(account);

            // Assert
            Assert.Equal(new SecurityIdentifier(expected, null), sid);
        }

        [Fact]
        public void ResolveAccount_UnknownName_ReturnsNull()
        {
            // Act
            var sid = new SeamProbe(_vault).Resolve("definitely-not-an-account-7c1f");

            // Assert
            Assert.Null(sid);
        }

        [Fact]
        public void ResolveAccount_NameNtAccountRejects_ReturnsNull()
        {
            // Arrange
            var probe = new SeamProbe(_vault);
            var name = new string('a', 600);

            // Act
            var sid = probe.Resolve(name);

            // Assert
            Assert.Null(sid);
        }

        [Fact]
        public void ResolveAccount_RelativeNotation_ResolvesALocalAccount()
        {
            // Arrange
            using var identity = WindowsIdentity.GetCurrent();
            var name = identity.Name;
            var slash = name.IndexOf('\\');
            Assert.SkipUnless(slash > 0 && string.Equals(name.Substring(0, slash), Environment.MachineName, StringComparison.OrdinalIgnoreCase),
                "The relative '.\\user' notation only names local accounts, and this run's account is not one.");

            // Act
            var sid = new SeamProbe(_vault).Resolve(@".\" + name.Substring(slash + 1));

            // Assert
            Assert.Equal(identity.User, sid);
        }

        [Fact]
        public void IsAdministratorsMember_ElevatedCurrentUser_IsNeverReportedAsNotAMember()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            using var identity = WindowsIdentity.GetCurrent();

            // Act
            var member = new SeamProbe(_vault).IsMember(identity.User!);

            // Assert: a direct member is found, one admitted through a nested group is "unknown"
            Assert.NotEqual((bool?)false, member);
        }

        [Fact]
        public void IsAdministratorsMember_LocalService_IsNeverReportedAsAMember()
        {
            // Act
            var member = new SeamProbe(_vault).IsMember(TargetSid);

            // Assert
            Assert.NotEqual((bool?)true, member);
        }

        [Fact]
        public void GetHardLinkCount_CountsTheLinks()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var probe = new SeamProbe(_vault);
            var file = Path.Combine(TempDirectory, "links.bin");
            File.WriteAllText(file, "x");

            // Act
            var single = probe.LinkCount(file);
            RunCmd($"mklink /H \"{Path.Combine(TempDirectory, "links-2.bin")}\" \"{file}\"");
            var doubled = probe.LinkCount(file);
            var missing = probe.LinkCount(Path.Combine(TempDirectory, "absent.bin"));

            // Assert
            Assert.Equal(1, single);
            Assert.Equal(2, doubled);
            Assert.Equal(-1, missing);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a vault holding every file the hardening covers.
        /// </summary>
        private void CreateVault()
        {
            Directory.CreateDirectory(Path.Combine(_vault, AppConfig.DbFolderName));
            Directory.CreateDirectory(Path.Combine(_vault, AppConfig.SecurityFolderName));
            foreach (var file in new[]
            {
                AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, HandleFile,
                ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName,
                DbFile, KeyFile,
            })
            {
                File.WriteAllText(Path.Combine(_vault, file), "test");
            }
        }

        /// <summary>
        /// Grants an account Modify on the vault, inherited by everything below it, as an earlier hardening would.
        /// </summary>
        private void GrantInheritedModify(SecurityIdentifier sid)
        {
            var vault = new DirectoryInfo(_vault);
            var acl = vault.GetAccessControl(AccessControlSections.Access);
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            vault.SetAccessControl(acl);
        }

        /// <summary>
        /// Asserts the target's two entries on a writable folder: List and Create Files on the folder itself (never
        /// Delete on it), and the rights <see cref="ServyExePermissionsHardener.GetWritableFolderFileRights"/> names on
        /// the files created in it - without Delete in <c>recovery\</c> (#7241). Both rules are bounded from above as
        /// well as from below: neither carries Change Permissions (WRITE_DAC) or Take Ownership, which would let the
        /// target rewrite the ACL it was just given (#7163).
        /// </summary>
        private static void AssertWritableFolder(string path)
        {
            // Arrange
            var rules = ExplicitRules(path, TargetSid, AccessControlType.Allow);

            // Assert
            Assert.Equal(2, rules.Count);

            var self = Assert.Single(rules, r => r.InheritanceFlags == InheritanceFlags.None);
            Assert.True(Has((int)self.FileSystemRights, FileSystemRights.Read | FileSystemRights.CreateFiles));
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.Delete));
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.DeleteSubdirectoriesAndFiles));

            // WRITE_DAC on the folder would let the target grant itself Delete Subdirectories And Files and then remove
            // db\Servy.db whatever that file's own protected ACL says, because FILE_DELETE_CHILD overrides the child's.
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.ChangePermissions), $"{path} is not re-ACLable by the target");
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.TakeOwnership), $"{path} is not re-ownable by the target");

            var files = Assert.Single(rules, r => r.InheritanceFlags == InheritanceFlags.ObjectInherit);
            Assert.Equal(PropagationFlags.InheritOnly, files.PropagationFlags);
            var expected = ServyExePermissionsHardener.GetWritableFolderFileRights(Path.GetFileName(path));
            Assert.True(Has((int)files.FileSystemRights, expected));
            if ((expected & FileSystemRights.Delete) == 0)
                Assert.False(Has((int)files.FileSystemRights, FileSystemRights.Delete), $"the files in {path} are not deletable");

            // Nothing above the rights GetWritableFolderFileRights names on the files created in it either.
            Assert.False(Has((int)files.FileSystemRights, FileSystemRights.ChangePermissions), $"the files in {path} are not re-ACLable by the target");
            Assert.False(Has((int)files.FileSystemRights, FileSystemRights.TakeOwnership), $"the files in {path} are not re-ownable by the target");

            Assert.False(Has(AllowedRights(path, TargetSid), FileSystemRights.Delete), $"{path} itself is not deletable");
        }

        /// <summary>
        /// The combined Allow rights a SID holds on a file or directory, explicit and inherited, that apply to it.
        /// </summary>
        private static int AllowedRights(string path, SecurityIdentifier sid)
        {
            FileSystemSecurity acl = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access)
                : new FileInfo(path).GetAccessControl(AccessControlSections.Access);

            return acl.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Where(r => sid.Equals(r.IdentityReference) && r.AccessControlType == AccessControlType.Allow)
                .Where(r => (r.PropagationFlags & PropagationFlags.InheritOnly) == 0) // an inherit-only entry does not apply to the object itself
                .Aggregate(0, (rights, r) => rights | (int)r.FileSystemRights);
        }

        /// <summary>
        /// The explicit rules of one type a SID holds on a file or directory.
        /// </summary>
        private static List<FileSystemAccessRule> ExplicitRules(string path, SecurityIdentifier sid, AccessControlType type)
        {
            FileSystemSecurity acl = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access)
                : new FileInfo(path).GetAccessControl(AccessControlSections.Access);

            return acl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Where(r => sid.Equals(r.IdentityReference) && r.AccessControlType == type)
                .ToList();
        }

        private static bool Has(int rights, FileSystemRights flag) => (rights & (int)flag) == (int)flag;

        private static string Sddl(string path) => Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All)
            : new FileInfo(path).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);

        /// <summary>
        /// Runs a <c>cmd.exe</c> built-in (here <c>mklink</c>) and fails the test if it does not succeed.
        /// </summary>
        private static void RunCmd(string arguments)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c " + arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = Process.Start(psi)!)
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, $"cmd /c {arguments} failed: {output}");
            }
        }

        /// <summary>
        /// Exposes the protected seams so they can be run against the real system.
        /// </summary>
        private sealed class SeamProbe : ServyExePermissionsHardener
        {
            public SeamProbe(string vaultDirectory) : base(vaultDirectory)
            {
            }

            public SecurityIdentifier? Resolve(string account) => ResolveAccount(account);

            public bool? IsMember(SecurityIdentifier sid) => IsAdministratorsMember(sid);

            public int LinkCount(string path) => GetHardLinkCount(path);
        }

        #endregion

        #region Test Data

        /// <summary>
        /// Provides dynamic test data for administrative principals resolved against the local system.
        /// </summary>
        public class AdministrativePrincipalsData : TheoryData<string>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AdministrativePrincipalsData"/> class and populates
            /// localized names for built-in administrative principals.
            /// </summary>
            public AdministrativePrincipalsData()
            {
                // Arrange & Act: Resolve localized account name for BUILTIN\Administrators
                var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                Add(adminSid.Translate(typeof(NTAccount)).Value);

                // Arrange & Act: Resolve localized account name for NT AUTHORITY\SYSTEM
                var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                Add(systemSid.Translate(typeof(NTAccount)).Value);
            }
        }

        /// <summary>
        /// Provides dynamic test data for well-known account resolution tests.
        /// </summary>
        public class WellKnownAccountData : TheoryData<string, WellKnownSidType>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="WellKnownAccountData"/> class and populates
            /// localized well-known account names mapped to their respective <see cref="WellKnownSidType"/>.
            /// </summary>
            public WellKnownAccountData()
            {
                // Arrange & Act: Resolve localized account name for LocalService
                var localServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
                Add(localServiceSid.Translate(typeof(NTAccount)).Value, WellKnownSidType.LocalServiceSid);

                // Arrange & Act: Resolve localized account name for NetworkService
                var networkServiceSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
                Add(networkServiceSid.Translate(typeof(NTAccount)).Value, WellKnownSidType.NetworkServiceSid);

                // Arrange & Act: Resolve localized account name for BUILTIN\Administrators
                var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                Add(adminSid.Translate(typeof(NTAccount)).Value, WellKnownSidType.BuiltinAdministratorsSid);
            }
        }

        #endregion
    }
}
