using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.UnitTests.Security
{
    /// <summary>
    /// Covers the decisions <see cref="ServyExePermissionsHardener"/> makes before and around the ACL writes: which
    /// status each precondition leads to, which files it hardens with which rights, how each outcome is reported, and
    /// which accounts a re-apply covers. The seams replace elevation, account resolution and group membership, and
    /// the vault is an empty temporary directory, so no file ACL is ever rewritten here; the ACL writes themselves are
    /// covered by <c>ServyExePermissionsHardenerIntegrationTests</c>. Outcomes are told apart by the log, hence the
    /// sequential logger collection.
    /// </summary>
    [Collection(LoggerCollection.Name)]
    public class ServyExePermissionsHardenerTests : TempDirectoryTestBase
    {
        private static readonly SecurityIdentifier LocalServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);

        #region Constructor and candidates

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_BlankVaultDirectory_Throws(string vaultDirectory)
        {
            // Act
            var ex = Record.Exception(() => new ServyExePermissionsHardener(vaultDirectory));

            // Assert
            var argumentException = Assert.IsType<ArgumentException>(ex);
            Assert.Equal("vaultDirectory", argumentException.ParamName);
        }

        [Fact]
        public void Constructor_Default_HardensTheProgramDataVault()
        {
            // Act
            var sut = new ServyExePermissionsHardener();

            // Assert
            Assert.Equal(AppConfig.ProgramDataPath, sut.VaultDirectory);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("LocalSystem", false)]
        [InlineData(@"NT AUTHORITY\SYSTEM", false)]
        [InlineData(@"  .\LocalSystem  ", false)]
        [InlineData(@".\svc-servy", true)]
        [InlineData(@"NT AUTHORITY\LocalService", true)]
        [InlineData(@"DOMAIN\gMSA$", true)]
        public void IsHardeningCandidate_EveryAccountButLocalSystem(string account, bool expected)
        {
            // Act
            var actual = ServyExePermissionsHardener.IsHardeningCandidate(account);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetServiceAccounts_KeepsDistinctNonLocalSystemAccountsInFirstSeenOrder()
        {
            // Arrange
            var services = new List<ServiceDto>
            {
                new ServiceDto { Name = "a", RunAsLocalSystem = true, UserAccount = null },
                new ServiceDto { Name = "b", RunAsLocalSystem = false, UserAccount = @" .\svc-one " },
                new ServiceDto { Name = "c", RunAsLocalSystem = false, UserAccount = @".\SVC-ONE" },
                new ServiceDto { Name = "d", RunAsLocalSystem = null, UserAccount = @"NT AUTHORITY\LocalService" },
                new ServiceDto { Name = "e", RunAsLocalSystem = false, UserAccount = "   " },
                new ServiceDto { Name = "f", RunAsLocalSystem = false, UserAccount = "LocalSystem" },
                new ServiceDto { Name = "g", RunAsLocalSystem = true, UserAccount = @".\stale-account" },
                null,
            };

            // Act
            var accounts = ServyExePermissionsHardener.GetServiceAccounts(services);

            // Assert
            Assert.Equal(new[] { @".\svc-one", @"NT AUTHORITY\LocalService" }, accounts);
        }

        [Fact]
        public void GetServiceAccounts_NullServices_ReturnsEmpty()
        {
            // Act
            var accounts = ServyExePermissionsHardener.GetServiceAccounts(null);

            // Assert
            Assert.Empty(accounts);
        }

        #endregion

        #region Harden preconditions

        [Fact]
        public void Harden_NotElevated_ChangesNothing()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { Elevated = false };

            // Act
            var result = sut.Harden("svc", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.NotElevated, result.Status);
            Assert.Equal(0, sut.ResolveCalls);
            Assert.False(result.VaultAccessGranted);
        }

        [Fact]
        public void Harden_VaultMissing_ChangesNothing()
        {
            // Arrange
            var sut = new TestableHardener(Path.Combine(TempDirectory, "absent"));

            // Act
            var result = sut.Harden("svc", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.VaultNotFound, result.Status);
            Assert.Equal(0, sut.ResolveCalls);
            Assert.False(Directory.Exists(Path.Combine(TempDirectory, "absent")));
        }

        [Fact]
        public void Harden_AccountDoesNotResolve_ChangesNothing()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { Sid = null };

            // Act
            var result = sut.Harden("nobody", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InvalidAccount, result.Status);
            Assert.Contains("could not be resolved", result.Reason);
            Assert.False(TargetHasVaultAce(LocalServiceSid));
        }

        [Theory]
        [InlineData(WellKnownSidType.WorldSid)]
        [InlineData(WellKnownSidType.BuiltinUsersSid)]
        [InlineData(WellKnownSidType.AuthenticatedUserSid)]
        public void Harden_BroadGroup_IsRefused(WellKnownSidType broadGroup)
        {
            // Arrange
            var sid = new SecurityIdentifier(broadGroup, null);
            var sut = new TestableHardener(TempDirectory) { Sid = sid };

            // Act
            var result = sut.Harden("group", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InvalidAccount, result.Status);
            Assert.Contains("broad group", result.Reason);
            Assert.False(TargetHasVaultAce(sid));
        }

        [Theory]
        [InlineData(WellKnownSidType.BuiltinAdministratorsSid)]
        [InlineData(WellKnownSidType.LocalSystemSid)]
        public void Harden_AdministratorsOrLocalSystem_IsSkippedWithoutAMembershipQuery(WellKnownSidType principal)
        {
            // Arrange
            var sid = new SecurityIdentifier(principal, null);
            var sut = new TestableHardener(TempDirectory) { Sid = sid };

            // Act
            var result = sut.Harden("admin", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Skipped, result.Status);
            Assert.Contains("protected administrative principal", result.Reason);
            Assert.Equal(0, sut.MembershipCalls);
            Assert.False(result.VaultAccessGranted);
        }

        [Fact]
        public void Harden_MemberOfAdministrators_IsSkipped()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { IsMember = true };

            // Act
            var result = sut.Harden("admin-member", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Skipped, result.Status);
            Assert.Contains("member of Administrators", result.Reason);
            Assert.False(result.VaultAccessGranted);
            Assert.False(TargetHasVaultAce(LocalServiceSid));
        }

        [Fact]
        public void Harden_NotAMember_GrantsTheVaultAndReportsEveryRequiredFileMissing()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { IsMember = false };

            // Act
            var result = sut.Harden("svc", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Incomplete, result.Status);
            Assert.True(result.VaultAccessGranted);
            Assert.True(TargetHasVaultAce(LocalServiceSid));
            Assert.Empty(result.Hardened);
            Assert.Empty(result.Failed);
            Assert.Contains(AppConfig.ServyServiceUIExe, result.Missing);
            Assert.Contains(AppConfig.ServyServiceCLIExe, result.Missing);
            Assert.Contains(AppConfig.ServyRestarterExe, result.Missing);
            Assert.Contains(Path.Combine(AppConfig.DbFolderName, AppConfig.DatabaseFileName), result.Missing);
            Assert.Contains(Path.Combine(AppConfig.SecurityFolderName, AppConfig.AESKeyFileName), result.Missing);
            Assert.Equal(new[] { AppConfig.ServyServiceUIExe + ".config", AppConfig.ServyServiceCLIExe + ".config", AppConfig.ServyRestarterExe + ".config" }, result.Skipped);
        }

        [Fact]
        public async Task Harden_MembershipUnknown_HardensAndSaysSo()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { IsMember = null };

            // Act
            var capture = await LogCapture.RunAsync(() => Task.FromResult(sut.Harden("svc", CancellationToken.None)));

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Incomplete, capture.Result.Status);
            Assert.True(capture.Result.VaultAccessGranted);
            Assert.Contains("Could not rule out that 'svc' is a member of Administrators", capture.Log);
        }

        [Fact]
        public void Harden_Cancelled_StopsBeforeTheFiles()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var ex = Record.Exception(() => sut.Harden("svc", cts.Token));

                // Assert
                Assert.IsAssignableFrom<OperationCanceledException>(ex);
            }
        }

        #endregion

        #region GetTargetFiles

        [Fact]
        public void GetTargetFiles_ListsEveryFileWithItsRights()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);

            // Act
            var targets = sut.GetTargetFiles().ToDictionary(t => t.RelativePath);

            // Assert
            foreach (var exe in new[] { AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, AppConfig.HandleExeFileName + ".exe" })
            {
                Assert.Equal(FileSystemRights.ReadAndExecute, targets[exe].Rights);
                Assert.False(targets[exe].Optional);
                Assert.False(targets[exe].PreserveInherited);
            }

            foreach (var config in new[] { AppConfig.ServyServiceUIExe + ".config", AppConfig.ServyServiceCLIExe + ".config", AppConfig.ServyRestarterExe + ".config" })
            {
                Assert.Equal(FileSystemRights.Read, targets[config].Rights);
                Assert.True(targets[config].Optional);
                Assert.False(targets[config].PreserveInherited);
            }

            var db = targets[Path.Combine("db", "Servy.db")];
            Assert.Equal(FileSystemRights.Read | FileSystemRights.Write, db.Rights);
            Assert.True(db.PreserveInherited);
            Assert.False(db.Optional);

            var key = targets[Path.Combine("security", "aes_key.dat")];
            Assert.Equal(FileSystemRights.Read, key.Rights);
            Assert.True(key.PreserveInherited);
            Assert.False(key.Optional);
            Assert.Equal(0, (int)(key.Rights & (FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.Delete)));
        }

        [Fact]
        public void GetTargetFiles_HardensEveryDllInTheVaultRootAsABinary()
        {
            // Arrange
            File.WriteAllText(Path.Combine(TempDirectory, "Servy.Core.dll"), "x");
            File.WriteAllText(Path.Combine(TempDirectory, "Newtonsoft.Json.DLL"), "x");
            Directory.CreateDirectory(Path.Combine(TempDirectory, "logs"));
            File.WriteAllText(Path.Combine(TempDirectory, "logs", "nested.dll"), "x");
            var sut = new TestableHardener(TempDirectory);

            // Act
            var targets = sut.GetTargetFiles();

            // Assert
            var dlls = targets.Where(t => t.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Equal(new[] { "Newtonsoft.Json.DLL", "Servy.Core.dll" }, dlls.Select(t => t.RelativePath));
            Assert.All(dlls, d => Assert.Equal(FileSystemRights.ReadAndExecute, d.Rights));
            Assert.All(dlls, d => Assert.False(d.Optional));
        }

        #endregion

        #region ReportResult

        [Theory]
        [InlineData(ExePermissionsHardeningStatus.Hardened, true)]
        [InlineData(ExePermissionsHardeningStatus.Incomplete, true)]
        [InlineData(ExePermissionsHardeningStatus.Skipped, true)]
        [InlineData(ExePermissionsHardeningStatus.Failed, false)]
        [InlineData(ExePermissionsHardeningStatus.NotElevated, false)]
        [InlineData(ExePermissionsHardeningStatus.VaultNotFound, false)]
        [InlineData(ExePermissionsHardeningStatus.InvalidAccount, false)]
        public void ReportResult_OnlyAnOutcomeThatLeftTheAccountUnhardenedIsAFailure(ExePermissionsHardeningStatus status, bool expected)
        {
            // Arrange
            var result = new ExePermissionsHardeningResult("svc").Complete(status, "reason");

            // Act
            var actual = LogCapture.Run(() => ServyExePermissionsHardener.ReportResult(result));

            // Assert
            Assert.Equal(expected, actual.Result);
        }

        [Fact]
        public void ReportResult_Incomplete_NamesTheMissingFiles()
        {
            // Arrange
            var result = new ExePermissionsHardeningResult("svc");
            result.AddHardened("Servy.Service.exe");
            result.AddMissing("Servy.Service.CLI.exe");
            result.Complete(ExePermissionsHardeningStatus.Incomplete);

            // Act
            var capture = LogCapture.Run(() => ServyExePermissionsHardener.ReportResult(result));

            // Assert
            Assert.Contains("1 files locked down", capture.Log);
            Assert.Contains("Not present yet, hardened once extracted: Servy.Service.CLI.exe", capture.Log);
        }

        [Fact]
        public void ReportResult_Failed_NamesTheFailedFilesAtErrorLevel()
        {
            // Arrange
            var result = new ExePermissionsHardeningResult("svc");
            result.AddFailed("Servy.Restarter.exe");
            result.Complete(ExePermissionsHardeningStatus.Failed);

            // Act
            var capture = LogCapture.Run(() => ServyExePermissionsHardener.ReportResult(result), LogLevel.Error);

            // Assert
            Assert.False(capture.Result);
            Assert.Contains("failed on: Servy.Restarter.exe", capture.Log);
        }

        [Fact]
        public void ReportResult_NotApplied_GivesTheReason()
        {
            // Arrange
            var result = new ExePermissionsHardeningResult("svc").Complete(ExePermissionsHardeningStatus.InvalidAccount, "the account could not be resolved");

            // Act
            var capture = LogCapture.Run(() => ServyExePermissionsHardener.ReportResult(result), LogLevel.Error);

            // Assert
            Assert.Contains("for 'svc' was not applied: the account could not be resolved", capture.Log);
        }

        #endregion

        #region HardenAsync

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task HardenAsync_BlankAccount_ReturnsFalseWithoutResolvingIt(string account)
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);

            // Act
            var result = await sut.HardenAsync(account, CancellationToken.None);

            // Assert
            Assert.False(result);
            Assert.Equal(0, sut.ResolveCalls);
        }

        [Theory]
        [InlineData("LocalSystem")]
        [InlineData(@"NT AUTHORITY\SYSTEM")]
        public async Task HardenAsync_LocalSystem_ReturnsTrueWithoutResolvingIt(string account)
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);

            // Act
            var result = await sut.HardenAsync(account, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(0, sut.ResolveCalls);
            Assert.False(TargetHasVaultAce(LocalServiceSid));
        }

        [Fact]
        public async Task HardenAsync_TrimsTheAccountAndReportsTheOutcome()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { IsMember = false };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync(@"  .\svc-servy  ", CancellationToken.None));

            // Assert
            Assert.True(capture.Result);
            Assert.Equal(@".\svc-servy", sut.LastResolvedAccount);
            Assert.Contains(@"Hardened Servy's vault for '.\svc-servy'", capture.Log);
        }

        [Fact]
        public async Task HardenAsync_UnexpectedException_ReturnsFalseAndLogsIt()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { ResolveException = new InvalidOperationException("LSA unavailable") };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Contains("Executable permission hardening for 'svc' failed.", capture.Log);
            Assert.Contains("LSA unavailable", capture.Log);
        }

        [Fact]
        public async Task HardenAsync_Cancelled_ReturnsFalseInsteadOfThrowing()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", cts.Token));

                // Assert
                Assert.False(capture.Result);
                Assert.Equal(0, sut.ResolveCalls);
                Assert.Contains("was cancelled", capture.Log);
            }
        }

        [Fact]
        public async Task HardenAsync_NotElevated_ReturnsFalse()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { Elevated = false };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Contains("was not applied: the process is not elevated", capture.Log);
        }

        #endregion

        #region HardenServiceAccountsAsync

        [Fact]
        public async Task HardenServiceAccountsAsync_NullRepository_Throws()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.HardenServiceAccountsAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task HardenServiceAccountsAsync_HardensEachDistinctServiceAccountOnce()
        {
            // Arrange
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
            {
                new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = @".\svc-one" },
                new ServiceDto { Name = "b", RunAsLocalSystem = false, UserAccount = @".\SVC-ONE" },
                new ServiceDto { Name = "c", RunAsLocalSystem = true },
                new ServiceDto { Name = "d", RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\NetworkService" },
            });
            var sut = new TestableHardener(TempDirectory) { RecordOnly = true };

            // Act
            await sut.HardenServiceAccountsAsync(repository.Object, CancellationToken.None);

            // Assert
            Assert.Equal(new[] { @".\svc-one", @"NT AUTHORITY\NetworkService" }, sut.HardenedAccounts);
            repository.Verify(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HardenServiceAccountsAsync_NotElevated_DoesNotReadTheRepository()
        {
            // Arrange
            var repository = new Mock<IServiceRepository>();
            var sut = new TestableHardener(TempDirectory) { Elevated = false, RecordOnly = true };

            // Act
            await sut.HardenServiceAccountsAsync(repository.Object, CancellationToken.None);

            // Assert
            repository.Verify(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(sut.HardenedAccounts);
        }

        [Fact]
        public async Task HardenServiceAccountsAsync_RepositoryThrows_LogsAndReturns()
        {
            // Arrange
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database locked"));
            var sut = new TestableHardener(TempDirectory) { RecordOnly = true };

            // Act
            var log = await LogCapture.RunAsync(() => sut.HardenServiceAccountsAsync(repository.Object, CancellationToken.None));

            // Assert
            Assert.Empty(sut.HardenedAccounts);
            Assert.Contains("Failed to read the service accounts", log);
            Assert.Contains("database locked", log);
        }

        [Fact]
        public async Task HardenServiceAccountsAsync_RepositoryCancelled_Returns()
        {
            // Arrange
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
            var sut = new TestableHardener(TempDirectory) { RecordOnly = true };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.HardenServiceAccountsAsync(repository.Object, CancellationToken.None));

            // Assert
            Assert.Null(ex);
            Assert.Empty(sut.HardenedAccounts);
        }

        [Fact]
        public async Task HardenServiceAccountsAsync_CancelledBetweenAccounts_StopsBeforeTheNext()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                var repository = new Mock<IServiceRepository>();
                repository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
                {
                    new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = "first" },
                    new ServiceDto { Name = "b", RunAsLocalSystem = false, UserAccount = "second" },
                });
                var sut = new TestableHardener(TempDirectory) { RecordOnly = true, OnHarden = cts.Cancel };

                // Act
                await sut.HardenServiceAccountsAsync(repository.Object, cts.Token);

                // Assert
                Assert.Equal(new[] { "first" }, sut.HardenedAccounts);
            }
        }

        #endregion

        /// <summary>
        /// Whether the vault directory carries an explicit entry for <paramref name="sid"/>.
        /// </summary>
        private bool TargetHasVaultAce(SecurityIdentifier sid)
        {
            var acl = new DirectoryInfo(TempDirectory).GetAccessControl(AccessControlSections.Access);
            return acl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Any(r => sid.Equals(r.IdentityReference));
        }

        /// <summary>
        /// Replaces elevation, account resolution and group membership with settable values, and can record the
        /// accounts <see cref="HardenAsync"/> is called for instead of hardening them.
        /// </summary>
        private sealed class TestableHardener : ServyExePermissionsHardener
        {
            public TestableHardener(string vaultDirectory) : base(vaultDirectory)
            {
            }

            public bool Elevated { get; set; } = true;

            public SecurityIdentifier Sid { get; set; } = LocalServiceSid;

            public Exception ResolveException { get; set; }

            public bool? IsMember { get; set; } = false;

            public bool RecordOnly { get; set; }

            public Action OnHarden { get; set; }

            public int ResolveCalls { get; private set; }

            public int MembershipCalls { get; private set; }

            public string LastResolvedAccount { get; private set; }

            public List<string> HardenedAccounts { get; } = new List<string>();

            public override Task<bool> HardenAsync(string targetAccount, CancellationToken cancellationToken)
            {
                if (!RecordOnly)
                    return base.HardenAsync(targetAccount, cancellationToken);

                HardenedAccounts.Add(targetAccount);
                OnHarden?.Invoke();
                return Task.FromResult(true);
            }

            protected override bool IsProcessElevated() => Elevated;

            protected override SecurityIdentifier ResolveAccount(string account)
            {
                ResolveCalls++;
                LastResolvedAccount = account;
                if (ResolveException != null)
                    throw ResolveException;
                return Sid;
            }

            protected override bool? IsAdministratorsMember(SecurityIdentifier sid)
            {
                MembershipCalls++;
                return IsMember;
            }
        }
    }
}
