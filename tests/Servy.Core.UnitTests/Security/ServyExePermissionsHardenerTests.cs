using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.UnitTests.Security
{
    /// <summary>
    /// Covers the decisions <see cref="ServyExePermissionsHardener"/> makes before and around the ACL writes: which
    /// status each precondition leads to, which files it hardens with which rights, how each outcome is reported, and
    /// which accounts a re-apply covers. The seams replace elevation, account resolution, group membership and the
    /// hard link count, and the vault is a temporary directory, so every ACL rewritten here belongs to an item the test
    /// created; what the hardening leaves on each file is covered by
    /// <c>ServyExePermissionsHardenerIntegrationTests</c>. Outcomes are told apart by the log, hence the
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
        public void Constructor_BlankVaultDirectory_Throws(string? vaultDirectory)
        {
            // Act
            var ex = Record.Exception(() => new ServyExePermissionsHardener(vaultDirectory!));

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
        public void IsHardeningCandidate_EveryAccountButLocalSystem(string? account, bool expected)
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
                null!,
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
            var accounts = ServyExePermissionsHardener.GetServiceAccounts(null!);

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
            Assert.Empty(result.GrantedFolders);
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
            Assert.Empty(result.GrantedFolders);
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
            Assert.Empty(result.GrantedFolders);
            Assert.False(TargetHasVaultAce(LocalServiceSid));
        }

        [Fact]
        public void Harden_NotAMember_GrantsTheWritableFoldersAndReportsEveryRequiredFileMissing()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { IsMember = false };

            // Act
            var result = sut.Harden("svc", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Incomplete, result.Status);
            Assert.Equal(new[] { AppConfig.DbFolderName, AppConfig.LogsFolderName, AppConfig.RecoveryFolderName }, result.GrantedFolders);
            Assert.False(TargetHasVaultAce(LocalServiceSid));
            foreach (var folder in result.GrantedFolders)
            {
                Assert.True(Directory.Exists(Path.Combine(TempDirectory, folder)), $"{folder} was created");
                Assert.True(TargetHasAce(Path.Combine(TempDirectory, folder), LocalServiceSid), $"{folder} was granted");
            }
            Assert.Empty(result.Hardened);
            Assert.Empty(result.Failed);
            Assert.Contains(AppConfig.ServyServiceUIExe, result.Missing);
            Assert.Contains(AppConfig.ServyServiceCLIExe, result.Missing);
            Assert.Contains(AppConfig.ServyRestarterExe, result.Missing);
            Assert.Contains(Path.Combine(AppConfig.DbFolderName, AppConfig.DatabaseFileName), result.Missing);
            Assert.Contains(Path.Combine(AppConfig.SecurityFolderName, AppConfig.AESKeyFileName), result.Missing);
            Assert.Equal(new[] { ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName }, result.Skipped);
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
            Assert.Equal(3, capture.Result.GrantedFolders.Count);
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

        [Fact]
        public void Harden_CancelledBeforeTheCall_GrantsNoFolder()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { IsMember = false };
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var ex = Record.Exception(() => sut.Harden("svc", cts.Token));

                // Assert: GrantFolderAccess creates each folder it grants, so the check before the first one
                // is what keeps every writable folder absent
                Assert.IsAssignableFrom<OperationCanceledException>(ex);
                foreach (var folder in ServyExePermissionsHardener.GetWritableFolders())
                    Assert.False(Directory.Exists(Path.Combine(TempDirectory, folder)), $"{folder} was granted after cancellation");
            }
        }

        [Fact]
        public void Harden_CancelledDuringTheFirstFile_DoesNotTouchTheNextFile()
        {
            // Arrange
            File.WriteAllText(Path.Combine(TempDirectory, AppConfig.ServyServiceUIExe), "ui");
            File.WriteAllText(Path.Combine(TempDirectory, AppConfig.ServyServiceCLIExe), "cli");
            using (var cts = new CancellationTokenSource())
            {
                var sut = new TestableHardener(TempDirectory)
                {
                    IsMember = false,
                    HardLinkCount = _ => { cts.Cancel(); return 1; }
                };

                // Act
                var ex = Record.Exception(() => sut.Harden("svc", cts.Token));

                // Assert: the first file finishes, the check before the second one stops the loop
                Assert.IsAssignableFrom<OperationCanceledException>(ex);
                Assert.Equal(new[] { AppConfig.ServyServiceUIExe }, sut.HardLinkQueries);
            }
        }

        [Fact]
        public void Harden_OneFileThrows_IsReportedAsFailedAndTheNextFileIsStillProcessed()
        {
            // Arrange
            File.WriteAllText(Path.Combine(TempDirectory, AppConfig.ServyServiceUIExe), "ui");
            File.WriteAllText(Path.Combine(TempDirectory, AppConfig.ServyServiceCLIExe), "cli");
            var sut = new TestableHardener(TempDirectory)
            {
                IsMember = false,
                HardLinkCount = path => Path.GetFileName(path) == AppConfig.ServyServiceUIExe
                    ? throw new IOException("The process cannot access the file because it is being used by another process.")
                    : 1
            };

            // Act
            var result = sut.Harden("svc", CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, result.Status);
            Assert.Contains(AppConfig.ServyServiceUIExe, result.Failed);
            Assert.DoesNotContain(AppConfig.ServyServiceUIExe, result.Hardened);
            Assert.Contains(AppConfig.ServyServiceCLIExe, sut.HardLinkQueries);
        }

        [Fact]
        public async Task Harden_OneFolderCannotBeCreated_IsReportedAsFailedAndTheOtherFoldersAreStillGranted()
        {
            // Arrange: a regular file sits where the logs folder belongs, so creating that folder throws
            File.WriteAllText(Path.Combine(TempDirectory, AppConfig.LogsFolderName), "not a folder");
            var sut = new TestableHardener(TempDirectory) { IsMember = false };

            // Act
            var capture = await LogCapture.RunAsync(() => Task.FromResult(sut.Harden("svc", CancellationToken.None)));

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, capture.Result.Status);
            Assert.Contains(AppConfig.LogsFolderName, capture.Result.Failed);
            Assert.Equal(new[] { AppConfig.DbFolderName, AppConfig.RecoveryFolderName }, capture.Result.GrantedFolders);
            Assert.Contains($"Failed to grant 'svc' access to '{AppConfig.LogsFolderName}'", capture.Log);
        }

        #endregion

        #region GetTargetFiles

        [Fact]
        public void GetWritableFolders_AreTheDatabaseTheLogsAndTheRecoveryState()
        {
            // Act
            var folders = ServyExePermissionsHardener.GetWritableFolders();

            // Assert: the security folder and the vault root are deliberately absent
            Assert.Equal(new[] { "db", "logs", "recovery" }, folders);
        }

        [Theory]
        [InlineData("db")]
        [InlineData("logs")]
        public void GetWritableFolderFileRights_DatabaseAndLogs_AreReadWriteDelete(string folder)
        {
            // Act
            var rights = ServyExePermissionsHardener.GetWritableFolderFileRights(folder);

            // Assert: SQLite deletes its side files and the logger rotates its files, so Delete is needed here
            Assert.Equal(FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete, rights);
        }

        [Theory]
        [InlineData("recovery")]
        [InlineData("RECOVERY")]
        public void GetWritableFolderFileRights_Recovery_IsReadWriteWithoutDelete(string folder)
        {
            // Act
            var rights = ServyExePermissionsHardener.GetWritableFolderFileRights(folder);

            // Assert: the restart-attempts counter is rewritten in place, so it never needs Delete (#7241)
            Assert.Equal(FileSystemRights.Read | FileSystemRights.Write, rights);
            Assert.Equal(0, (int)(rights & FileSystemRights.Delete));
        }

        [Fact]
        public void GetTargetFiles_ListsEveryFileWithItsRights()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);

            // Act
            var targets = sut.GetTargetFiles().ToDictionary(t => t.RelativePath);

            // Assert
            foreach (var exe in new[] { AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe })
            {
                Assert.Equal(FileSystemRights.ReadAndExecute, targets[exe].Rights);
                Assert.False(targets[exe].Optional);
            }

            foreach (var settings in new[] { ServyExePermissionsHardener.ServiceSettingsFileName, ServyExePermissionsHardener.RestarterSettingsFileName })
            {
                Assert.Equal(FileSystemRights.Read, targets[settings].Rights);
                Assert.True(targets[settings].Optional);
            }

            var db = targets[Path.Combine("db", "Servy.db")];
            Assert.Equal(FileSystemRights.Read | FileSystemRights.Write, db.Rights);
            Assert.False(db.Optional);

            var key = targets[Path.Combine("security", "aes_key.dat")];
            Assert.Equal(FileSystemRights.Read, key.Rights);
            Assert.False(key.Optional);
            Assert.Equal(0, (int)(key.Rights & (FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.Delete)));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void GetTargetFiles_HardensTheHandleBinariesThatArePresent(bool x64Present, bool arm64Present)
        {
            // Arrange
            var x64 = AppConfig.HandleExeX64FileName + ".exe";
            var arm64 = AppConfig.HandleExeARM64FileName + ".exe";
            if (x64Present) File.WriteAllText(Path.Combine(TempDirectory, x64), "x");
            if (arm64Present) File.WriteAllText(Path.Combine(TempDirectory, arm64), "a");
            var sut = new TestableHardener(TempDirectory);

            // Act
            var names = sut.GetTargetFiles().Select(t => t.RelativePath).ToList();

            // Assert
            if (!x64Present && !arm64Present)
            {
                var expected = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? arm64 : x64;
                Assert.Single(names, n => n == x64 || n == arm64);
                Assert.Contains(expected, names);
            }
            else
            {
                Assert.Equal(x64Present, names.Contains(x64));
                Assert.Equal(arm64Present, names.Contains(arm64));
            }
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
            var result = await sut.HardenAsync(account, TestContext.Current.CancellationToken);

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
            var result = await sut.HardenAsync(account, TestContext.Current.CancellationToken);

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
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync(@"  .\svc-servy  ", TestContext.Current.CancellationToken));

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
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", TestContext.Current.CancellationToken));

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
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", TestContext.Current.CancellationToken));

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
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.HardenServiceAccountsAsync(null!, TestContext.Current.CancellationToken));
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
            await sut.HardenServiceAccountsAsync(repository.Object, TestContext.Current.CancellationToken);

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
            await sut.HardenServiceAccountsAsync(repository.Object, TestContext.Current.CancellationToken);

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
            var log = await LogCapture.RunAsync(() => sut.HardenServiceAccountsAsync(repository.Object, TestContext.Current.CancellationToken));

            // Assert
            Assert.Empty(sut.HardenedAccounts);
            Assert.Contains("Failed to read the service accounts", log);
            Assert.Contains("database locked", log);
        }

        [Fact]
        public async Task HardenServiceAccountsAsync_RepositoryCancelled_WarnsInsteadOfReportingAReadFailure()
        {
            // Arrange
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
            var sut = new TestableHardener(TempDirectory) { RecordOnly = true };

            // Act
            var log = await LogCapture.RunAsync(() => sut.HardenServiceAccountsAsync(repository.Object, TestContext.Current.CancellationToken));

            // Assert
            Assert.Empty(sut.HardenedAccounts);
            Assert.Contains("Executable permission hardening of the service accounts was cancelled.", log);
            Assert.DoesNotContain("Failed to read the service accounts", log);
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

        #region RevokeIfUnused

        private static readonly SecurityIdentifier NetworkServiceSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

        [Fact]
        public void RevokeIfUnused_AccountDoesNotResolve_ChangesNothing()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory) { Sid = null };

            // Act
            var result = sut.RevokeIfUnused("ghost", new List<string>(), CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InvalidAccount, result.Status);
            Assert.Empty(result.Revoked);
        }

        [Theory]
        [InlineData(WellKnownSidType.WorldSid, ExePermissionsHardeningStatus.InvalidAccount)]
        [InlineData(WellKnownSidType.BuiltinUsersSid, ExePermissionsHardeningStatus.InvalidAccount)]
        [InlineData(WellKnownSidType.BuiltinAdministratorsSid, ExePermissionsHardeningStatus.Skipped)]
        [InlineData(WellKnownSidType.LocalSystemSid, ExePermissionsHardeningStatus.Skipped)]
        public void RevokeIfUnused_BroadGroupOrAdministrativePrincipal_IsNeverPurged(WellKnownSidType principal, ExePermissionsHardeningStatus expected)
        {
            // Arrange
            var sid = new SecurityIdentifier(principal, null);
            AddAce(TempDirectory, sid);
            var sut = new TestableHardener(TempDirectory) { Sid = sid };

            // Act
            var result = sut.RevokeIfUnused("principal", new List<string>(), CancellationToken.None);

            // Assert
            Assert.Equal(expected, result.Status);
            Assert.True(ItemHasAce(TempDirectory, sid));
        }

        [Fact]
        public void RevokeIfUnused_SameAccountStillUsedUnderAnotherCase_KeepsItsEntries()
        {
            // Arrange
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            var sut = new TestableHardener(TempDirectory);

            // Act
            var result = sut.RevokeIfUnused(@".\svc", new List<string> { @".\SVC" }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InUse, result.Status);
            Assert.Contains(@"'.\SVC'", result.Reason);
            Assert.True(ItemHasAce(logs, LocalServiceSid));
        }

        [Fact]
        public void RevokeIfUnused_SameAccountStillUsedUnderAnotherSpelling_KeepsItsEntries()
        {
            // Arrange: two spellings of one account resolve to the same SID
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            var sut = new TestableHardener(TempDirectory) { Resolver = _ => LocalServiceSid };

            // Act
            var result = sut.RevokeIfUnused(@".\svc", new List<string> { @"HOST\svc" }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.InUse, result.Status);
            Assert.True(ItemHasAce(logs, LocalServiceSid));
        }

        [Fact]
        public void RevokeIfUnused_OnlyOtherAccountsRemain_RemovesEveryEntryOfTheAccountAndKeepsTheirs()
        {
            // Arrange
            AddAce(TempDirectory, LocalServiceSid);
            var db = GrantedFolder(AppConfig.DbFolderName, LocalServiceSid);
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            AddAce(logs, NetworkServiceSid);
            var recovery = GrantedFolder(AppConfig.RecoveryFolderName, LocalServiceSid);
            var exe = Path.Combine(TempDirectory, AppConfig.ServyServiceUIExe);
            File.WriteAllText(exe, "ui");
            AddAce(exe, LocalServiceSid);
            AddAce(exe, NetworkServiceSid);
            var cli = Path.Combine(TempDirectory, AppConfig.ServyServiceCLIExe);
            File.WriteAllText(cli, "cli");
            var sut = new TestableHardener(TempDirectory) { Resolver = a => a == "other" ? NetworkServiceSid : LocalServiceSid };

            // Act
            var result = sut.RevokeIfUnused("svc", new List<string> { "other" }, CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Revoked, result.Status);
            Assert.Equal(new[] { TempDirectory, AppConfig.DbFolderName, AppConfig.LogsFolderName, AppConfig.RecoveryFolderName, AppConfig.ServyServiceUIExe }, result.Revoked);
            Assert.Empty(result.Failed);
            foreach (var item in new[] { TempDirectory, db, logs, recovery, exe })
                Assert.False(ItemHasAce(item, LocalServiceSid), $"{item} still names the account");
            Assert.True(ItemHasAce(logs, NetworkServiceSid));
            Assert.True(ItemHasAce(exe, NetworkServiceSid));
        }

        [Fact]
        public void RevokeIfUnused_NothingWasGranted_RevokesNothingAndSucceeds()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(TempDirectory, AppConfig.LogsFolderName));
            var sut = new TestableHardener(TempDirectory);

            // Act
            var result = sut.RevokeIfUnused("svc", new List<string>(), CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Revoked, result.Status);
            Assert.Empty(result.Revoked);
            Assert.Empty(result.Failed);
        }

        [Fact]
        public void RevokeIfUnused_NotElevated_ChangesNothing()
        {
            // Arrange
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            var sut = new TestableHardener(TempDirectory) { Elevated = false };

            // Act
            var result = sut.RevokeIfUnused("svc", new List<string>(), CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.NotElevated, result.Status);
            Assert.True(ItemHasAce(logs, LocalServiceSid));
        }

        [Fact]
        public void RevokeIfUnused_VaultMissing_ChangesNothing()
        {
            // Arrange
            var sut = new TestableHardener(Path.Combine(TempDirectory, "missing"));

            // Act
            var result = sut.RevokeIfUnused("svc", new List<string>(), CancellationToken.None);

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.VaultNotFound, result.Status);
        }

        [Fact]
        public void RevokeIfUnused_FileWithASecondHardLink_IsNotTouchedAndTheRestIsStillRevoked()
        {
            // Arrange
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            var exe = Path.Combine(TempDirectory, AppConfig.ServyServiceUIExe);
            File.WriteAllText(exe, "ui");
            AddAce(exe, LocalServiceSid);
            var sut = new TestableHardener(TempDirectory) { HardLinkCount = _ => 2 };

            // Act
            var capture = LogCapture.Run(() => sut.RevokeIfUnused("svc", new List<string>(), CancellationToken.None));

            // Assert
            Assert.Equal(ExePermissionsHardeningStatus.Failed, capture.Result.Status);
            Assert.Equal(new[] { AppConfig.ServyServiceUIExe }, capture.Result.Failed);
            Assert.True(ItemHasAce(exe, LocalServiceSid));
            Assert.False(ItemHasAce(logs, LocalServiceSid));
            Assert.Contains("it has 2 NTFS hard links", capture.Log);
        }

        [Fact]
        public void RevokeIfUnused_Cancelled_StopsBeforeTheFolders()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var ex = Record.Exception(() => sut.RevokeIfUnused("svc", new List<string>(), cts.Token));

                // Assert
                Assert.IsAssignableFrom<OperationCanceledException>(ex);
            }
        }

        [Fact]
        public void RevokeIfUnused_CancelledBeforeTheCall_RevokesNoFolder()
        {
            // Arrange
            var db = GrantedFolder(AppConfig.DbFolderName, LocalServiceSid);
            var sut = new TestableHardener(TempDirectory);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var ex = Record.Exception(() => sut.RevokeIfUnused("svc", new List<string>(), cts.Token));

                // Assert: the vault root is revoked before any check, the folder loop's own check stops there
                Assert.IsAssignableFrom<OperationCanceledException>(ex);
                Assert.True(ItemHasAce(db, LocalServiceSid));
            }
        }

        [Fact]
        public void RevokeIfUnused_CancelledDuringTheFirstFile_DoesNotTouchTheNextFile()
        {
            // Arrange
            var ui = Path.Combine(TempDirectory, AppConfig.ServyServiceUIExe);
            File.WriteAllText(ui, "ui");
            AddAce(ui, LocalServiceSid);
            var cli = Path.Combine(TempDirectory, AppConfig.ServyServiceCLIExe);
            File.WriteAllText(cli, "cli");
            AddAce(cli, LocalServiceSid);
            using (var cts = new CancellationTokenSource())
            {
                var sut = new TestableHardener(TempDirectory)
                {
                    HardLinkCount = _ => { cts.Cancel(); return 1; }
                };

                // Act
                var ex = Record.Exception(() => sut.RevokeIfUnused("svc", new List<string>(), cts.Token));

                // Assert: the first file is revoked, the check before the second one stops the loop
                Assert.IsAssignableFrom<OperationCanceledException>(ex);
                Assert.Equal(new[] { AppConfig.ServyServiceUIExe }, sut.HardLinkQueries);
                Assert.True(ItemHasAce(cli, LocalServiceSid));
            }
        }

        [Theory]
        [InlineData(ExePermissionsHardeningStatus.Revoked, true)]
        [InlineData(ExePermissionsHardeningStatus.InUse, true)]
        [InlineData(ExePermissionsHardeningStatus.Skipped, true)]
        [InlineData(ExePermissionsHardeningStatus.Failed, false)]
        [InlineData(ExePermissionsHardeningStatus.NotElevated, false)]
        [InlineData(ExePermissionsHardeningStatus.VaultNotFound, false)]
        [InlineData(ExePermissionsHardeningStatus.InvalidAccount, false)]
        public void ReportRevokeResult_OnlyAnOutcomeThatLeftTheGrantsInPlaceUnwantedIsAFailure(ExePermissionsHardeningStatus status, bool expected)
        {
            // Act
            var reported = ServyExePermissionsHardener.ReportRevokeResult(new ExePermissionsHardeningResult("svc").Complete(status, "reason"));

            // Assert
            Assert.Equal(expected, reported);
        }

        [Fact]
        public async Task RevokeIfUnusedAsync_NullRepository_Throws()
        {
            // Arrange
            var sut = new TestableHardener(TempDirectory);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RevokeIfUnusedAsync("svc", null!, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("")]
        [InlineData("LocalSystem")]
        [InlineData(@"NT AUTHORITY\SYSTEM")]
        public async Task RevokeIfUnusedAsync_LocalSystemOrBlank_ReturnsTrueWithoutReadingTheRepository(string account)
        {
            // Arrange
            var repository = new Mock<IServiceRepository>();
            var sut = new TestableHardener(TempDirectory);

            // Act
            var revoked = await sut.RevokeIfUnusedAsync(account, repository.Object, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(revoked);
            repository.Verify(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal(0, sut.ResolveCalls);
        }

        [Fact]
        public async Task RevokeIfUnusedAsync_ARemainingServiceRunsUnderTheAccount_KeepsItsEntries()
        {
            // Arrange
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
            {
                new ServiceDto { Name = "remaining", RunAsLocalSystem = false, UserAccount = @" .\svc " },
            });
            var sut = new TestableHardener(TempDirectory);

            // Act
            var capture = await LogCapture.RunAsync(() => sut.RevokeIfUnusedAsync(@"  .\svc  ", repository.Object, TestContext.Current.CancellationToken));

            // Assert
            Assert.True(capture.Result);
            Assert.True(ItemHasAce(logs, LocalServiceSid));
            Assert.Contains(@"Kept the access of '.\svc' to Servy's vault", capture.Log);
        }

        [Fact]
        public async Task RevokeIfUnusedAsync_NoRemainingServiceRunsUnderTheAccount_RevokesItsEntries()
        {
            // Arrange
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceDto>
            {
                new ServiceDto { Name = "local-system", RunAsLocalSystem = true },
                new ServiceDto { Name = "stale", RunAsLocalSystem = true, UserAccount = @".\svc" },
            });
            var sut = new TestableHardener(TempDirectory);

            // Act
            var capture = await LogCapture.RunAsync(() => sut.RevokeIfUnusedAsync(@".\svc", repository.Object, TestContext.Current.CancellationToken));

            // Assert
            Assert.True(capture.Result);
            Assert.False(ItemHasAce(logs, LocalServiceSid));
            Assert.Contains(@"Revoked the access of '.\svc' to Servy's vault: ", capture.Log);
        }

        [Fact]
        public async Task RevokeIfUnusedAsync_RepositoryThrows_ReturnsFalseAndKeepsTheEntries()
        {
            // Arrange
            var logs = GrantedFolder(AppConfig.LogsFolderName, LocalServiceSid);
            var repository = new Mock<IServiceRepository>();
            repository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database locked"));
            var sut = new TestableHardener(TempDirectory);

            // Act
            var capture = await LogCapture.RunAsync(() => sut.RevokeIfUnusedAsync("svc", repository.Object, TestContext.Current.CancellationToken));

            // Assert
            Assert.False(capture.Result);
            Assert.True(ItemHasAce(logs, LocalServiceSid));
            Assert.Contains("Revoking the vault access of 'svc' failed.", capture.Log);
        }

        [Fact]
        public async Task RevokeIfUnusedAsync_Cancelled_ReturnsFalseInsteadOfThrowing()
        {
            // Arrange
            var repository = new Mock<IServiceRepository>();
            var sut = new TestableHardener(TempDirectory);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var revoked = await sut.RevokeIfUnusedAsync("svc", repository.Object, cts.Token);

                // Assert
                Assert.False(revoked);
                repository.Verify(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        #endregion

        /// <summary>
        /// Creates a writable folder in the vault carrying the two entries the hardening grants <paramref name="sid"/>.
        /// </summary>
        private string GrantedFolder(string name, SecurityIdentifier sid)
        {
            var path = Path.Combine(TempDirectory, name);
            Directory.CreateDirectory(path);
            var folder = new DirectoryInfo(path);
            var acl = folder.GetAccessControl(AccessControlSections.Access);
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.ReadAndExecute | FileSystemRights.CreateFiles,
                InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Modify,
                InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
            folder.SetAccessControl(acl);
            return path;
        }

        /// <summary>
        /// Adds an explicit Read entry for <paramref name="sid"/> on a file or directory the test owns.
        /// </summary>
        private static void AddAce(string path, SecurityIdentifier sid)
        {
            if (Directory.Exists(path))
            {
                var directory = new DirectoryInfo(path);
                var acl = directory.GetAccessControl(AccessControlSections.Access);
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Read, AccessControlType.Allow));
                directory.SetAccessControl(acl);
            }
            else
            {
                var file = new FileInfo(path);
                var acl = file.GetAccessControl(AccessControlSections.Access);
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Read, AccessControlType.Allow));
                file.SetAccessControl(acl);
            }
        }

        /// <summary>
        /// Whether a file or directory carries an explicit entry for <paramref name="sid"/>.
        /// </summary>
        private static bool ItemHasAce(string path, SecurityIdentifier sid)
        {
            AuthorizationRuleCollection rules;
            if (Directory.Exists(path))
                rules = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access).GetAccessRules(true, false, typeof(SecurityIdentifier));
            else
                rules = new FileInfo(path).GetAccessControl(AccessControlSections.Access).GetAccessRules(true, false, typeof(SecurityIdentifier));

            return rules.Cast<FileSystemAccessRule>().Any(r => sid.Equals(r.IdentityReference));
        }

        /// <summary>
        /// Whether the vault directory carries an explicit entry for <paramref name="sid"/>.
        /// </summary>
        private bool TargetHasVaultAce(SecurityIdentifier sid) => TargetHasAce(TempDirectory, sid);

        /// <summary>
        /// Whether a directory carries an explicit entry for <paramref name="sid"/>.
        /// </summary>
        private static bool TargetHasAce(string directory, SecurityIdentifier sid)
        {
            var acl = new DirectoryInfo(directory).GetAccessControl(AccessControlSections.Access);
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

            public SecurityIdentifier? Sid { get; set; } = LocalServiceSid;

            public Exception? ResolveException { get; set; }

            public bool? IsMember { get; set; } = false;

            public bool RecordOnly { get; set; }

            public Action? OnHarden { get; set; }

            public int ResolveCalls { get; private set; }

            public int MembershipCalls { get; private set; }

            public string? LastResolvedAccount { get; private set; }

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

            /// <summary>
            /// When set, resolves each account on its own instead of every account resolving to <see cref="Sid"/>.
            /// </summary>
            public Func<string, SecurityIdentifier?>? Resolver { get; set; }

            protected override SecurityIdentifier? ResolveAccount(string account)
            {
                ResolveCalls++;
                LastResolvedAccount = account;
                if (ResolveException != null)
                    throw ResolveException;
                return Resolver != null ? Resolver(account) : Sid;
            }

            protected override bool? IsAdministratorsMember(SecurityIdentifier sid)
            {
                MembershipCalls++;
                return IsMember;
            }

            /// <summary>
            /// When set, replaces the NTFS hard link count the hardener reads for a file, so a test can make one
            /// file throw from inside <c>HardenFile</c>'s try block without a real locked file or elevation.
            /// </summary>
            public Func<string, int>? HardLinkCount { get; set; }

            /// <summary>
            /// The file names, in order, whose hard link count was queried. It records how far the file loop got.
            /// </summary>
            public List<string> HardLinkQueries { get; } = new List<string>();

            /// <summary>
            /// Records the query and returns <see cref="HardLinkCount"/>'s answer when one is set, otherwise the
            /// real count.
            /// </summary>
            /// <param name="path">The full path of the file being hardened.</param>
            /// <returns>The number of NTFS hard links to <paramref name="path"/>.</returns>
            protected override int GetHardLinkCount(string path)
            {
                HardLinkQueries.Add(Path.GetFileName(path));
                return HardLinkCount != null ? HardLinkCount(path) : base.GetHardLinkCount(path);
            }
        }
    }
}
