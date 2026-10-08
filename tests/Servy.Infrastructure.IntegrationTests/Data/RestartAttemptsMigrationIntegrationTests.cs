using Dapper;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Xunit;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    /// <summary>
    /// Runs the version 10 migration of <see cref="SQLiteDbInitializer"/> against a real SQLite database and a real legacy
    /// recovery folder: the restart attempts columns, the import of the per-service counter files the wrapper wrote in
    /// <c>recovery\</c>, and the deletion of that folder once the import passed.
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
                conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} DROP COLUMN RestartAttempts;");
                conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} DROP COLUMN RestartAttemptsUpdatedAtTicks;");
                conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} DROP COLUMN AllowOverriddenRuntimeVars;");
                conn.Execute("UPDATE SchemaInfo SET Version = 9 WHERE Id = 1;");

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
                WriteCounterFile("RemovedLongAgo", "7", DateTime.UtcNow);

                // Act
                var ex = Record.Exception(() => SQLiteDbInitializer.Initialize(conn, _recoveryFolder));

                // Assert
                Assert.Null(ex);
                Assert.False(Directory.Exists(_recoveryFolder));
            }
        }

        [Fact]
        public void Initialize_CounterAlreadyWrittenThroughTheHost_IsNotOverwrittenByAStaleFile()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: the host already wrote the counter (a folder that survived an earlier, failed deletion)
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Current");
                conn.Execute($"UPDATE {SqlConstants.ServicesTableName} SET RestartAttempts = 1, RestartAttemptsUpdatedAtTicks = 42 WHERE Name = 'Current';");
                WriteCounterFile("Current", "9", DateTime.UtcNow);

                // Act
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert
                Assert.Equal((1, 42L), ReadCounter(conn, "Current"));
                Assert.False(Directory.Exists(_recoveryFolder));
            }
        }

        [Fact]
        public void Initialize_CounterFileLocked_KeepsTheFolderAndImportsItAtTheNextStart()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "Locked");
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

                // Act: the next start
                SQLiteDbInitializer.Initialize(conn, _recoveryFolder);

                // Assert
                Assert.Equal((5, time.Ticks), ReadCounter(conn, "Locked"));
                Assert.False(Directory.Exists(_recoveryFolder));
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
    }
}
