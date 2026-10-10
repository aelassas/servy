using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Testing;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.IntegrationTests.Security
{
    /// <summary>
    /// Runs <see cref="ServyExePermissionsHardener"/> against a real, temporary vault and reads the resulting ACLs back.
    /// The target account is <c>NT AUTHORITY\LocalService</c>, which runs the service <c>svc-one</c>;
    /// <c>NT AUTHORITY\NetworkService</c> plays another service account, which runs <c>svc-two</c>. Rewriting owners and DACLs needs an elevated process, as the product does, so every test that
    /// hardens a vault or creates a symbolic link or junction is skipped when the run is not elevated (CI runners are). The account-resolution
    /// and LocalService membership probes only read the system, and so do the hard-link count probe and its test (creating a hard link needs no elevation), and run either way.
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class ServyExePermissionsHardenerIntegrationTests : TempDirectoryTestBase
    {
        private const string NotElevatedSkipReason = "Rewriting file owners and DACLs requires an elevated process.";
        private const string TargetAccount = @"NT AUTHORITY\LocalService";
        private const string OtherAccount = @"NT AUTHORITY\NetworkService";
        private const string ServiceName = "svc-one";
        private const string OtherServiceName = "svc-two";

        private static readonly SecurityIdentifier TargetSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
        private static readonly SecurityIdentifier OtherSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
        private static readonly SecurityIdentifier AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier UsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        private static readonly SecurityIdentifier EveryoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        private static readonly string DbFile = Path.Combine(AppConfig.DbFolderName, AppConfig.DatabaseFileName);
        private static readonly string KeyFile = Path.Combine(AppConfig.SecurityFolderName, AppConfig.AESKeyFileName);
        private static readonly string HandleFile = AppConfig.HandleExeX64FileName + ".exe";
        private static readonly string ServiceLogsRoot = Path.Combine(AppConfig.LogsFolderName, AppConfig.ServiceLogsFolderName);
        private static readonly string ServiceLogsFolder = ServiceLogPaths.GetRelativeFolderPath(ServiceName);
        private static readonly string OtherServiceLogsFolder = ServiceLogPaths.GetRelativeFolderPath(OtherServiceName);
        private static readonly IReadOnlyCollection<string> Services = new[] { ServiceName };
        private static readonly IReadOnlyCollection<string> OtherServices = new[] { OtherServiceName };

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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Equal(ServyExePermissionsHardener.GetWritableFolders(Services), result.GrantedFolders);
            Assert.Empty(result.Failed);
            Assert.Empty(result.Missing);
            Assert.Equal(6, result.Hardened.Count);

            // 1. Nothing on the vault root; List and Create Files on each writable folder, Modify on the files created in it
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Deny));
            foreach (var folder in ServyExePermissionsHardener.GetWritableFolders(Services))
            {
                AssertWritableFolder(Path.Combine(_vault, folder));
            }

            // 2. Binaries: Read & Execute, no write, no Delete (Servy.Host.exe excluded)
            foreach (var exe in new[] { AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, HandleFile })
            {
                var rights = AllowedRights(Path.Combine(_vault, exe), TargetSid);
                Assert.True(Has(rights, FileSystemRights.ReadAndExecute), $"{exe} is Read & Execute");
                Assert.False(Has(rights, FileSystemRights.WriteData), $"{exe} is not writable");
                Assert.False(Has(rights, FileSystemRights.Delete), $"{exe} is not deletable");
            }

            // 3. Settings files: Read only (appsettings.host.json excluded)
            foreach (var settings in new[] { ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName })
            {
                var rights = AllowedRights(Path.Combine(_vault, settings), TargetSid);
                Assert.True(Has(rights, FileSystemRights.Read), $"{settings} is readable");
                Assert.False(Has(rights, FileSystemRights.WriteData), $"{settings} is not writable");
                Assert.False(Has(rights, FileSystemRights.Delete), $"{settings} is not deletable");
            }

            // 4. Servy.Host.exe and appsettings.host.json: no access for custom service accounts
            foreach (var hostItem in new[] { AppConfig.ServyHostExe, ServyExePermissionsHardener.HostSettingsFileName })
            {
                Assert.Equal(0, AllowedRights(Path.Combine(_vault, hostItem), TargetSid));
                Assert.Empty(ExplicitRules(Path.Combine(_vault, hostItem), TargetSid, AccessControlType.Allow));
            }

            // 5. The database, the encryption key and their folders: nothing at all. The wrapper reads its
            // configuration and writes its runtime state through the Servy host service (#7224, #7248).
            foreach (var closed in new[] { AppConfig.DbFolderName, DbFile, AppConfig.SecurityFolderName, KeyFile, AppConfig.LogsFolderName, ServiceLogsRoot })
            {
                Assert.Equal(0, AllowedRights(Path.Combine(_vault, closed), TargetSid));
                Assert.Empty(ExplicitRules(Path.Combine(_vault, closed), TargetSid, AccessControlType.Allow));
            }

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
            _sut.Harden(TargetAccount, Services, CancellationToken.None);
            var serviceLog = Path.Combine(_vault, ServiceLogsFolder, "Servy.Service.log");
            var sharedServiceLog = Path.Combine(_vault, ServiceLogsRoot, "Servy.Service.log");
            Directory.CreateDirectory(Path.Combine(_vault, OtherServiceLogsFolder));
            var otherServiceLog = Path.Combine(_vault, OtherServiceLogsFolder, "Servy.Service.log");
            var wal = Path.Combine(_vault, AppConfig.DbFolderName, "Servy.db-wal");
            var adminLog = Path.Combine(_vault, AppConfig.LogsFolderName, "Servy.Manager.log");
            var planted = Path.Combine(_vault, "planted.exe");
            var securityFile = Path.Combine(_vault, AppConfig.SecurityFolderName, "planted.dat");
            foreach (var file in new[] { serviceLog, sharedServiceLog, otherServiceLog, wal, adminLog, planted, securityFile })
            {
                File.WriteAllText(file, string.Empty);
            }

            // Assert: the service's own logs are writable and deletable (log rotation)...
            Assert.True(Has(AllowedRights(serviceLog, TargetSid), FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete));

            // ...and a file anywhere else, logs\services\ itself, another service's folder, SQLite's side files and the
            // administrative logs included, carries no grant at all
            Assert.Equal(0, AllowedRights(sharedServiceLog, TargetSid));
            Assert.Equal(0, AllowedRights(otherServiceLog, TargetSid));
            Assert.Equal(0, AllowedRights(wal, TargetSid));
            Assert.Equal(0, AllowedRights(adminLog, TargetSid));
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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Deny));
            Assert.Equal(0, AllowedRights(pdb, TargetSid));
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, AppConfig.SecurityFolderName), TargetSid));
        }

        [Fact]
        public void Harden_GrantsOfThePreviousLayout_AreRemovedFromTheDatabaseTheKeyAndTheLogs()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: what the previous hardening wrote - List and Create Files plus an inherited Modify on db\ and
            // logs\, Read and Write on a protected Servy.db, Read on a protected key - and the files that inherited
            // the folder grants (SQLite's -wal, the shared log). An earlier layout's grant on logs\services\ is there too, with
            // a log in it.
            CreateVault();
            var db = Path.Combine(_vault, AppConfig.DbFolderName);
            var logs = Path.Combine(_vault, AppConfig.LogsFolderName);
            var serviceLogs = Path.Combine(_vault, ServiceLogsRoot);
            Directory.CreateDirectory(serviceLogs);
            foreach (var folder in new[] { db, logs, serviceLogs })
            {
                var folderInfo = new DirectoryInfo(folder);
                var folderAcl = folderInfo.GetAccessControl(AccessControlSections.Access);
                folderAcl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.Read | FileSystemRights.CreateFiles, AccessControlType.Allow));
                folderAcl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.Modify,
                    InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                folderInfo.SetAccessControl(folderAcl);
            }

            ProtectWithGrant(Path.Combine(_vault, DbFile), FileSystemRights.Read | FileSystemRights.Write);
            ProtectWithGrant(Path.Combine(_vault, KeyFile), FileSystemRights.Read);
            var wal = Path.Combine(db, "Servy.db-wal");
            File.WriteAllText(wal, "wal");
            var sharedLog = Path.Combine(logs, "Servy.Service.log");
            File.WriteAllText(sharedLog, "log");
            var movedLog = Path.Combine(serviceLogs, "Servy.Service.log");
            File.WriteAllText(movedLog, "log");
            Assert.True(Has(AllowedRights(movedLog, TargetSid), FileSystemRights.Write));
            Assert.True(Has(AllowedRights(wal, TargetSid), FileSystemRights.Write));
            Assert.True(Has(AllowedRights(Path.Combine(_vault, DbFile), TargetSid), FileSystemRights.Write));

            // Act
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

            // Assert: the account can neither read nor write anything in db\, security\ or logs\ any more, the log
            // left in logs\services\ itself included
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            foreach (var item in new[] { db, Path.Combine(_vault, DbFile), wal, Path.Combine(_vault, AppConfig.SecurityFolderName), Path.Combine(_vault, KeyFile), logs, sharedLog, serviceLogs, movedLog })
                Assert.Equal(0, AllowedRights(item, TargetSid));
            Assert.True(File.Exists(movedLog));

            // ... and logs\services\svc-one\ is the one folder it writes
            AssertWritableFolder(Path.Combine(_vault, ServiceLogsFolder));
        }

        [Fact]
        public void Harden_WritableFolderIsAJunction_IsNotGrantedAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            var outside = Path.Combine(TempDirectory, "outside-logs");
            Directory.CreateDirectory(outside);
            Directory.CreateDirectory(Path.Combine(_vault, ServiceLogsRoot));
            RunCmd($"mklink /J \"{Path.Combine(_vault, ServiceLogsFolder)}\" \"{outside}\"");

            // Act
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Equal(new[] { ServiceLogsFolder }, result.Failed);
            Assert.DoesNotContain(ServiceLogsFolder, result.GrantedFolders);
            Assert.Empty(ExplicitRules(outside, TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public void Harden_RunTwice_IsIdempotent()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            var first = _sut.Harden(TargetAccount, Services, CancellationToken.None);
            var second = _sut.Harden(TargetAccount, Services, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, first.Status);
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, second.Status);
            Assert.Empty(ExplicitRules(_vault, TargetSid, AccessControlType.Allow));
            Assert.Equal(2, ExplicitRules(Path.Combine(_vault, ServiceLogsFolder), TargetSid, AccessControlType.Allow).Count);
            Assert.Single(ExplicitRules(Path.Combine(_vault, AppConfig.ServyServiceUIExe), TargetSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(Path.Combine(_vault, AppConfig.ServyHostExe), TargetSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(Path.Combine(_vault, KeyFile), TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public void Harden_TwoAccounts_EachKeepsItsOwnAccess()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            _sut.Harden(TargetAccount, Services, CancellationToken.None);
            var second = _sut.Harden(OtherAccount, OtherServices, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, second.Status);
            var exe = Path.Combine(_vault, AppConfig.ServyServiceUIExe);
            Assert.True(Has(AllowedRights(exe, TargetSid), FileSystemRights.ReadAndExecute));
            Assert.True(Has(AllowedRights(exe, OtherSid), FileSystemRights.ReadAndExecute));
            var key = Path.Combine(_vault, KeyFile);
            Assert.Equal(0, AllowedRights(key, TargetSid));
            Assert.Equal(0, AllowedRights(key, OtherSid));
            var db = Path.Combine(_vault, DbFile);
            Assert.Equal(0, AllowedRights(db, TargetSid));
            Assert.Equal(0, AllowedRights(db, OtherSid));
            AssertWritableFolder(Path.Combine(_vault, ServiceLogsFolder));
            AssertWritableFolder(Path.Combine(_vault, OtherServiceLogsFolder), OtherSid);
        }

        [Fact]
        public void Harden_TwoAccounts_NeitherCanReachTheLogsOfTheOther()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            _sut.Harden(TargetAccount, Services, CancellationToken.None);
            _sut.Harden(OtherAccount, OtherServices, CancellationToken.None);
            var ownLog = Path.Combine(_vault, ServiceLogsFolder, "Servy.Service.log");
            var otherLog = Path.Combine(_vault, OtherServiceLogsFolder, "Servy.Restarter.log");
            File.WriteAllText(ownLog, "one");
            File.WriteAllText(otherLog, "two");

            // Assert: each account holds no entry at all on the other's folder or on the files in it
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, OtherServiceLogsFolder), TargetSid));
            Assert.Equal(0, AllowedRights(otherLog, TargetSid));
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, ServiceLogsFolder), OtherSid));
            Assert.Equal(0, AllowedRights(ownLog, OtherSid));
            Assert.True(Has(AllowedRights(ownLog, TargetSid), FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete));
            Assert.True(Has(AllowedRights(otherLog, OtherSid), FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete));
        }

        [Fact]
        public void Harden_TwoServicesOfOneAccount_GrantsBothFolders()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();

            // Act
            var result = _sut.Harden(TargetAccount, new[] { ServiceName, OtherServiceName }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Equal(new[] { ServiceLogsFolder, OtherServiceLogsFolder }, result.GrantedFolders);
            AssertWritableFolder(Path.Combine(_vault, ServiceLogsFolder));
            AssertWritableFolder(Path.Combine(_vault, OtherServiceLogsFolder));
        }

        [Fact]
        public void Harden_ServiceTheAccountNoLongerRuns_LosesItsFolder()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: the account ran both services, and svc-two has since moved to another account
            CreateVault();
            _sut.Harden(TargetAccount, new[] { ServiceName, OtherServiceName }, CancellationToken.None);
            var otherLog = Path.Combine(_vault, OtherServiceLogsFolder, "Servy.Service.log");
            File.WriteAllText(otherLog, "two");

            // Act
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            Assert.Empty(ExplicitRules(Path.Combine(_vault, OtherServiceLogsFolder), TargetSid, AccessControlType.Allow));
            Assert.Equal(0, AllowedRights(otherLog, TargetSid));
            AssertWritableFolder(Path.Combine(_vault, ServiceLogsFolder));
        }

        [Fact]
        public void Harden_ServiceNameThatIsNotAFolderName_GrantsItsSafeFolder()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            const string unsafeName = "My:Service*";

            // Act
            var result = _sut.Harden(TargetAccount, new[] { unsafeName }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Hardened, result.Status);
            var folder = Path.Combine(_vault, ServiceLogsRoot, "My%3AService%2A");
            Assert.True(Directory.Exists(folder));
            AssertWritableFolder(folder);
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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            File.Delete(Path.Combine(_vault, ServyExePermissionsHardener.HostSettingsFileName));

            // Act
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            var result = _sut.Harden(TargetAccount, Services, CancellationToken.None);

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
            var result = _sut.Harden(account, Services, CancellationToken.None);

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
            var result = _sut.Harden(account, Services, CancellationToken.None);

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
            var hardened = await _sut.HardenAsync(TargetAccount, Services, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(hardened);
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, KeyFile), TargetSid));
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, AppConfig.ServyHostExe), TargetSid));
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
                new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = TargetAccount },
                new ServiceDto { Name = OtherServiceName, RunAsLocalSystem = false, UserAccount = @"nt authority\localservice" },
            });

            // Act
            await _sut.HardenServiceAccountsAsync(repository.Object, TestContext.Current.CancellationToken);

            // Assert
            var restarter = Path.Combine(_vault, AppConfig.ServyRestarterExe);
            Assert.True(Has(AllowedRights(restarter, TargetSid), FileSystemRights.ReadAndExecute));
            Assert.False(Has(AllowedRights(restarter, TargetSid), FileSystemRights.Delete));

            // Both spellings are one account, so it keeps the folders of both its services
            AssertWritableFolder(Path.Combine(_vault, ServiceLogsFolder));
            AssertWritableFolder(Path.Combine(_vault, OtherServiceLogsFolder));
        }

        [Fact]
        public async Task HardenServiceAsync_KeepsTheFoldersOfTheOtherServicesOfTheAccount()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: svc-two already runs under the account, and svc-one is being installed under it
            CreateVault();
            _sut.Harden(TargetAccount, OtherServices, CancellationToken.None);
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
            {
                new ServiceDto { Name = OtherServiceName, RunAsLocalSystem = false, UserAccount = TargetAccount },
            });

            // Act
            var hardened = await _sut.HardenServiceAsync(ServiceName, TargetAccount, repository.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(hardened);
            AssertWritableFolder(Path.Combine(_vault, ServiceLogsFolder));
            AssertWritableFolder(Path.Combine(_vault, OtherServiceLogsFolder));
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
            _sut.Harden(TargetAccount, Services, CancellationToken.None);
            _sut.Harden(OtherAccount, OtherServices, CancellationToken.None);

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, ServiceName, new List<ServiceDto> { new ServiceDto { Name = OtherServiceName, UserAccount = OtherAccount } }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Revoked, result.Status);
            Assert.Empty(result.Failed);
            var items = new List<string> { _vault };
            foreach (var folder in ServyExePermissionsHardener.GetWritableFolders(Services))
                items.Add(Path.Combine(_vault, folder));
            foreach (var file in new[] { AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, AppConfig.ServyHostExe, HandleFile,
                ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName, ServyExePermissionsHardener.HostSettingsFileName,
                DbFile, KeyFile, AppConfig.DbFolderName, AppConfig.SecurityFolderName, AppConfig.LogsFolderName, ServiceLogsRoot })
                items.Add(Path.Combine(_vault, file));
            foreach (var item in items)
            {
                Assert.Empty(ExplicitRules(item, TargetSid, AccessControlType.Allow));
                Assert.Empty(ExplicitRules(item, TargetSid, AccessControlType.Deny));
                Assert.Equal(0, AllowedRights(item, TargetSid));
            }

            // A file the service writes later inherits nothing, and the account that still runs a service keeps
            // exactly what it had - which, for the database and the key, is nothing either
            var laterLog = Path.Combine(_vault, ServiceLogsFolder, "later.log");
            File.WriteAllText(laterLog, "later");
            Assert.Equal(0, AllowedRights(laterLog, TargetSid));
            var otherLaterLog = Path.Combine(_vault, OtherServiceLogsFolder, "later.log");
            File.WriteAllText(otherLaterLog, "later");
            Assert.Equal(0, AllowedRights(otherLaterLog, TargetSid));
            Assert.True(Has(AllowedRights(otherLaterLog, OtherSid), FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete));
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, KeyFile), OtherSid));
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, DbFile), OtherSid));
            AssertWritableFolder(Path.Combine(_vault, OtherServiceLogsFolder), OtherSid);
        }

        [Fact]
        public void RevokeIfUnused_AccountStillRunsAService_KeepsEverything()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            _sut.Harden(TargetAccount, Services, CancellationToken.None);
            var before = Sddl(Path.Combine(_vault, KeyFile));

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, ServiceName, new List<ServiceDto> { new ServiceDto { Name = ServiceName, UserAccount = @"nt authority\localservice" } }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InUse, result.Status);
            Assert.Empty(result.Revoked);
            Assert.Equal(before, Sddl(Path.Combine(_vault, KeyFile)));
            foreach (var folder in ServyExePermissionsHardener.GetWritableFolders(Services))
                AssertWritableFolder(Path.Combine(_vault, folder));
        }

        [Fact]
        public void RevokeIfUnused_AccountStillRunsAnotherService_RevokesOnlyTheRemovedServiceFolder()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: the account runs both services, and svc-one is removed
            CreateVault();
            _sut.Harden(TargetAccount, new[] { ServiceName, OtherServiceName }, CancellationToken.None);
            var removedLog = Path.Combine(_vault, ServiceLogsFolder, "Servy.Service.log");
            File.WriteAllText(removedLog, "one");
            var exeBefore = Sddl(Path.Combine(_vault, AppConfig.ServyServiceUIExe));

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, ServiceName, new List<ServiceDto> { new ServiceDto { Name = OtherServiceName, UserAccount = TargetAccount } }, CancellationToken.None);

            // Assert: the removed service's folder and log are out of reach, the remaining one is untouched
            Assert.Equal(ExePermissionsHardeningStatus.InUse, result.Status);
            Assert.Equal(new[] { ServiceLogsFolder }, result.Revoked);
            Assert.Empty(ExplicitRules(Path.Combine(_vault, ServiceLogsFolder), TargetSid, AccessControlType.Allow));
            Assert.Equal(0, AllowedRights(removedLog, TargetSid));
            Assert.True(File.Exists(removedLog));
            AssertWritableFolder(Path.Combine(_vault, OtherServiceLogsFolder));
            Assert.Equal(exeBefore, Sddl(Path.Combine(_vault, AppConfig.ServyServiceUIExe)));
        }

        [Fact]
        public void RevokeIfUnused_LogFolderAndFileOwnedByTheAccount_AreReownedByAdministrators()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange - a log the service account created is owned by it; an elevated process creates items owned by
            //           BUILTIN\Administrators, so hand the log folder and a log the current user's own SID, which any
            //           token may set as owner, and revoke that account (#7442)
            CreateVault();
            var folder = Path.Combine(_vault, ServiceLogsFolder);
            Directory.CreateDirectory(folder);
            var log = Path.Combine(folder, "Servy.Service.log");
            File.WriteAllText(log, "one");
            using var identity = WindowsIdentity.GetCurrent();
            var folderAcl = new DirectoryInfo(folder).GetAccessControl();
            folderAcl.SetOwner(identity.User!);
            new DirectoryInfo(folder).SetAccessControl(folderAcl);
            var logAcl = new FileInfo(log).GetAccessControl();
            logAcl.SetOwner(identity.User!);
            new FileInfo(log).SetAccessControl(logAcl);
            Assert.Equal(identity.User, new DirectoryInfo(folder).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
            Assert.Equal(identity.User, new FileInfo(log).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));

            // Act
            var result = _sut.RevokeIfUnused(identity.Name, ServiceName, new List<ServiceDto>(), CancellationToken.None);

            // Assert - neither item is left to the account, so it holds no implicit right to rewrite their DACLs
            Assert.Equal(ExePermissionsHardeningStatus.Revoked, result.Status);
            Assert.Empty(result.Failed);
            Assert.Contains(ServiceLogsFolder, result.Revoked);
            Assert.Contains(Path.Combine(ServiceLogsFolder, "Servy.Service.log"), result.Revoked);
            Assert.Equal(AdministratorsSid, new DirectoryInfo(folder).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
            Assert.Equal(AdministratorsSid, new FileInfo(log).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
            Assert.True(File.Exists(log));
        }

        [Fact]
        public void RevokeIfUnused_FileIsASymbolicLink_IsNotTouchedAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            _sut.Harden(TargetAccount, Services, CancellationToken.None);
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
            var result = _sut.RevokeIfUnused(TargetAccount, ServiceName, new List<ServiceDto>(), CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Equal(new[] { AppConfig.ServyRestarterExe }, result.Failed);
            Assert.Equal(before, new FileInfo(outside).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
            Assert.Empty(ExplicitRules(Path.Combine(_vault, KeyFile), TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public void RevokeIfUnused_VaultIsAJunction_RemovesNothingOnItsTargetAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: the grant is written on the real directory, before the junction exists,
            // so the arrange step itself never goes through the link.
            var realVault = Path.Combine(TempDirectory, "real-vault");
            Directory.CreateDirectory(realVault);
            var real = new DirectoryInfo(realVault);
            var acl = real.GetAccessControl(AccessControlSections.Access);
            acl.AddAccessRule(new FileSystemAccessRule(TargetSid, FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            real.SetAccessControl(acl);
            RunCmd($"mklink /J \"{_vault}\" \"{realVault}\"");

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, ServiceName, new List<ServiceDto>(), CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Equal(new[] { _vault }, result.Failed);
            Assert.Empty(result.Revoked);
            Assert.NotEmpty(ExplicitRules(realVault, TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public void RevokeIfUnused_ClosedFolderIsAJunction_RemovesNothingOnItsTargetAndFails()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: a file outside the vault holds an entry for the target account, and the vault's logs\ is a
            // junction to its folder. The grant is written before the junction exists, so the arrange step never
            // goes through the link.
            CreateVault();
            var outside = Path.Combine(TempDirectory, "outside-logs");
            Directory.CreateDirectory(outside);
            var outsideFile = Path.Combine(outside, "other.log");
            File.WriteAllText(outsideFile, "test");
            ProtectWithGrant(outsideFile, FileSystemRights.Read);
            RunCmd($"mklink /J \"{Path.Combine(_vault, AppConfig.LogsFolderName)}\" \"{outside}\"");

            // Act
            var result = _sut.RevokeIfUnused(TargetAccount, ServiceName, new List<ServiceDto>(), CancellationToken.None);

            // Assert: the link is refused, and the walk never reached the file behind it
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Contains(AppConfig.LogsFolderName, result.Failed);
            Assert.DoesNotContain(Path.Combine(AppConfig.LogsFolderName, "other.log"), result.Revoked);
            Assert.NotEmpty(ExplicitRules(outsideFile, TargetSid, AccessControlType.Allow));
        }

        [Fact]
        public async Task RevokeIfUnusedAsync_LastServiceOfTheAccountRemoved_RevokesAndReturnsTrue()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            CreateVault();
            _sut.Harden(TargetAccount, Services, CancellationToken.None);
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
            {
                new ServiceDto { Name = "local-system", RunAsLocalSystem = true },
            });

            // Act
            var revoked = await _sut.RevokeIfUnusedAsync(TargetAccount, ServiceName, repository.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(revoked);
            Assert.Equal(0, AllowedRights(Path.Combine(_vault, KeyFile), TargetSid));
            Assert.Empty(ExplicitRules(Path.Combine(_vault, AppConfig.DbFolderName), TargetSid, AccessControlType.Allow));
            Assert.Empty(ExplicitRules(Path.Combine(_vault, ServiceLogsFolder), TargetSid, AccessControlType.Allow));
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
                AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, AppConfig.ServyHostExe, HandleFile,
                ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName, ServyExePermissionsHardener.HostSettingsFileName,
                DbFile, KeyFile,
            })
            {
                File.WriteAllText(Path.Combine(_vault, file), "test");
            }
        }

        /// <summary>
        /// Gives a file the protected ACL an earlier hardening wrote: no inheritance, Full Control for Administrators and
        /// Local System, and <paramref name="rights"/> for the target account.
        /// </summary>
        private static void ProtectWithGrant(string file, FileSystemRights rights)
        {
            var info = new FileInfo(file);
            var acl = info.GetAccessControl(AccessControlSections.Access);
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new FileSystemAccessRule(AdministratorsSid, FileSystemRights.FullControl, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(TargetSid, rights, AccessControlType.Allow));
            info.SetAccessControl(acl);
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
        /// the files created in it, for the target account or <paramref name="sid"/>. Both rules are bounded from above as
        /// well as from below: neither carries Change Permissions (WRITE_DAC) or Take Ownership, which would let the
        /// target rewrite the ACL it was just given (#7163).
        /// </summary>
        private static void AssertWritableFolder(string path, SecurityIdentifier? sid = null)
        {
            // Arrange
            sid = sid ?? TargetSid;
            var rules = ExplicitRules(path, sid, AccessControlType.Allow);

            // Assert
            Assert.Equal(2, rules.Count);

            var self = Assert.Single(rules, r => r.InheritanceFlags == InheritanceFlags.None);
            Assert.True(Has((int)self.FileSystemRights, FileSystemRights.Read | FileSystemRights.CreateFiles));
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.Delete));
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.DeleteSubdirectoriesAndFiles));

            // WRITE_DAC on the folder would let the target grant itself Delete Subdirectories And Files and then remove
            // another service's log whatever that file's own ACL says, because FILE_DELETE_CHILD overrides the child's.
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.ChangePermissions), $"{path} is not re-ACLable by the target");
            Assert.False(Has((int)self.FileSystemRights, FileSystemRights.TakeOwnership), $"{path} is not re-ownable by the target");

            var files = Assert.Single(rules, r => r.InheritanceFlags == InheritanceFlags.ObjectInherit);
            Assert.Equal(PropagationFlags.InheritOnly, files.PropagationFlags);
            var expected = ServyExePermissionsHardener.GetWritableFolderFileRights();
            Assert.True(Has((int)files.FileSystemRights, expected));

            // The rule the files created in it inherit carries neither of those two rights either.
            Assert.False(Has((int)files.FileSystemRights, FileSystemRights.ChangePermissions), $"the files in {path} are not re-ACLable by the target");
            Assert.False(Has((int)files.FileSystemRights, FileSystemRights.TakeOwnership), $"the files in {path} are not re-ownable by the target");

            Assert.False(Has(AllowedRights(path, sid), FileSystemRights.Delete), $"{path} itself is not deletable");
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
