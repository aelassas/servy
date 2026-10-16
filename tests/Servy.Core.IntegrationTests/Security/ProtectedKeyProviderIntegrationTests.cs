using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using Xunit;

namespace Servy.Core.IntegrationTests.Security
{
    /// <summary>
    /// Integration tests for the <see cref="ProtectedKeyProvider"/>.
    /// These tests require a Windows environment due to the reliance on DPAPI (ProtectedData).
    /// </summary>
    public class ProtectedKeyProviderIntegrationTests : TempDirectoryTestBase
    {
        #region Constructor Tests

        [Theory]
        [InlineData(null, "valid_iv_path")]
        [InlineData("", "valid_iv_path")]
        [InlineData("   ", "valid_iv_path")]
        [InlineData("valid_key_path", null)]
        [InlineData("valid_key_path", "")]
        [InlineData("valid_key_path", "   ")]
        public void Constructor_InvalidPaths_ThrowsArgumentException(string keyPath, string ivPath)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ProtectedKeyProvider(keyPath, ivPath));
        }

        [Fact]
        public void Constructor_IdenticalPaths_ThrowsArgumentException()
        {
            // Arrange
            var path = Path.Combine(TempDirectory, "shared.key");

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

        [Fact]
        public void GetIV_ExistingValidFile_UnprotectsSuccessfully()
        {
            // Arrange
            var keyPath = GetTempFilePath("existing_iv.key");
            var ivPath = GetTempFilePath("existing_iv.iv");
            byte[] originalIv;

            // Generation phase
            using (var generatorProvider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                originalIv = generatorProvider.GetIV();
            } // disposed

            // Act - Retrieval phase (simulating a service restart)
            using (var readerProvider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                var retrievedIv = readerProvider.GetIV();

                // Assert
                Assert.Equal(originalIv, retrievedIv);
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

            // 1. Manually create a legacy v7.8 key without machine entropy
            var rawLegacyData = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(rawLegacyData);
            }

            // Encrypted with NULL entropy
            byte[] legacyEncrypted = ProtectedData.Protect(rawLegacyData, null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(keyPath, legacyEncrypted);

            // Capture the exact file bytes prior to migration
            byte[] bytesBeforeMigration = File.ReadAllBytes(keyPath);

            // Act
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                var retrievedKey = provider.GetKey();

                // Assert 1: Must successfully decrypt the legacy data
                Assert.Equal(rawLegacyData, retrievedKey);

                // Assert 2: The file on disk was rewritten
                byte[] bytesAfterMigration = File.ReadAllBytes(keyPath);
                Assert.NotEqual(bytesBeforeMigration, bytesAfterMigration);
            }

            // Assert 3: Verify the migrated file is genuinely entropy-protected
            // Path A: A fresh provider instance can successfully read it (using machine entropy)
            using (var freshProvider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                var roundTripKey = freshProvider.GetKey();
                Assert.Equal(rawLegacyData, roundTripKey);
            }

            // Path B: Raw decryption without entropy MUST fail
            byte[] migratedBytes = File.ReadAllBytes(keyPath);
            Assert.Throws<CryptographicException>(() =>
            {
                ProtectedData.Unprotect(migratedBytes, null, DataProtectionScope.LocalMachine);
            });
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

            // Assert 3: Verify the migrated file is genuinely entropy-protected.
            // The byte comparison above cannot show this on its own - DPAPI output differs on every
            // Protect call, so it would also pass if the migration rewrote the IV without entropy.
            // Path A: A fresh provider instance can successfully read it (using machine entropy)
            using (var freshProvider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                var roundTripIv = freshProvider.GetIV();
                Assert.Equal(rawLegacyIvData, roundTripIv);
            }

            // Path B: Raw decryption without entropy MUST fail
            byte[] migratedBytes = File.ReadAllBytes(ivPath);
            Assert.Throws<CryptographicException>(() =>
            {
                ProtectedData.Unprotect(migratedBytes, null, DataProtectionScope.LocalMachine);
            });
        }

        [Theory]
        [InlineData("key", "Failed to unprotect encryption key")]
        [InlineData("iv", "Failed to unprotect encryption IV")]
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

        [Fact]
        public void GetKey_ProtectedBlobOfTheWrongLength_IsRejectedAndTheFileIsKept()
        {
            // Arrange: a blob DPAPI accepts (legacy, no entropy) that holds 16 bytes instead of the 32-byte key
            var keyPath = GetTempFilePath("short.key");
            var ivPath = GetTempFilePath("short.iv");
            var blob = ProtectedData.Protect(new byte[16], null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(keyPath, blob);

            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var ex = Assert.Throws<InvalidOperationException>(() => provider.GetKey());

                // Assert: never used as a key, never migrated or replaced
                Assert.StartsWith("CRITICAL:", ex.Message);
                Assert.Equal(blob, File.ReadAllBytes(keyPath));
            }
        }

        [Theory]
        [InlineData("key")]
        [InlineData("iv")]
        public void GetMaterial_FileLocked_RetriesAndEventuallyThrows(string targetType)
        {
            // Arrange
            var keyPath = GetTempFilePath($"locked_{targetType}.key");
            var ivPath = GetTempFilePath($"locked_{targetType}.iv");
            var targetPath = targetType == "key" ? keyPath : ivPath;

            // Save some mock dummy payload data to force execution past the file creation stage
            // directly into the ReadAllBytes runtime sequence block.
            File.WriteAllBytes(targetPath, new byte[] { 0x01, 0x02, 0x03, 0x04 });

            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            // Lock the target file exclusively on this thread execution boundary
            using (var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Measure the exact elapsed execution time to verify the backoff retries took place
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Act
                var exception = Assert.ThrowsAny<Exception>(() =>
                    targetType == "key" ? provider.GetKey() : provider.GetIV());

                stopwatch.Stop();

                // Assert
                // 1. Verify the structural type of the exception bubble context matches the filesystem failure path
                var baseException = exception is InvalidOperationException && exception.InnerException != null
                    ? exception.InnerException
                    : exception;

                // The read-retry loop catches and, on the final attempt, rethrows exactly IOException
                // (other than the not-found pair) and UnauthorizedAccessException, so those are the
                // two classes a caller can observe from this path.
                Assert.True(baseException is IOException || baseException is UnauthorizedAccessException,
                    $"Expected filesystem access error, but instead caught: {baseException.GetType().Name}");

                // 2. Verify the backoff retry time logic contract.
                // Both ends of that contract live in AppConfig: every attempt but the last sleeps
                // KeyProviderReadRetryBackoffBaseMs * 2^attempt, and the last one rethrows without
                // sleeping, so the total is the sum over KeyProviderReadMaxRetries - 1 attempts.
                int expectedSleepMs = 0;
                for (int attempt = 0; attempt < AppConfig.KeyProviderReadMaxRetries - 1; attempt++)
                {
                    expectedSleepMs += AppConfig.KeyProviderReadRetryBackoffBaseMs * (1 << attempt);
                }

                // Thread.Sleep(n) never returns early, so the lower bound needs no slack.
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                Assert.True(elapsedMs >= expectedSleepMs,
                    $"The key provider did not retry or back off exponentially. Expected at least {expectedSleepMs}ms of accumulated backoff, but total execution time was only {elapsedMs}ms.");

                // Bound it from above too, so a grown retry count or base cannot pass unnoticed.
                Assert.True(elapsedMs < expectedSleepMs * 3 + TestTimeouts.CiGenerousMs,
                    $"The backoff took {elapsedMs}ms, far beyond the {expectedSleepMs}ms the retry policy allows - the retry count or the backoff base may have grown.");
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
            provider.GetKey();
            provider.GetIV();

            // GetKey/GetIV hand out defensive clones, so hold the internal buffers instead:
            // those are the arrays Dispose zeroes in place before nulling the fields.
            var internalKey = TestReflection.GetField<byte[]>(provider, "_cachedKey");
            var internalIv = TestReflection.GetField<byte[]>(provider, "_cachedIv");

            // Baseline, so an all-zero buffer cannot make the zeroing assertions vacuous
            Assert.Contains(internalKey, b => b != 0);
            Assert.Contains(internalIv, b => b != 0);

            // Act
            provider.Dispose();

            // Assert
            // Verify that subsequent access throws ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(provider.GetKey);
            Assert.Throws<ObjectDisposedException>(provider.GetIV);

            // Verify the buffers were actually zeroed, not merely dropped
            Assert.All(internalKey, b => Assert.Equal(0, b));
            Assert.All(internalIv, b => Assert.Equal(0, b));

            // Verify the backing fields are fully cleared out to null post-disposal
            var cachedKey = TestReflection.GetField<byte[]>(provider, "_cachedKey");
            var cachedIv = TestReflection.GetField<byte[]>(provider, "_cachedIv");

            Assert.Null(cachedKey);
            Assert.Null(cachedIv);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimesSafely()
        {
            // Arrange
            var provider = new ProtectedKeyProvider(GetTempFilePath("k.key"), GetTempFilePath("i.iv"));

            // Populate the cache, so the zeroing branch of Dispose runs on the first call
            // and has to stay safe on the second and third
            provider.GetKey();
            provider.GetIV();

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
            return Path.Combine(TempDirectory, fileName);
        }

        #endregion
    }

    /// <summary>
    /// Integration tests for the cross-process lock failure arms of <see cref="ProtectedKeyProvider"/>.
    /// Each test squats the provider's own global mutex name with a real kernel object, so the arm under
    /// test is reached without any production seam. The arms report through the static logger that
    /// <see cref="LogCapture"/> redirects, which is why these tests join
    /// <see cref="CoreOsIntegrationCollection"/> instead of running in parallel with the rest of the suite.
    /// A unique temporary path per test keeps both the mutex name and the provider's static
    /// migration-failure counter private to that test.
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class ProtectedKeyProviderLockIntegrationTests : TempDirectoryTestBase
    {
        #region Lock Failure Tests

        [Fact]
        public void GetKey_LockNameSquattedByAnotherObjectType_FailsClosedWithoutWritingTheKey()
        {
            // Arrange
            var keyPath = GetTempFilePath("squatted.key");
            var ivPath = GetTempFilePath("squatted.iv");

            // A named event carrying the mutex's own name makes MutexAcl.Create throw
            // WaitHandleCannotBeOpenedException: same name, different kernel object type.
            using (new EventWaitHandle(false, EventResetMode.ManualReset, KeyVaultMutexName(keyPath)))
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var captured = LogCapture.Run(() => Record.Exception(() => provider.GetKey()));

                // Assert
                var wrapped = Assert.IsType<InvalidOperationException>(captured.Result);
                Assert.Contains("Could not acquire the cross-process lock", wrapped.Message);

                var lockFailure = Assert.IsType<System.Security.SecurityException>(wrapped.InnerException);
                Assert.IsType<WaitHandleCannotBeOpenedException>(lockFailure.InnerException);
                Assert.Contains("CRITICAL: Failed to allocate global synchronization mutex", captured.Log);

                // Fail closed: no key may be generated outside the cross-process lock,
                // and no silent fallback to a per-session Local\ namespace may stand in for it.
                Assert.False(File.Exists(keyPath));
            }
        }

        [Fact]
        public void GetKey_LegacyFileWhenMigrationLockFails_ReturnsLegacyDataAndEscalatesOnTheThirdFailure()
        {
            // Arrange
            var keyPath = GetTempFilePath("legacy-locked.key");
            var ivPath = GetTempFilePath("legacy-locked.iv");

            var rawLegacyData = new byte[AppConfig.AesKeySizeBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(rawLegacyData);
            }

            // A v7.8 file: protected with NO entropy, so the primary machine-entropy unprotect fails,
            // the null-entropy fallback succeeds, and the provider re-saves under the mutex - which
            // the squatted name below makes impossible.
            byte[] legacyEncrypted = ProtectedData.Protect(rawLegacyData, null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(keyPath, legacyEncrypted);

            var logs = new List<string>();

            using (new EventWaitHandle(false, EventResetMode.ManualReset, KeyVaultMutexName(keyPath)))
            {
                // Act - one fresh provider per attempt, so the instance cache cannot short-circuit the read
                for (int attempt = 0; attempt < AppConfig.KeyProviderMigrationFailureEscalationThreshold; attempt++)
                {
                    using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
                    {
                        var captured = LogCapture.Run(() => provider.GetKey());

                        // The service stays operational: the legacy data is still returned
                        Assert.Equal(rawLegacyData, captured.Result);
                        logs.Add(captured.Log);
                    }
                }
            }

            // Assert - every attempt below the threshold is a transient warning that names its own attempt number
            for (int i = 0; i < logs.Count - 1; i++)
            {
                Assert.Contains("(Attempt " + (i + 1) + "/" + AppConfig.KeyProviderMigrationFailureEscalationThreshold, logs[i]);
                Assert.DoesNotContain("PERSISTENT SECURITY DEGRADATION", logs[i]);
            }

            // Assert - the attempt that reaches the threshold escalates and names the consecutive-failure count
            Assert.Contains("PERSISTENT SECURITY DEGRADATION", logs[logs.Count - 1]);
            Assert.Contains("Failed " + AppConfig.KeyProviderMigrationFailureEscalationThreshold + " consecutive times", logs[logs.Count - 1]);

            // The migration never reached SaveProtected, so the file on disk is untouched
            Assert.Equal(legacyEncrypted, File.ReadAllBytes(keyPath));
        }

        [Fact]
        public void GetKey_LockAbandonedByPreviousOwner_TakesOwnershipAndGeneratesTheKey()
        {
            // Arrange
            var keyPath = GetTempFilePath("abandoned.key");
            var ivPath = GetTempFilePath("abandoned.iv");

            // The holder keeps the kernel object alive after the owning thread dies, so the provider
            // opens the abandoned mutex instead of creating a fresh one of the same name.
            using (new Mutex(false, KeyVaultMutexName(keyPath)))
            {
                var owner = new Thread(() => AbandonKeyVaultMutex(keyPath));
                owner.Start();
                owner.Join();

                using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
                {
                    // Act
                    var captured = LogCapture.Run(() => provider.GetKey(), LogLevel.Warn);

                    // Assert - the #1808 failure is that this throws instead of generating the key
                    Assert.Equal(AppConfig.AesKeySizeBytes, captured.Result.Length);
                    Assert.True(File.Exists(keyPath));
                    Assert.Contains("was abandoned by a previous owner", captured.Log);
                }
            }
        }

        [Fact]
        public void GetKey_LockAlreadyCreatedByAnotherHolder_LogsThatItJoinedTheExistingMutex()
        {
            // Arrange
            var keyPath = GetTempFilePath("joined.key");
            var ivPath = GetTempFilePath("joined.iv");

            // The mutex already exists and is unowned, so MutexAcl.Create joins it rather than
            // creating it, and the DACL it would have applied is the creating process's.
            using (new Mutex(false, KeyVaultMutexName(keyPath)))
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var captured = LogCapture.Run(() => provider.GetKey(), LogLevel.Debug);

                // Assert
                Assert.Equal(AppConfig.AesKeySizeBytes, captured.Result.Length);
                Assert.Contains("Joined existing cross-process mutex", captured.Log);
            }
        }

        [Fact]
        public void GetKey_AnotherHolderWritesTheKeyWhileWeWaitForTheLock_ReturnsTheirKeyAndKeepsTheirFile()
        {
            // Arrange
            // A real key file for the "winning" process, produced at a side path so this test's lock is not involved
            byte[] winnerKey;
            var winnerKeyPath = GetTempFilePath("winner.key");
            using (var winner = new ProtectedKeyProvider(winnerKeyPath, GetTempFilePath("winner.iv")))
            {
                winnerKey = winner.GetKey();
            }
            byte[] winnerFile = File.ReadAllBytes(winnerKeyPath);

            var keyPath = GetTempFilePath("race.key");
            var ivPath = GetTempFilePath("race.iv");
            byte[] loserKey = null;
            Exception loserError = null;

            using (var holder = new Mutex(false, KeyVaultMutexName(keyPath)))
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                holder.WaitOne();
                var loser = new Thread(() =>
                {
                    try { loserKey = provider.GetKey(); }
                    catch (Exception ex) { loserError = ex; }
                });
                loser.Start();

                // Wait until the provider is blocked on the lock: past the outer File.Exists, before the inner one
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while ((loser.ThreadState & ThreadState.WaitSleepJoin) == 0 && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(10);
                }

                // The winner writes its key while the provider waits, then hands the lock over
                File.Copy(winnerKeyPath, keyPath);
                holder.ReleaseMutex();

                // Act
                Assert.True(loser.Join(TimeSpan.FromSeconds(10)), "GetKey did not return after the lock was released");
            }

            // Assert
            Assert.Null(loserError);
            Assert.Equal(winnerKey, loserKey);
            Assert.Equal(winnerFile, File.ReadAllBytes(keyPath));
        }
        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Mirrors the mutex name <c>ProtectedKeyProvider.RunUnderMutex</c> derives for a path:
        /// an FNV-1a hash of the lower-cased path in the <c>Global\</c> namespace.
        /// </summary>
        /// <param name="path">The key or IV file path the provider locks on.</param>
        /// <returns>The name of the system mutex the provider will open for <paramref name="path"/>.</returns>
        private static string KeyVaultMutexName(string path)
        {
            uint stableHash = 2166136261;
            foreach (char c in path.ToLowerInvariant())
            {
                stableHash = (stableHash ^ c) * 16777619;
            }

            return $@"Global\Servy.ProtectedKeyProvider:{stableHash:X8}";
        }

        /// <summary>
        /// Takes the key vault's mutex and returns without releasing it, so the thread running this
        /// method abandons the mutex when it exits.
        /// </summary>
        /// <param name="keyPath">The key file path whose mutex name is abandoned.</param>
        private static void AbandonKeyVaultMutex(string keyPath)
        {
            // Deliberately neither released nor disposed: a thread that exits while owning the mutex
            // is what makes the next WaitOne on that name throw AbandonedMutexException.
            var mutex = new Mutex(false, KeyVaultMutexName(keyPath));
            mutex.WaitOne();
        }

        /// <summary>
        /// Builds a path inside this test's own temporary directory.
        /// </summary>
        /// <param name="fileName">The file name to place in the temporary directory.</param>
        /// <returns>The absolute path of <paramref name="fileName"/> under the test's temporary directory.</returns>
        private string GetTempFilePath(string fileName)
        {
            return Path.Combine(TempDirectory, fileName);
        }

        #endregion
    }

    /// <summary>
    /// Integration tests for what <see cref="ProtectedKeyProvider"/> reports when it refuses a key file.
    /// The reports go through the static logger that <see cref="LogCapture"/> redirects, which is why these
    /// tests join <see cref="CoreOsIntegrationCollection"/> instead of running in parallel with the rest of the suite.
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class ProtectedKeyProviderRejectionLogIntegrationTests : TempDirectoryTestBase
    {
        #region Rejection Report Tests

        [Fact]
        public void GetKey_CorruptedFile_LogsACriticalErrorAndNeverReplacesTheFile()
        {
            // Arrange
            var keyPath = GetTempFilePath("corrupt_kept.key");
            var ivPath = GetTempFilePath("corrupt_kept.iv");
            var corrupt = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            File.WriteAllBytes(keyPath, corrupt);

            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var captured = LogCapture.Run(() => Record.Exception(() => provider.GetKey()));
                var again = Assert.Throws<InvalidOperationException>(() => provider.GetKey());

                // Assert: reported with what to do, logged with the same text, and never regenerated, on any attempt
                var ex = Assert.IsType<InvalidOperationException>(captured.Result);
                Assert.StartsWith("CRITICAL:", ex.Message);
                Assert.Contains("Do NOT delete or replace aes_key.dat", ex.Message);
                Assert.Contains("CRITICAL: Failed to unprotect encryption key", captured.Log);
                Assert.Contains("Do NOT delete or replace aes_key.dat", captured.Log);
                Assert.StartsWith("CRITICAL:", again.Message);
                Assert.Equal(corrupt, File.ReadAllBytes(keyPath));
            }
        }

        [Fact]
        public void GetKey_EntropyProtectedBlobOfTheWrongLength_ReportsTheLengthWithoutALegacyFallbackWarning()
        {
            // Arrange: a blob protected with the provider's own machine entropy that holds 16 bytes instead of the 32-byte key
            var keyPath = GetTempFilePath("short_entropy.key");
            var ivPath = GetTempFilePath("short_entropy.iv");
            var machineEntropy = TestReflection.GetFieldStatic<Lazy<byte[]>>(typeof(ProtectedKeyProvider), "MachineEntropy").Value;
            var blob = ProtectedData.Protect(new byte[16], machineEntropy, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(keyPath, blob);

            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                // Act
                var captured = LogCapture.Run(() => Record.Exception(() => provider.GetKey()));

                // Assert: refused as CRITICAL for its real reason, the length, and never reported as a legacy v7.8 file
                var ex = Assert.IsType<InvalidOperationException>(captured.Result);
                Assert.StartsWith("CRITICAL:", ex.Message);
                var reason = Assert.IsType<CryptographicException>(ex.InnerException);
                Assert.Contains($"holds 16 bytes instead of {AppConfig.AesKeySizeBytes}", reason.Message);
                Assert.DoesNotContain("SECURITY DEGRADATION WARNING", captured.Log);
                Assert.Equal(blob, File.ReadAllBytes(keyPath));
            }
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Builds a path inside this test's own temporary directory.
        /// </summary>
        /// <param name="fileName">The file name to place in the temporary directory.</param>
        /// <returns>The absolute path of <paramref name="fileName"/> under the test's temporary directory.</returns>
        private string GetTempFilePath(string fileName)
        {
            return Path.Combine(TempDirectory, fileName);
        }

        #endregion
    }

    /// <summary>
    /// Integration tests for the owner check <see cref="ProtectedKeyProvider"/> runs on a key file it loads (#7531):
    /// an elevated run hands a key file another principal owns to <c>BUILTIN\Administrators</c> and re-hardens its DACL,
    /// an unelevated run loads it unchanged. The tests swap the process-global
    /// <see cref="SecurityHelper.IsAdministratorCore"/> seam, which is why they join
    /// <see cref="CoreOsIntegrationCollection"/> instead of running in parallel with the rest of the suite.
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class ProtectedKeyProviderOwnerIntegrationTests : TempDirectoryTestBase
    {
        private const string NotElevatedSkipReason = "Handing a file to BUILTIN\\Administrators requires an elevated process.";

        private static readonly SecurityIdentifier AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier ForeignSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

        #region Owner Check Tests

        [Fact]
        public void GetKey_ElevatedRunAndKeyFileOwnedByAnotherPrincipal_TakesOwnershipAndReHardensTheAcl()
        {
            if (!SecurityHelper.IsAdministrator()) return; // NotElevatedSkipReason

            // Arrange
            var keyPath = Path.Combine(TempDirectory, "foreign_owner.key");
            var ivPath = Path.Combine(TempDirectory, "foreign_owner.iv");
            var expectedKey = CreateKeyOwnedByCurrentUserWithForeignAce(keyPath, ivPath, out var currentUserSid);

            var originalSeam = SecurityHelper.IsAdministratorCore;
            SecurityHelper.IsAdministratorCore = () => true;
            byte[] key;

            try
            {
                // Act
                using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
                {
                    key = provider.GetKey();
                }
            }
            finally
            {
                SecurityHelper.IsAdministratorCore = originalSeam;
            }

            // Assert: the key is still loaded, the file is handed over and the foreign entry is gone
            Assert.Equal(expectedKey, key);
            var acl = new FileInfo(keyPath).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            Assert.Equal(AdministratorsSid, acl.GetOwner(typeof(SecurityIdentifier)));
            Assert.DoesNotContain(ExplicitRules(acl), rule => ForeignSid.Equals(rule.IdentityReference));
        }

        [Fact]
        public void GetKey_UnelevatedRunAndKeyFileOwnedByAnotherPrincipal_LoadsTheKeyAndLeavesTheFileUnchanged()
        {
            // Arrange
            var keyPath = Path.Combine(TempDirectory, "foreign_owner_unelevated.key");
            var ivPath = Path.Combine(TempDirectory, "foreign_owner_unelevated.iv");
            var expectedKey = CreateKeyOwnedByCurrentUserWithForeignAce(keyPath, ivPath, out var currentUserSid);

            var originalSeam = SecurityHelper.IsAdministratorCore;
            SecurityHelper.IsAdministratorCore = () => false;
            byte[] key;

            try
            {
                // Act
                using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
                {
                    key = provider.GetKey();
                }
            }
            finally
            {
                SecurityHelper.IsAdministratorCore = originalSeam;
            }

            // Assert: an unelevated read works as before and changes neither the owner nor the DACL
            Assert.Equal(expectedKey, key);
            var acl = new FileInfo(keyPath).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            Assert.Equal(currentUserSid, acl.GetOwner(typeof(SecurityIdentifier)));
            Assert.Contains(ExplicitRules(acl), rule => ForeignSid.Equals(rule.IdentityReference));
        }

        [Fact]
        public void GetKey_ElevatedRunAndKeyFileIsASymbolicLink_LoadsTheKeyAndLeavesTheTargetUnchanged()
        {
            if (!SecurityHelper.IsAdministrator()) return; // NotElevatedSkipReason

            // Arrange: a real key file owned by the current user with a foreign ACE, and a link to it that the provider is pointed at
            var targetKeyPath = Path.Combine(TempDirectory, "link_target.key");
            var targetIvPath = Path.Combine(TempDirectory, "link_target.iv");
            var expectedKey = CreateKeyOwnedByCurrentUserWithForeignAce(targetKeyPath, targetIvPath, out var currentUserSid);

            var linkKeyPath = Path.Combine(TempDirectory, "link.key");
            var linkIvPath = Path.Combine(TempDirectory, "link.iv");
            CreateFileSymbolicLink(linkKeyPath, targetKeyPath);

            // The link gets the same owner and foreign entry as its target, so the guard is observable on either of them
            var linkInfo = new FileInfo(linkKeyPath);
            Assert.True((linkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint, "the key path is a reparse point");
            var linkArranged = linkInfo.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            linkArranged.SetOwner(currentUserSid);
            linkArranged.AddAccessRule(new FileSystemAccessRule(ForeignSid, FileSystemRights.Read, AccessControlType.Allow));
            linkInfo.SetAccessControl(linkArranged);

            var originalSeam = SecurityHelper.IsAdministratorCore;
            SecurityHelper.IsAdministratorCore = () => true;
            byte[] key;

            try
            {
                // Act
                using (var provider = new ProtectedKeyProvider(linkKeyPath, linkIvPath))
                {
                    key = provider.GetKey();
                }
            }
            finally
            {
                SecurityHelper.IsAdministratorCore = originalSeam;
            }

            // Assert: the key still loads through the link, and neither the link nor the file it points at changes owner or loses its foreign entry
            Assert.Equal(expectedKey, key);
            foreach (var path in new[] { targetKeyPath, linkKeyPath })
            {
                var acl = new FileInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
                Assert.Equal(currentUserSid, acl.GetOwner(typeof(SecurityIdentifier)));
                Assert.Contains(ExplicitRules(acl), rule => ForeignSid.Equals(rule.IdentityReference));
            }
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Generates a key file, then hands it to the current user's own SID, which any token may set as owner, and adds
        /// an explicit entry for a principal that is neither Administrators nor Local System.
        /// </summary>
        /// <param name="keyPath">The key file path.</param>
        /// <param name="ivPath">The IV file path.</param>
        /// <param name="currentUserSid">Receives the SID the file is handed to.</param>
        /// <returns>The key the file holds.</returns>
        private static byte[] CreateKeyOwnedByCurrentUserWithForeignAce(string keyPath, string ivPath, out SecurityIdentifier currentUserSid)
        {
            byte[] key;
            using (var provider = new ProtectedKeyProvider(keyPath, ivPath))
            {
                key = provider.GetKey();
            }

            using (var identity = WindowsIdentity.GetCurrent())
            {
                currentUserSid = identity.User;
            }

            // The arrangement means nothing when the current user is itself one of the administrative principals
            Assert.NotEqual(AdministratorsSid, currentUserSid);
            Assert.NotEqual(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), currentUserSid);

            var file = new FileInfo(keyPath);
            var acl = file.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            acl.SetOwner(currentUserSid);
            acl.AddAccessRule(new FileSystemAccessRule(ForeignSid, FileSystemRights.Read, AccessControlType.Allow));
            file.SetAccessControl(acl);

            var arranged = file.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            Assert.Equal(currentUserSid, arranged.GetOwner(typeof(SecurityIdentifier)));
            Assert.Contains(ExplicitRules(arranged), rule => ForeignSid.Equals(rule.IdentityReference));

            return key;
        }

        /// <summary>
        /// Creates a file symbolic link with the <c>mklink</c> built-in of <c>cmd.exe</c>, because .NET Framework has no
        /// equivalent of <c>File.CreateSymbolicLink</c>. Creating the link needs an elevated process.
        /// </summary>
        /// <param name="linkPath">The path of the link to create.</param>
        /// <param name="targetPath">The file the link points at.</param>
        private static void CreateFileSymbolicLink(string linkPath, string targetPath)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/c mklink \"{linkPath}\" \"{targetPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = System.Diagnostics.Process.Start(psi))
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, $"mklink \"{linkPath}\" \"{targetPath}\" failed: {output}");
            }
        }

        /// <summary>
        /// Lists the explicit entries of a file's DACL.
        /// </summary>
        /// <param name="acl">The file's security descriptor.</param>
        /// <returns>The explicit access rules, with their principals as SIDs.</returns>
        private static List<FileSystemAccessRule> ExplicitRules(FileSecurity acl)
            => acl.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();

        #endregion
    }
}
