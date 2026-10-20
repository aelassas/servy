using Dapper;
using Servy.Core.Security;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System.Data.Common;
using System.Data.SQLite;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    /// <summary>
    /// Runs the version 12 migration of <see cref="SQLiteDbInitializer"/> against a real SQLite database and the real
    /// <see cref="SecureData"/>: every heartbeat URL stored in clear text is re-saved encrypted (#7579), and nothing else.
    /// </summary>
    [Collection(DatabaseTestCollection.Name)]
    public class HeartbeatUrlEncryptionMigrationIntegrationTests : TempDirectoryTestBase
    {
        private const string PlainUrl = "https://hc-ping.com/0f9d4c1e-token";

        private static DbConnection CreateConnection()
        {
            var conn = new SQLiteConnection("Data Source=:memory:;Version=3;New=True;");
            conn.Open();
            return conn;
        }

        private ProtectedKeyProvider CreateKeyProvider()
            => new ProtectedKeyProvider(Path.Combine(TempDirectory, "aes_key.dat"), Path.Combine(TempDirectory, "aes_iv.dat"));

        private static void AddService(DbConnection conn, string name, string? heartbeatUrl)
            => conn.Execute(
                $"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath, HeartbeatUrl) VALUES (@name, 'C:\\app.exe', @heartbeatUrl);",
                new { name, heartbeatUrl });

        private static string? ReadHeartbeatUrl(DbConnection conn, string name)
            => conn.QuerySingle<string?>($"SELECT HeartbeatUrl FROM {SqlConstants.ServicesTableName} WHERE Name = @name;", new { name });

        private static int ReadVersion(DbConnection conn)
            => conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");

        /// <summary>
        /// Takes a database at the latest version back to version 11, before heartbeat URLs were encrypted, so the next
        /// initialization runs the version 12 migration over the rows it holds.
        /// </summary>
        private static void DowngradeToVersion11(DbConnection conn)
            => conn.Execute("UPDATE SchemaInfo SET Version = 11 WHERE Id = 1;");

        /// <summary>
        /// An encryption service that fails the test if it is used at all.
        /// </summary>
        private sealed class UnusableSecureData : ISecureData
        {
            public string Encrypt(string plainText) => throw new InvalidOperationException("Encrypt must not be called.");

            public string Decrypt(string cipherText) => throw new InvalidOperationException("Decrypt must not be called.");

            public void Dispose()
            {
                /* no-op */
            }
        }

        [Fact]
        public void Initialize_Version11Database_EncryptsEveryHeartbeatUrlStoredInClearText()
        {
            using (var keys = CreateKeyProvider())
            using (var secureData = new SecureData(keys))
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "plain", PlainUrl);
                AddService(conn, "other", "http://localhost:8080/heartbeat");
                DowngradeToVersion11(conn);

                // Act
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null, secureData);

                // Assert
                Assert.Equal(12, SQLiteDbInitializer.LatestSchemaVersion);
                Assert.Equal(12, ReadVersion(conn));

                var stored = ReadHeartbeatUrl(conn, "plain");
                Assert.True(SecureData.HasEncryptionMarker(stored), $"Expected ciphertext, found '{stored}'.");
                Assert.DoesNotContain("hc-ping.com", stored);
                Assert.Equal(PlainUrl, secureData.Decrypt(stored!));

                var other = ReadHeartbeatUrl(conn, "other");
                Assert.True(SecureData.HasEncryptionMarker(other));
                Assert.Equal("http://localhost:8080/heartbeat", secureData.Decrypt(other!));
            }
        }

        [Fact]
        public void Initialize_Version11Database_LeavesEncryptedBlankAndMissingValuesAndOtherColumnsAlone()
        {
            using (var keys = CreateKeyProvider())
            using (var secureData = new SecureData(keys))
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                var alreadyEncrypted = secureData.Encrypt(PlainUrl);
                AddService(conn, "encrypted", alreadyEncrypted);
                AddService(conn, "blank", "   ");
                AddService(conn, "missing", null);
                DowngradeToVersion11(conn);

                // Act
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null, secureData);

                // Assert
                // Not encrypted twice: the stored ciphertext is byte for byte the one written before the migration.
                Assert.Equal(alreadyEncrypted, ReadHeartbeatUrl(conn, "encrypted"));
                // The repository leaves blank values unencrypted, and so does the migration.
                Assert.Equal("   ", ReadHeartbeatUrl(conn, "blank"));
                Assert.Null(ReadHeartbeatUrl(conn, "missing"));
                Assert.Equal(3, conn.QuerySingle<int>($"SELECT COUNT(*) FROM {SqlConstants.ServicesTableName} WHERE ExecutablePath = 'C:\\app.exe';"));
            }
        }

        [Fact]
        public void Initialize_ClearTextHeartbeatUrlWithoutEncryptionService_RollsTheMigrationBack()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "plain", PlainUrl);
                DowngradeToVersion11(conn);

                // Act
                var ex = Assert.Throws<InvalidOperationException>(() => SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null));

                // Assert
                // The version stays at 11 and the value in clear text, so the next start with the keys migrates it.
                Assert.Contains("heartbeat URL", ex.Message);
                Assert.Equal(11, ReadVersion(conn));
                Assert.Equal(PlainUrl, ReadHeartbeatUrl(conn, "plain"));
            }
        }

        [Fact]
        public void Initialize_NothingToEncrypt_NeverUsesTheEncryptionService()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                // The CLI passes a service that reads the keys on first use (#7531), so a start with nothing to
                // encrypt must not touch it.
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "none", null);
                DowngradeToVersion11(conn);

                // Act
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null, new UnusableSecureData());

                // Assert
                Assert.Equal(12, ReadVersion(conn));
            }
        }

        [Fact]
        public void Initialize_AlreadyAtVersion12_DoesNotRunTheMigrationAgain()
        {
            using (var conn = CreateConnection())
            {
                // Arrange
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null);
                AddService(conn, "late", PlainUrl);

                // Act
                SQLiteDbInitializer.Initialize(conn, legacyRecoveryFolderPath: null, new UnusableSecureData());

                // Assert
                // Rows written after the migration go through ServiceRepository, which encrypts them itself.
                Assert.Equal(PlainUrl, ReadHeartbeatUrl(conn, "late"));
            }
        }
    }
}
