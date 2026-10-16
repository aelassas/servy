using Dapper;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System.Data.Common;
using System.Data.SQLite;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    /// <summary>
    /// Runs the version 10 migration of <see cref="SQLiteDbInitializer"/> against a real SQLite database and a real legacy
    /// recovery folder: the restart attempts columns, the import of the per-service counter files the wrapper wrote in
    /// <c>recovery\</c>, which runs only with that migration and skips a link or a file with several hard links, and the deletion of that
    /// folder once the import passed.
    /// </summary>
    [Collection(DatabaseTestCollection.Name)]
    public class RestartAttemptsMigrationIntegrationTests : TempDirectoryTestBase
    {
        private readonly string _recoveryFolder;

        public RestartAttemptsMigrationIntegrationTests()
        {
            _recoveryFolder = Path.Combine(TempDirectory, "recovery");
        }

        private static DbConnection CreateConnection()
        {
            var conn = new SQLiteConnection("Data Source=:memory:;Version=3;New=True;");
            conn.Open();
            return conn;
        }

        private static void AddService(DbConnection conn, string name)
            => conn.Execute($"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath) VALUES (@name, 'C:\\app.exe');", new { name });

        private static (int? Attempts, long? Ticks) ReadCounter(DbConnection conn, string name)
            => conn.QuerySingle<(int?, long?)>(
                $"SELECT RestartAttempts, RestartAttemptsUpdatedAtTicks FROM {SqlConstants.ServicesTableName} WHERE Name = @name;", new { name });

        /// <summary>
        /// Takes a database <see cref="SQLiteDbInitializer.Initialize(DbConnection, string)"/> created back to version 9,
        /// before the restart attempts columns, so the next initialization runs the version 10 migration and its import.
        /// </summary>
        /// <param name="conn">The open connection.</param>
        private static void DowngradeToVersion9(DbConnection conn)
        {
            conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} DROP COLUMN RestartAttempts;");
            conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} DROP COLUMN RestartAttemptsUpdatedAtTicks;");
            conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} DROP COLUMN AllowOverriddenRuntimeVars;");
            conn.Execute("UPDATE SchemaInfo SET Version = 9 WHERE Id = 1;");
        }

        /// <summary>Creates a second hard link to an existing file.</summary>
        /// <param name="lpFileName">The new link.</param>
        /// <param name="lpExistingFileName">The existing file.</param>
        /// <param name="lpSecurityAttributes">Reserved; <see cref="IntPtr.Zero"/>.</param>
        /// <returns><see langword="true"/> when the link was created.</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private string WriteCounterFile(string serviceName, string content, DateTime lastWriteUtc)
        {
            Directory.CreateDirectory(_recoveryFolder);
            var path = Path.Combine(_recoveryFolder, LegacyRecoveryImporter.GetCounterFileName(serviceName));
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, lastWriteUtc);
            return path;
        }

        [Fact]
        public void Initialize_FreshDatabase_HasTheRestartAttemptsColumnsAtVersion11()
        {
            using (var conn = CreateConnection())
            {
                // Act
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);

                // Assert
                Assert.Equal(11, SQLiteDbInitializer.LatestSchemaVersion);
                Assert.Equal(11, conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;"));
                var columns = conn.Query($"PRAGMA table_info({SqlConstants.ServicesTableName});").ToDictionary(r => (string)r.name, r => (string)r.type);
                Assert.Equal("INTEGER", columns["RestartAttempts"]);
                Assert.Equal("INTEGER", columns["RestartAttemptsUpdatedAtTicks"]);
            }
        }

        [Fact]
        public void Initialize_Version9Database_AddsTheColumnsAndKeepsTheRows()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: a version 9 database, i.e. without the version 10 and 11 columns
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "existing");
                DowngradeToVersion9(conn);

                // Act
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);

                // Assert
                Assert.Equal(11, conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;"));
                Assert.Equal((null, null), ReadCounter(conn, "existing"));
            }
        }

        [Fact]
        public void Initialize_LegacyRecoveryFolder_ImportsEachCounterWithItsTimeAndDeletesTheFolder()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Alpha");
                AddService(conn, "Beta service.");
                AddService(conn, "Gamma");
                DowngradeToVersion9(conn);
                var alphaTime = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
                var betaTime = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);
                WriteCounterFile("Alpha", "3", alphaTime);
                WriteCounterFile("Beta service.", " 12\r\n", betaTime);

                // Act
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert
                Assert.Equal((3, alphaTime.Ticks), ReadCounter(conn, "Alpha"));
                Assert.Equal((12, betaTime.Ticks), ReadCounter(conn, "Beta service."));
                Assert.Equal((null, null), ReadCounter(conn, "Gamma")); // no file, nothing imported
                Assert.False(Directory.Exists(_recoveryFolder), "the recovery folder is deleted once the import passed");
            }
        }

        [Theory]
        [InlineData("not-a-number")]
        [InlineData("-4")]
        [InlineData("")]
        public void Initialize_CorruptCounterFile_ImportsZeroAsTheWrapperReadIt(string content)
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Corrupt");
                DowngradeToVersion9(conn);
                var time = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
                WriteCounterFile("Corrupt", content, time);

                // Act
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert
                Assert.Equal((0, time.Ticks), ReadCounter(conn, "Corrupt"));
                Assert.False(Directory.Exists(_recoveryFolder));
            }
        }

        [Fact]
        public void Initialize_CounterFileOfAnUninstalledService_IsIgnoredAndTheFolderStillDeleted()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                DowngradeToVersion9(conn);
                WriteCounterFile("RemovedLongAgo", "7", DateTime.UtcNow);

                // Act
                var ex = Record.Exception(() => SQLiteDbInitializer.Initialize(conn, _recoveryFolder));

                // Assert
                Assert.Null(ex);
                Assert.False(Directory.Exists(_recoveryFolder));
            }
        }

        [Fact]
        public void Import_CounterAlreadyWrittenThroughTheHost_IsNotOverwrittenByAStaleFile()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: the host already wrote the counter
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Current");
                conn.Execute($"UPDATE {SqlConstants.ServicesTableName} SET RestartAttempts = 1, RestartAttemptsUpdatedAtTicks = 42 WHERE Name = 'Current';");
                WriteCounterFile("Current", "9", DateTime.UtcNow);

                // Act
                bool result;
                using (var transaction = conn.BeginTransaction())
                {
                    result = LegacyRecoveryImporter.Import(conn, transaction, _recoveryFolder);
                    transaction.Commit();
                }

                // Assert: the stale file is handled without being read, so the folder may go
                Assert.True(result);
                Assert.Equal((1, 42L), ReadCounter(conn, "Current"));
            }
        }

        [Fact]
        public void Initialize_CounterFileLocked_KeepsTheFolderAndDoesNotImportItAtALaterStart()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Locked");
                DowngradeToVersion9(conn);
                var time = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
                var path = WriteCounterFile("Locked", "5", time);

                // Act: the file cannot be read while it is held exclusively
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    SQLiteDbInitializer.Initialize(conn, _recoveryFolder);
                }

                // Assert: nothing imported, nothing deleted, and the migration itself did not fail
                Assert.Equal((null, null), ReadCounter(conn, "Locked"));
                Assert.True(File.Exists(path));
                Assert.Equal(11, conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;"));

                // Act: the next start, with the file readable
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert: the import ran once, with the migration, so the folder is not scanned again
                Assert.Equal((null, null), ReadCounter(conn, "Locked"));
                Assert.True(File.Exists(path));
            }
        }

        [Fact]
        public void Initialize_DatabaseAlreadyMigrated_DoesNotScanTheLegacyRecoveryFolder()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: a database already at the latest version, and a valid counter file whose row was never written
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Migrated");
                var path = WriteCounterFile("Migrated", "4", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

                // Act
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert: the scan is bypassed, so nothing is imported and nothing is deleted
                Assert.Equal((null, null), ReadCounter(conn, "Migrated"));
                Assert.True(File.Exists(path));
            }
        }

        [Fact]
        public void Initialize_CounterFileOwnedByAnotherAccount_IsImportedAndTheFolderDeleted()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: a counter file owned by the current user's own SID, neither BUILTIN\Administrators nor
                //          NT AUTHORITY\SYSTEM, as the wrapper left it when it ran under a service account (#7532)
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "ServiceAccount");
                DowngradeToVersion9(conn);
                var time = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
                var path = WriteCounterFile("ServiceAccount", "8", time);
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var acl = new FileInfo(path).GetAccessControl();
                    acl.SetOwner(identity.User!);
                    new FileInfo(path).SetAccessControl(acl);
                    Assert.Equal(identity.User, new FileInfo(path).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
                }

                // Act
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert: imported whatever its owner, and the folder deleted
                Assert.Equal((8, time.Ticks), ReadCounter(conn, "ServiceAccount"));
                Assert.False(Directory.Exists(_recoveryFolder), "the recovery folder is deleted once the import passed");
            }
        }

        [Fact]
        public void Initialize_CounterFileWithASecondHardLink_IsSkippedAndKept()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: the counter file is also reachable under a second name outside the folder
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Linked");
                DowngradeToVersion9(conn);
                var path = WriteCounterFile("Linked", "6", new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc));
                var secondName = Path.Combine(TempDirectory, "elsewhere.dat");
                Assert.True(CreateHardLink(secondName, path, IntPtr.Zero), $"CreateHardLink failed: {Marshal.GetLastWin32Error()}");

                // Act
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert: neither imported nor deleted, under either name
                Assert.Equal((null, null), ReadCounter(conn, "Linked"));
                Assert.True(File.Exists(path));
                Assert.Equal("6", File.ReadAllText(secondName));
            }
        }

        [Fact]
        public void Initialize_CounterFileIsASymbolicLink_IsSkippedAndTheTargetKept()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: the counter file's name is a link to a file outside the folder
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Symlinked");
                DowngradeToVersion9(conn);
                Directory.CreateDirectory(_recoveryFolder);
                var target = Path.Combine(TempDirectory, "target.dat");
                File.WriteAllText(target, "9");
                var link = Path.Combine(_recoveryFolder, LegacyRecoveryImporter.GetCounterFileName("Symlinked"));

                try
                {
                    File.CreateSymbolicLink(link, target);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Assert.Skip("Symlink creation unavailable on this runner");
                }

                // Act
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert: not imported through the link, and neither the link nor its target is deleted
                Assert.Equal((null, null), ReadCounter(conn, "Symlinked"));
                Assert.True(File.Exists(link));
                Assert.Equal("9", File.ReadAllText(target));
            }
        }

        [Fact]
        public void Initialize_NoRecoveryFolder_ImportsNothing()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "NoFolder");
                DowngradeToVersion9(conn);

                // Act
                var ex = Record.Exception(() => SQLiteDbInitializer.Initialize(conn, _recoveryFolder));

                // Assert
                Assert.Null(ex);
                Assert.Equal((null, null), ReadCounter(conn, "NoFolder"));
                Assert.False(Directory.Exists(_recoveryFolder));
            }
        }

        [Fact]
        public void DeleteFolder_MissingFolder_ReturnsTrue()
        {
            Assert.True(LegacyRecoveryImporter.DeleteFolder(Path.Combine(TempDirectory, "absent")));
        }

        [Fact]
        public void DeleteFolder_FolderIsALink_DeletesTheLinkAndLeavesTheTargetContent()
        {
            // Arrange
            var target = Path.Combine(TempDirectory, "target");
            var link = Path.Combine(TempDirectory, "link");
            Directory.CreateDirectory(target);
            var keep = Path.Combine(target, "keep.dat");
            File.WriteAllText(keep, "7");

            try
            {
                Directory.CreateSymbolicLink(link, target);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Assert.Skip("Symlink creation unavailable on this runner");
            }

            // Act
            var result = LegacyRecoveryImporter.DeleteFolder(link);

            // Assert
            Assert.True(result);
            Assert.False(Directory.Exists(link));
            Assert.True(File.Exists(keep), "the link target's content must survive");
        }

        [Fact]
        public void DeleteFolder_FileInsideIsHeldOpen_ReturnsFalseAndKeepsTheFolder()
        {
            // Arrange
            var folder = Path.Combine(TempDirectory, "recovery-locked");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "held.dat");
            File.WriteAllText(file, "1");

            // Act
            bool result;
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                result = LegacyRecoveryImporter.DeleteFolder(folder);
            }

            // Assert
            Assert.False(result);
            Assert.True(File.Exists(file));
        }
    }
}
