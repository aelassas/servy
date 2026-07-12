using Servy.Core.Security;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Xunit;

namespace Servy.Core.IntegrationTests.Security
{
    /// <summary>
    /// Integration tests for the <see cref="ProtectedKeyProvider"/>.
    /// These tests require a Windows environment due to the reliance on DPAPI (ProtectedData).
    /// </summary>
    public class ProtectedKeyProviderIntegrationTests : IDisposable
    {
        private readonly string _testDirectory;

        public ProtectedKeyProviderIntegrationTests()
        {
            // Create a unique temporary directory for each test run to guarantee isolation
            _testDirectory = Path.Combine(Path.GetTempPath(), "Servy_ProtectedKeyProvider_Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        #region Constructor Tests

        [Theory]
        [InlineData(null, "valid_iv_path")]
        [InlineData("", "valid_iv_path")]
        [InlineData("   ", "valid_iv_path")]
        [InlineData("valid_key_path", null)]
        [InlineData("valid_key_path", "")]
        public void Constructor_InvalidPaths_ThrowsArgumentException(string keyPath, string ivPath)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ProtectedKeyProvider(keyPath, ivPath));
        }

        [Fact]
        public void Constructor_IdenticalPaths_ThrowsArgumentException()
        {
            // Arrange
            var path = Path.Combine(_testDirectory, "shared.key");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new ProtectedKeyProvider(path, path));
            Assert.Contains("different file paths", exception.Message);
        }

        #endregion

        #region Generation and Retrieval Tests

        [Fact]
        public void GetKey_FileDoesNotExist_GeneratesAndSavesKey()
        {
            // Arrange
            var keyPath = GetTempFilePath("master.key");
            var ivPath = GetTempFilePath("master.iv");
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var key = provider.GetKey();

                // Assert
                Assert.NotNull(key);
                Assert.Equal(32, key.Length);
                Assert.True(File.Exists(keyPath));

                // Verify file actually contains DPAPI encrypted data (not plaintext)
                byte[] fileBytes = File.ReadAllBytes(keyPath);
                Assert.NotEqual(key, fileBytes);
            }
        }

        [Fact]
        public void GetIV_FileDoesNotExist_GeneratesAndSavesIV()
        {
            // Arrange
            var keyPath = GetTempFilePath("master.key");
            var ivPath = GetTempFilePath("master.iv");
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var iv = provider.GetIV();

                // Assert
                Assert.NotNull(iv);
                Assert.Equal(16, iv.Length);
                Assert.True(File.Exists(ivPath));

                // Verify the IV file is protected, not plaintext
                byte[] fileBytes = File.ReadAllBytes(ivPath);
                Assert.NotEqual(iv, fileBytes);
            }
        }

        [Fact]
        public void GetKey_SubsequentCalls_ReturnIdenticalDataButDifferentReferences()
        {
            // Arrange
            var keyPath = GetTempFilePath("master.key");
            var ivPath = GetTempFilePath("master.iv");
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var key1 = provider.GetKey();
                var key2 = provider.GetKey();

                // Assert - Values must be identical
                Assert.Equal(key1, key2);

                // Assert - References must be different (cloned from cache to prevent mutation)
                Assert.NotSame(key1, key2);

                // Mutating the returned array should NOT corrupt the internal cache
                key1[0] = (byte)(key1[0] ^ 0xFF);
                var key3 = provider.GetKey();
                Assert.NotEqual(key1, key3);
                Assert.Equal(key2, key3);
            }
        }

        [Fact]
        public void GetIV_SubsequentCalls_ReturnIdenticalDataButDifferentReferences()
        {
            // Arrange
            var keyPath = GetTempFilePath("master.key");
            var ivPath = GetTempFilePath("master.iv");
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var iv1 = provider.GetIV();
                var iv2 = provider.GetIV();

                // Assert - Verify the expected AES initialization vector length constraint
                Assert.Equal(16, iv1.Length);

                // Assert - Values must be identical
                Assert.Equal(iv1, iv2);

                // Assert - References must be different (defensive clone from internal cache field)
                Assert.NotSame(iv1, iv2);

                // Act - Mutate the returned array to test isolation resilience boundaries
                iv1[0] = (byte)(iv1[0] ^ 0xFF);
                var iv3 = provider.GetIV();

                // Assert - Mutating the localized instance should NOT corrupt the internal backing buffer
                Assert.NotEqual(iv1, iv3);
                Assert.Equal(iv2, iv3);
            }
        }

        [Fact]
        public void GetKey_ExistingValidFile_UnprotectsSuccessfully()
        {
            // Arrange
            var keyPath = GetTempFilePath("master.key");
            var ivPath = GetTempFilePath("master.iv");
            byte[] originalKey;

            // Generation phase
            using (var generatorProvider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                originalKey = generatorProvider.GetKey();
            } // disposed

            // Act - Retrieval phase (simulating a service restart)
            using (var readerProvider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                var retrievedKey = readerProvider.GetKey();

                // Assert
                Assert.Equal(originalKey, retrievedKey);
            }
        }

        #endregion

        #region Migration and Resilience Tests

        [Fact]
        public void GetKey_LegacyNoEntropyFile_MigratesToEntropyProtected()
        {
            // Arrange
            var keyPath = GetTempFilePath("legacy.key");
            var ivPath = GetTempFilePath("legacy.iv");

            // 1. Manually create a v7.8 legacy key without machine entropy
            var rawLegacyData = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(rawLegacyData); }
            byte[] legacyEncrypted = ProtectedData.Protect(rawLegacyData, null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(keyPath, legacyEncrypted);

            // Capture the exact file state prior to the migration execution step
            byte[] bytesBeforeMigration = File.ReadAllBytes(keyPath);

            // Act
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                var retrievedKey = provider.GetKey();

                // Assert
                Assert.Equal(rawLegacyData, retrievedKey); // Must successfully decrypt

                byte[] bytesAfterMigration = File.ReadAllBytes(keyPath);
                Assert.NotEqual(bytesBeforeMigration, bytesAfterMigration); // Re-written during migration, no timing dependency
            }
        }

        [Fact]
        public void GetIV_LegacyNoEntropyFile_MigratesToEntropyProtected()
        {
            // Arrange
            var keyPath = GetTempFilePath("legacy_iv_migration.key");
            var ivPath = GetTempFilePath("legacy_iv_migration.iv");

            // 1. Manually create a v7.8 legacy IV (16 bytes) without machine-unique entropy
            var rawLegacyIvData = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(rawLegacyIvData);
            }

            byte[] legacyEncrypted = ProtectedData.Protect(rawLegacyIvData, null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(ivPath, legacyEncrypted);

            // Capture the raw file bytes state prior to executing the migration routing loop
            byte[] bytesBeforeMigration = File.ReadAllBytes(ivPath);

            // Act
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                var retrievedIv = provider.GetIV();

                // Assert - Must successfully fallback to null-entropy and decrypt the original data
                Assert.Equal(rawLegacyIvData, retrievedIv);

                // Assert - Verify that automatic migration occurred by asserting the file payload changed on disk
                byte[] bytesAfterMigration = File.ReadAllBytes(ivPath);
                Assert.NotEqual(bytesBeforeMigration, bytesAfterMigration);
            }
        }

        [Theory]
        [InlineData("key", "Failed to unprotect encryption key")]
        [InlineData("iv", "Failed to unprotect encryption key")]
        public void GetMaterial_CorruptedFile_ThrowsInvalidOperationException(string targetType, string expectedMessageToken)
        {
            // Arrange
            var keyPath = GetTempFilePath("corrupt.key");
            var ivPath = GetTempFilePath("corrupt.iv");
            var targetPath = targetType == "key" ? keyPath : ivPath;

            // Write garbage bytes that DPAPI cannot unprotect
            File.WriteAllBytes(targetPath, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });

            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() =>
                    targetType == "key" ? provider.GetKey() : provider.GetIV());

                Assert.Contains(expectedMessageToken, ex.Message);
            }
        }

        [Theory]
        [InlineData("key")]
        [InlineData("iv")]
        public void GetMaterial_FileLocked_RetriesAndEventuallyThrows(string targetType)
        {
            // Arrange
            var keyPath = GetTempFilePath("locked.key");
            var ivPath = GetTempFilePath("locked.iv");
            var targetPath = targetType == "key" ? keyPath : ivPath;

            File.WriteAllBytes(targetPath, new byte[] { 0x01 }); // Create dummy file

            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            // Lock the target file exclusively on this thread execution boundary
            using (var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act & Assert
                // Will attempt to read 3 times with exponential backoff and fail
                Assert.Throws<IOException>(() =>
                    targetType == "key" ? provider.GetKey() : provider.GetIV());
            }
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_ZeroesInternalState_ThrowsOnSubsequentAccess()
        {
            // Arrange
            var keyPath = GetTempFilePath("master.key");
            var ivPath = GetTempFilePath("master.iv");
            var provider = new ProtectedKeyProvider(keyPath, ivPath);

            // Populate the cache
            _ = provider.GetKey();
            _ = provider.GetIV();

            // Act
            provider.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => provider.GetKey());
            Assert.Throws<ObjectDisposedException>(() => provider.GetIV());
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimesSafely()
        {
            // Arrange
            var provider = new ProtectedKeyProvider(GetTempFilePath("k.key"), GetTempFilePath("i.iv"));

            // Act
            var exception = Record.Exception(() =>
            {
                provider.Dispose();
                provider.Dispose();
                provider.Dispose();
            });

            // Assert
            Assert.Null(exception); // Should not throw on multiple disposes
        }

        #endregion

        #region Test Lifecycle

        private string GetTempFilePath(string fileName)
        {
            return Path.Combine(_testDirectory, fileName);
        }

        public void Dispose()
        {
            // Clean up the temporary test directory
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Swallow cleanup exceptions to prevent failing the test runner 
                    // if a file lock lingers momentarily.
                }
            }
        }

        #endregion
    }
}