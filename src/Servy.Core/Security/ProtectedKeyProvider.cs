using Microsoft.Win32;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides secure storage and retrieval of an AES encryption key and IV using Windows DPAPI.
    /// Each instance manages its own key and IV file paths, utilizing in-memory caching to optimize
    /// performance and minimize DPAPI/Disk I/O roundtrips.
    /// </summary>
    public class ProtectedKeyProvider : SecureDisposable, IProtectedKeyProvider
    {
        #region Security & Synchronization Settings

        /// <summary>
        /// The protection scope used for DPAPI operations. 
        /// <see cref="DataProtectionScope.LocalMachine"/> is used to allow the service to access 
        /// the keys regardless of the specific user account context (e.g., SYSTEM vs. Service Account).
        /// </summary>
        private static readonly DataProtectionScope ProtectionScope = DataProtectionScope.LocalMachine;

        /// <summary>
        /// Caches the machine-unique entropy to optimize performance and ensure thread-safe initialization.
        /// </summary>
        /// <remarks>
        /// The factory may run more than once under concurrent first access and only the published value is 
        /// shared. Caching this value avoids redundant registry I/O operations and string-to-byte conversions 
        /// during subsequent calls to <see cref="GetKey"/> or <see cref="GetIV"/>.
        /// </remarks>
        private static readonly Lazy<byte[]> MachineEntropy = new Lazy<byte[]>(GetMachineEntropy, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Tracks consecutive migration failures per file path to escalate visibility on persistent issues.
        /// </summary>
        private static readonly ConcurrentDictionary<string, int> MigrationFailureCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The number of consecutive migration failures before escalating to a system-level Error/EventLog entry.
        /// </summary>
        private const int MigrationFailureEscalationThreshold = 3;

        #endregion

        #region Private Fields

        private readonly string _keyFilePath;
        private readonly string _ivFilePath;

        // In-memory cache for unprotected materials
        private byte[] _cachedKey;
        private byte[] _cachedIv;
        private readonly object _cacheLock = new object();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectedKeyProvider"/> class.
        /// </summary>
        /// <param name="keyFilePath">The file path to store the protected AES key.</param>
        /// <param name="ivFilePath">The file path to store the protected AES IV.</param>
        /// <exception cref="ArgumentException">Thrown if paths are invalid or identical.</exception>
        public ProtectedKeyProvider(string keyFilePath, string ivFilePath)
        {
            if (string.IsNullOrWhiteSpace(keyFilePath))
                throw new ArgumentException("Key file path cannot be null or empty", nameof(keyFilePath));
            if (string.IsNullOrWhiteSpace(ivFilePath))
                throw new ArgumentException("IV file path cannot be null or empty", nameof(ivFilePath));

            _keyFilePath = Path.GetFullPath(keyFilePath);
            _ivFilePath = Path.GetFullPath(ivFilePath);

            if (string.Equals(_keyFilePath, _ivFilePath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Key and IV must use different file paths");
        }

        #endregion

        #region IProtectedKeyProvider Implementation

        /// <inheritdoc/>
        public byte[] GetKey()
        {
            ThrowIfDisposed();
            return GetCachedOrGenerate(ref _cachedKey, _keyFilePath, 32);
        }

        /// <inheritdoc/>
        public byte[] GetIV()
        {
            ThrowIfDisposed();
            return GetCachedOrGenerate(ref _cachedIv, _ivFilePath, 16);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Safely retrieves the key from the cache or generates it, ensuring thread-safety
        /// and preventing callers from mutating the internal cache via array cloning.
        /// </summary>
        /// <param name="cacheField">A reference to the backing field (e.g., _cachedKey or _cachedIv).</param>
        /// <param name="path">The filesystem path to the protected file.</param>
        /// <param name="length">The expected length of the key material.</param>
        /// <returns>A clone of the decrypted key material.</returns>
        private byte[] GetCachedOrGenerate(ref byte[] cacheField, string path, int length)
        {
            lock (_cacheLock)
            {
                // 1. FAST-PATH: Synchronized Read
                // We must hold the lock even for the fast-path read to prevent a TOCTOU race 
                // condition with InvalidateCache, which explicitly calls CryptographicOperations.ZeroMemory 
                // on the backing array. If we read outside the lock, we risk cloning a zeroed array.
                if (cacheField != null)
                {
                    return (byte[])cacheField.Clone();
                }

                // 2. GENERATE: Materialize and cache the keys.
                // GetOrGenerate handles its own internal migration/rotation logic.
                byte[] decrypted = GetOrGenerate(path, length);

                // Capture ownership of the freshly generated key material directly without 
                // executing an extra intermediate cloning pass. This prevents un-zeroed plaintext 
                // leftovers from floating on the managed heap before garbage collection.
                cacheField = decrypted;

                // CODE PARITY: Return a clone directly from the initialized cache field.
                // This enforces literal parity with the XML documentation contract and guarantees 
                // both paths exhibit identical, symmetric array allocation behavior.
                return (byte[])cacheField.Clone();
            }
        }

        /// <summary>
        /// Explicitly invalidates the in-memory cache and zeroes out the backing arrays.
        /// </summary>
        private void InvalidateCache()
        {
            lock (_cacheLock)
            {
                if (_cachedKey != null)
                {
                    Array.Clear(_cachedKey, 0, _cachedKey.Length);
                    _cachedKey = null;
                }
                if (_cachedIv != null)
                {
                    Array.Clear(_cachedIv, 0, _cachedIv.Length);
                    _cachedIv = null;
                }
            }
        }

        /// <summary>
        /// Retrieves a machine-unique entropy value for use as an additional protection layer in DPAPI operations.
        /// </summary>
        /// <returns>
        /// A byte array derived from the Windows <c>MachineGuid</c> or the local <see cref="Environment.MachineName"/> as a fallback.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method implements a dynamic entropy strategy to mitigate the risk of hardcoded secrets in the binary.
        /// By utilizing the <c>MachineGuid</c> located at <c>HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography</c>, 
        /// the resulting entropy is unique to the specific OS installation.
        /// </para>
        /// <para>
        /// This ensures that even if an attacker gains access to the encrypted data and the application source code, 
        /// they cannot decrypt the material on a different machine, effectively making the protected files non-portable.
        /// </para>
        /// </remarks>
        [ExcludeFromCodeCoverage]
        private static byte[] GetMachineEntropy()
        {
            // ROBUSTNESS: Force the 64-bit registry view to prevent WoW64 redirection.
            // On 64-bit Windows, the Cryptography key is not present in the WoW6432Node,
            // which previously caused 32-bit callers to silently fall back to MachineName.
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                var guid = key?.GetValue("MachineGuid") as string;

                if (!string.IsNullOrWhiteSpace(guid))
                {
                    return Encoding.UTF8.GetBytes(guid);
                }

                // Log a loud error when the fallback is triggered.
                // MachineName is highly predictable, making this a significant degradation of the entropy layer.
                Logger.Error("CRITICAL SECURITY DEGRADATION: 'MachineGuid' registry key is missing or inaccessible. " +
                             "Falling back to predictable Environment.MachineName for DPAPI entropy. " +
                             "This reduces protection against offline attacks.");

                return Encoding.UTF8.GetBytes(Environment.MachineName);
            }
        }

        /// <summary>
        /// Safely executes an action delegate within a cross-process system mutex mapped to a specific filesystem path.
        /// Ensures synchronization between Session 0 System processes and Interactive user instances.
        /// </summary>
        private void RunUnderMutex(string path, Action action)
        {
            // Compute a deterministic FNV-1a stable hash of the path to avoid process-randomized string.GetHashCode() anomalies
            uint stableHash = 2166136261;
            foreach (char c in path.ToLowerInvariant())
            {
                stableHash = (stableHash ^ c) * 16777619;
            }
            var mutexName = $@"Global\Servy.ProtectedKeyProvider:{stableHash:X8}";

            Mutex mutex;
            try
            {
                // Configure explicit DACL rules to permit cross-process and cross-session synchronization.
                // This ensures both the Session 0 SYSTEM service and the interactive user Manager share the exact same lock.
                var mutexSecurity = new MutexSecurity();

                // Allow everyone (WorldSid) to Synchronize and Modify (Release) the shared Mutex
                var accessRule = new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    MutexRights.Synchronize | MutexRights.Modify,
                    AccessControlType.Allow);

                mutexSecurity.AddAccessRule(accessRule);

                // Use MutexAcl to atomically instantiate the named system mutex with security descriptors applied
                mutex = MutexAcl.Create(initiallyOwned: false, mutexName, out _, mutexSecurity);
            }
            catch (Exception ex)
            {
                // Fail fast: We must not silently fall back to a per-session Local\ namespace, 
                // as doing so bypasses cross-session synchronization and risks key corruption.
                Logger.Error($"CRITICAL: Failed to allocate global synchronization mutex '{mutexName}'. Cross-process execution aborted.", ex);
                throw new System.Security.SecurityException($"Could not establish cross-session lock boundary: {ex.Message}", ex);
            }

            using (mutex)
            {
                bool owned = false;
                try
                {
                    try
                    {
                        owned = mutex.WaitOne(TimeSpan.FromSeconds(AppConfig.KeyProviderMutexTimeoutSeconds));
                    }
                    catch (AbandonedMutexException)
                    {
                        // Prior owner died while holding the mutex. We still own it; just log and proceed.
                        owned = true;
                        Logger.Warn($"Mutex '{mutexName}' was abandoned by a previous owner. Continuing with ownership.");
                    }

                    if (!owned)
                    {
                        throw new TimeoutException($"Timed out waiting for cross-process lock on {path}");
                    }

                    action();
                }
                finally
                {
                    if (owned) mutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Retrieves the protected bytes from the specified file path, or generates new ones if the file is missing.
        /// Supports backward compatibility by falling back to no-entropy decryption.
        /// </summary>
        /// <param name="path">The full filesystem path where the protected data is stored.</param>
        /// <param name="length">The number of random bytes to generate if the file is missing.</param>
        /// <returns>
        /// A decrypted byte array containing the raw keying material.
        /// </returns>
        /// <remarks>
        /// This method implements a process-safe Double-Check Locking pattern using a global system mutex 
        /// to prevent multiple concurrent Servy executables from generating conflicting cryptographic keys.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the data cannot be unprotected. This usually occurs if the file was created 
        /// on a different machine or under a different security context that DPAPI cannot resolve.
        /// </exception>
        private byte[] GetOrGenerate(string path, int length)
        {
            // Double-check locking pattern to ensure only one process or thread generates the file
            if (!File.Exists(path))
            {
                byte[] generatedData = null;
                RunUnderMutex(path, () =>
                {
                    if (!File.Exists(path))
                    {
                        generatedData = GenerateRandomBytes(length);
                        SaveProtected(path, generatedData);
                    }
                });

                if (generatedData != null)
                {
                    return generatedData;
                }
            }

            byte[] encrypted = null;
            try
            {
                // Retry with short exponential backoff
                const int maxRetries = 3;

                // Move the safety check to the loop condition
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        encrypted = File.ReadAllBytes(path);
                        break; // Success: exit the loop
                    }
                    catch (Exception ex) when (
                        (ex is IOException io && !(io is FileNotFoundException) && !(io is DirectoryNotFoundException))
                        || ex is UnauthorizedAccessException)
                    {
                        // If this was the last attempt, rethrow to be caught by BaseCommand
                        if (attempt == maxRetries - 1)
                        {
                            var verb = ex is UnauthorizedAccessException ? "Access denied" : "Failed to read file";
                            Logger.Error($"{verb} after {maxRetries} attempts: {path}", ex);
                            throw;
                        }

                        // Exponential backoff
                        Thread.Sleep(AppConfig.KeyProviderReadRetryBackoffBaseMs * (1 << attempt));
                    }
                }

                // Defensive check: Even though the throw above prevents this, 
                // static analysis tools (and good practice) appreciate the explicit guard.
                if (encrypted is null)
                {
                    throw new InvalidOperationException($"Failed to read {path} after {maxRetries} attempts");
                }

                byte[] dynamicEntropy = MachineEntropy.Value;

                try
                {
                    // 1. Primary Attempt (v7.9+ logic): Use machine-unique entropy
                    var unprotectResult = ProtectedData.Unprotect(encrypted, dynamicEntropy, ProtectionScope);

                    // Reset failure counter on successful read with modern entropy
                    MigrationFailureCounts.TryRemove(path, out _);

                    return unprotectResult;
                }
                catch (CryptographicException)
                {
                    // 2. Fallback Attempt (v7.8 compatibility): Try with NO entropy (null)
                    byte[] decryptedData = ProtectedData.Unprotect(encrypted, null, ProtectionScope);

                    try
                    {
                        // 3. Automatic Migration: Re-save with the new machine-unique entropy.
                        // ROBUSTNESS: Ensure the upgrade save executes within the exact same global 
                        // named mutex namespace as the generation loop. This prevents staging file (.tmp)
                        // naming collisions when multiple services attempt to migrate the same legacy file concurrently.
                        RunUnderMutex(path, () =>
                        {
                            SaveProtected(path, decryptedData);
                        });

                        // Success: Clear any previous failure noise
                        MigrationFailureCounts.TryRemove(path, out _);

                        return decryptedData;
                    }
                    catch (Exception ex)
                    {
                        // 4. Escalation: Track repeated failures to prevent invisible degraded states
                        int failCount = MigrationFailureCounts.AddOrUpdate(path, 1, (_, count) => count + 1);
                        string baseMsg = $"Key migration to entropy-protected format failed for '{path}'";

                        if (failCount >= MigrationFailureEscalationThreshold)
                        {
                            string escalatedMsg = $"[EventID: {EventIds.PersistentMigrationFailure}] PERSISTENT SECURITY DEGRADATION: {baseMsg}. Failed {failCount} consecutive times. The file cannot be upgraded to modern encryption. System remains in v7.8 compatibility mode.";
                            TryWriteServyEventLog($"{escalatedMsg}\n\nError: {ex.Message}", EventLogEntryType.Error, EventIds.PersistentMigrationFailure);
                            Logger.Error(escalatedMsg, ex);
                        }
                        else
                        {
                            string warningMsg = $"[EventID: {EventIds.TransientMigrationWarning}] {baseMsg} (Attempt {failCount}/{MigrationFailureEscalationThreshold}): {ex.Message}";
                            TryWriteServyEventLog(warningMsg, EventLogEntryType.Warning, EventIds.TransientMigrationWarning);
                            Logger.Warn(warningMsg);
                        }

                        // We still return the data so the service remains operational.
                        return decryptedData;
                    }
                }
            }
            catch (CryptographicException ex)
            {
                // DPAPI is machine-specific; moving the file to another server will trigger this exception.
                string errorMsg = $"Failed to unprotect encryption key at '{path}'. The file may have been moved from another machine or restored from an image.";

                string workaround = "Workaround:\n" +
                                    "1. (If possible) Export service configurations to XML or JSON on the original machine.\n" +
                                    "2. On the new machine, backup and delete the following folders:\n" +
                                    "   - %ProgramData%\\Servy\\security\n" +
                                    "   - %ProgramData%\\Servy\\db\n" +
                                    "3. Import the services via the CLI, PowerShell, or Manager.\n" +
                                    "4. You will need to re-enter usernames and passwords if your services run under specific accounts, as those secrets are not exported for security reasons.";

                // Direct Event Log Surface
                TryWriteServyEventLog($"{errorMsg}\n\n{workaround}\n\nError: {ex.Message}", EventLogEntryType.Error, EventIds.KeyUnprotectFailed);

                var msg = $"{errorMsg}\n{workaround}";

                Logger.Error(msg, ex);

                throw new InvalidOperationException(msg, ex);
            }
            finally
            {
                if (encrypted != null) Array.Clear(encrypted, 0, encrypted.Length);
            }
        }

        /// <summary>
        /// Protects the given byte array using DPAPI and stores it securely on disk using an atomic write with explicit ACLs.
        /// </summary>
        /// <param name="path">The full file path to store the protected data.</param>
        /// <param name="data">The plaintext data to protect.</param>
        /// <remarks>
        /// <para>
        /// <b>Architectural Warning:</b> This method must be executed within the boundaries of the <c>Global\</c> 
        /// named mutex established by <see cref="RunUnderMutex"/>. 
        /// </para>
        /// </remarks>
        [ExcludeFromCodeCoverage]
        private void SaveProtected(string path, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            byte[] encrypted = null;
            try
            {
                var directory = Path.GetDirectoryName(path);

                // CHECK: Is this a child of the root vault?
                // This prevents the "reset ACL" bug for subfolders like /db or /security
                string root = AppConfig.ProgramDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                bool isChildOfRoot = directory != null && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(directory))
                {
                    SecurityHelper.CreateSecureDirectory(directory, breakInheritance: !isChildOfRoot);
                }

                // Encrypt data with DPAPI using the machine-specific key and additional entropy.
                // DataProtectionScope is usually LocalMachine for services
                byte[] dynamicEntropy = MachineEntropy.Value;
                encrypted = ProtectedData.Protect(data, dynamicEntropy, ProtectionScope);

                // Use a stable, deterministic staging file name. Because this runs under the global system
                // mutex (RunUnderMutex), concurrent writers cannot clash, and an orphaned .tmp left by a prior
                // crash is cleanly overwritten and self-healed rather than accumulating.
                var tempPath = $"{path}.staging.tmp";

                try
                {
                    // Write to a temporary file first (will naturally overwrite any stale orphan from a prior hard crash)
                    File.WriteAllBytes(tempPath, encrypted);

                    // Apply explicit file-level ACLs to the temp file
                    var fs = new FileSecurity();
                    SecurityIdentifier currentUserSid;
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        currentUserSid = identity.User;
                    }

                    // Thread the canonical policy parameters through the central SecurityHelper core
                    SecurityHelper.ApplySecurityRules(fs, currentUserSid, breakInheritance: !isChildOfRoot);

                    new FileInfo(tempPath).SetAccessControl(fs);

                    // Atomically replace the existing file
                    NativeMethodsHelpers.AtomicSecureMove(tempPath, path);

                    // Explicit invalidation on successful key rotation
                    InvalidateCache();
                }
                finally
                {
                    // If the move succeeded, tempPath no longer exists here.
                    // If an exception was thrown before the move, this cleans up the orphaned file.
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
                    }
                }
            }
            finally
            {
                // Deterministically wipe the sensitive encrypted buffer from RAM
                if (encrypted != null)
                {
                    Array.Clear(encrypted, 0, encrypted.Length);
                }
            }
        }

        /// <summary>
        /// Generates a random byte array of the specified length.
        /// </summary>
        /// <param name="length">The length of the byte array.</param>
        /// <returns>A random byte array.</returns>
        private static byte[] GenerateRandomBytes(int length)
        {
            var buffer = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }
            return buffer;
        }

        /// <summary>
        /// Attempts to record a diagnostic entry in the Windows Event Log using the centralized Servy configuration.
        /// </summary>
        /// <param name="formattedMessage">The fully prepared log message text.</param>
        /// <param name="type">The Windows <see cref="EventLogEntryType"/> (e.g., Error, Warning, or Information).</param>
        /// <param name="eventId">The specific numerical identifier for the event, typically derived from the Core taxonomy.</param>
        /// <remarks>
        /// This method encapsulates the interaction with the Windows Event Log subsystem. It is designed to be 
        /// "fail-silent" regarding the caller; if the Event Log service is unavailable, permissions are restricted, 
        /// or the source is not registered, the exception is caught, logged to the primary file-based 
        /// <see cref="Logger"/> at a Debug level, and execution continues.
        /// </remarks>
        private static void TryWriteServyEventLog(string formattedMessage, EventLogEntryType type, int eventId)
        {
            try
            {
                // Delegate Event Log writes directly through the centralized infrastructure helper core
                // to automatically inherit message truncation constraints and explicit prefix processing policies.
                string structuredMessage = $"[{AppConfig.EventSource}] {formattedMessage}";
                EventLogLogger.WriteRawToWindowsEventLog(AppConfig.EventLogName, AppConfig.EventSource, structuredMessage, type, eventId);
            }
            catch (Exception eventLogEx)
            {
                // Fail-over: Log the failure to the primary logger so the original diagnostic isn't lost.
                Logger.Debug($"EventLog write failed (falling back to file logger): {eventLogEx.GetType().Name} - {eventLogEx.Message}");
            }
        }

        #endregion

        #region SecureDisposable Overrides

        /// <inheritdoc />
        protected override void ZeroSensitiveData()
        {
            InvalidateCache();
        }

        #endregion
    }
}