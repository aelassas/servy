using Servy.Core.Helpers;
using Servy.Testing;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class AppFoldersHelperTests : TempDirectoryTestBase
    {
        private const string TempToken = "{tmp}";

        #region EnsureFolders Tests

        [Theory]
        [InlineData(null, "key.aes", "iv.aes", null)]
        [InlineData("Data Source=db.db;", null, "iv.aes", null)]
        [InlineData("Data Source=db.db;", "key.aes", null, null)]
        [InlineData("", "key.aes", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "key.aes", "", null)]
        [InlineData("    ", "key.aes", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "    ", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "key.aes", "    ", null)]
        [InlineData("Data Source=db.db;", "key.aes", "iv.aes", "")]
        [InlineData("Data Source=db.db;", "key.aes", "iv.aes", "    ")]
        public void EnsureFolders_NullOrWhitespaceArgs_Throws(string conn, string key, string iv, string rootVaultPath)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath));
        }

        // The theory above only asserts the exception TYPE, and its "key.aes" is relative, so every row using it
        // throws at the aesKeyFilePath absolute-path guard before the iv/rootVaultPath value is ever inspected.
        // These four give every parameter but the one under test an absolute, non-root path and assert the
        // message, so each guard below is genuinely the first one that can fire.

        [Fact]
        public void EnsureFolders_WhitespaceAesIVFilePath_ThrowsForCorrectReason()
        {
            // Arrange
            var conn = $"Data Source={Path.Combine(TempDirectory, "Servy.db")};";
            var key = Path.Combine(TempDirectory, "key.aes");

            // Act
            var ex = Assert.Throws<ArgumentException>(() =>
                AppFoldersHelper.EnsureFolders(conn, key, "    ", rootVaultPath: TempDirectory));

            // Assert
            Assert.StartsWith("aesIVFilePath cannot be null or whitespace", ex.Message);
        }

        [Fact]
        public void EnsureFolders_WhitespaceRootVaultPath_ThrowsForCorrectReason()
        {
            // Arrange
            var conn = $"Data Source={Path.Combine(TempDirectory, "Servy.db")};";
            var key = Path.Combine(TempDirectory, "key.aes");
            var iv = Path.Combine(TempDirectory, "iv.aes");

            // Act
            var ex = Assert.Throws<ArgumentException>(() =>
                AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: "   "));

            // Assert
            Assert.StartsWith("rootVaultPath cannot be whitespace", ex.Message);
        }

        [Fact]
        public void EnsureFolders_AesKeyFilePathIsDriveRoot_ThrowsCannotDetermineFolder()
        {
            // Arrange
            var conn = $"Data Source={Path.Combine(TempDirectory, "Servy.db")};";
            var key = Path.GetPathRoot(TempDirectory); // absolute, but Path.GetDirectoryName has no parent to return
            var iv = Path.Combine(TempDirectory, "iv.aes");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory));

            // Assert
            Assert.Equal("Cannot determine AES key folder path.", ex.Message);
        }

        [Fact]
        public void EnsureFolders_AesIVFilePathIsDriveRoot_ThrowsCannotDetermineFolder()
        {
            // Arrange
            var conn = $"Data Source={Path.Combine(TempDirectory, "Servy.db")};";
            var key = Path.Combine(TempDirectory, "key.aes");
            var iv = Path.GetPathRoot(TempDirectory);

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory));

            // Assert
            Assert.Equal("Cannot determine AES IV folder path.", ex.Message);
        }

        [Fact]
        public void EnsureFolders_MalformedConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange
            var conn = "==;;";
            var key = Path.Combine(TempDirectory, "key.aes");
            var iv = Path.Combine(TempDirectory, "iv.aes");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory));

            // Assert
            Assert.Equal("Connection string format is invalid.", ex.Message);
            Assert.IsType<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public void EnsureFolders_ConnectionStringMissingDataSource_ThrowsInvalidOperationException()
        {
            // Arrange
            var conn = "Server=myserver;Database=mydb;";
            var key = Path.Combine(TempDirectory, "key.aes");
            var iv = Path.Combine(TempDirectory, "iv.aes");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory));

            // Assert
            Assert.Contains("Data Source", ex.Message);
        }

        [Fact]
        public void EnsureFolders_EmptyDataSourceValue_ThrowsInvalidOperationException()
        {
            // Arrange
            // A quoted blank value is kept by DbConnectionStringBuilder; an unquoted empty
            // one is dropped, which lands on the missing-key guard instead (covered above).
            var conn = "Data Source=\"   \";";
            var key = Path.Combine(TempDirectory, "key.aes");
            var iv = Path.Combine(TempDirectory, "iv.aes");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory));

            // Assert
            Assert.Equal("The database path provided in the connection string is empty.", ex.Message);
        }

        [Fact]
        public void EnsureFolders_DataSourceKeySpelling_Succeeds()
        {
            // Arrange
            var dbFolder = Path.Combine(TempDirectory, "db");
            var keyFolder = Path.Combine(TempDirectory, "keys");
            var ivFolder = Path.Combine(TempDirectory, "iv");

            var conn = $"DataSource={Path.Combine(dbFolder, "Servy.db")};";
            var key = Path.Combine(keyFolder, "key.aes");
            var iv = Path.Combine(ivFolder, "iv.aes");

            // Act
            AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory);

            // Assert
            Assert.True(Directory.Exists(dbFolder));
            Assert.True(Directory.Exists(keyFolder));
            Assert.True(Directory.Exists(ivFolder));
        }

        [Fact]
        public void EnsureFolders_ValidPaths_CreatesAllFoldersUnderCustomRoot()
        {
            // Arrange
            var dbFolder = Path.Combine(TempDirectory, "db");
            var keyFolder = Path.Combine(TempDirectory, "keys");
            var ivFolder = Path.Combine(TempDirectory, "iv");

            var conn = $"Data Source={Path.Combine(dbFolder, "Servy.db")};";
            var key = Path.Combine(keyFolder, "key.aes");
            var iv = Path.Combine(ivFolder, "iv.aes");

            // Act: Supply custom rootVaultPath so tests execute deterministically without touching system C:\ProgramData\Servy
            AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory);

            // Assert: Verify all operational directories exist under the scoped test root
            Assert.True(Directory.Exists(dbFolder));
            Assert.True(Directory.Exists(keyFolder));
            Assert.True(Directory.Exists(ivFolder));
            Assert.True(Directory.Exists(Path.Combine(TempDirectory, "recovery")));
            Assert.True(Directory.Exists(Path.Combine(TempDirectory, "logs")));

            // Assert: Folders nested inside the root vault maintain inheritance
            var dbSecurity = new DirectoryInfo(dbFolder).GetAccessControl();
            Assert.False(dbSecurity.AreAccessRulesProtected); // child of root -> inheritance preserved
        }

        [Fact]
        public void EnsureFolders_ExternalFolder_BreaksInheritance()
        {
            // Arrange: Place root vault and database folder in separate root directories
            var externalTempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var dbFolder = Path.Combine(externalTempDir, "external_db");
                var keyFolder = Path.Combine(TempDirectory, "keys");
                var ivFolder = Path.Combine(TempDirectory, "iv");

                var conn = $"Data Source={Path.Combine(dbFolder, "Servy.db")};";
                var key = Path.Combine(keyFolder, "key.aes");
                var iv = Path.Combine(ivFolder, "iv.aes");

                // Act
                AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: TempDirectory);

                // Assert: External folder must break inheritance as its own security root
                Assert.True(Directory.Exists(dbFolder));
                var dbSecurity = new DirectoryInfo(dbFolder).GetAccessControl();
                Assert.True(dbSecurity.AreAccessRulesProtected); // external folder -> inheritance broken
            }
            finally
            {
                try { if (Directory.Exists(externalTempDir)) Directory.Delete(externalTempDir, true); }
                catch { /* Prevent teardown exceptions */ }
            }
        }

        [Theory]
        [InlineData("Data Source=Servy.db;", "{tmp}\\key.aes", "{tmp}\\iv.aes", "Cannot determine database folder path.")]
        [InlineData("Data Source=:db:;", "{tmp}\\key.aes", "{tmp}\\iv.aes", "Cannot determine database folder path.")]
        public void EnsureFolders_PathWithoutDirectory_ThrowsInvalidOperationException(string conn, string key, string iv, string expectedMessage)
        {
            // Arrange
            string resolvedConn = conn.Replace(TempToken, TempDirectory);
            string resolvedKey = key.Replace(TempToken, TempDirectory);
            string resolvedIv = iv.Replace(TempToken, TempDirectory);

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(resolvedConn, resolvedKey, resolvedIv, rootVaultPath: TempDirectory));

            // Assert
            Assert.Equal(expectedMessage, ex.Message);
        }

        [Theory]
        [InlineData("Data Source={tmp}\\db\\Servy.db;", "key.aes", "{tmp}\\iv\\iv.aes", "aesKeyFilePath must be an absolute path")]
        [InlineData("Data Source={tmp}\\db\\Servy.db;", "{tmp}\\key\\key.aes", "iv.aes", "aesIVFilePath must be an absolute path")]
        [InlineData("Data Source=..\\Servy.db;", "{tmp}\\key.aes", "{tmp}\\iv.aes", "dbFolder must be an absolute path")]
        public void EnsureFolders_PathNotRooted_ThrowsArgumentException(string conn, string key, string iv, string expectedMessage)
        {
            // Arrange
            string resolvedConn = conn.Replace(TempToken, TempDirectory);
            string resolvedKey = key.Replace(TempToken, TempDirectory);
            string resolvedIv = iv.Replace(TempToken, TempDirectory);

            // Act
            var ex = Assert.Throws<ArgumentException>(() =>
                AppFoldersHelper.EnsureFolders(resolvedConn, resolvedKey, resolvedIv, rootVaultPath: TempDirectory));

            // Assert
            Assert.Contains(expectedMessage, ex.Message);
        }

        #endregion

        #region GetAppDirectory Tests

        [Fact]
        public void GetAppDirectory_ReturnsDirectoryContainingProcessOrBaseDir()
        {
            // Arrange: the SUT prefers the process path and falls back to the base directory
            var processPath = Process.GetCurrentProcess().MainModule.FileName;
            var expected = !string.IsNullOrEmpty(processPath)
                ? Path.GetDirectoryName(processPath)
                : AppContext.BaseDirectory;

            // Act
            var appDir = AppFoldersHelper.GetAppDirectory();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(appDir));
            Assert.True(Directory.Exists(appDir));

            // AppContext.BaseDirectory carries a trailing separator and Path.GetDirectoryName does not
            Assert.Equal(
                expected.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                appDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        #endregion
    }
}
