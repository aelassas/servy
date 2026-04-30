using Microsoft.Win32;
using Servy.Core.Config;
using Servy.Core.Logging;
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
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides secure storage and retrieval of an AES encryption key and IV using Windows DPAPI.
    /// Each instance manages its own key and IV file paths.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ProtectedKeyProvider : IProtectedKeyProvider
    {
        #region Security & Synchronization Settings

        /// <summary>
        /// The protection scope used for DPAPI operations. 
        /// <see cref="DataProtectionScope.LocalMachine"/> is used to allow the service to access 
        /// the keys regardless of the specific user account context (e.g., SYSTEM vs. Service Account).
        /// </summary>
        private static readonly DataProtectionScope DataProtectionScope = DataProtectionScope.LocalMachine;

        /// <summary>
        /// Synchronization object used to prevent race conditions during file I/O operations,
        /// ensuring thread-safe generation and storage of key material.
        /// </summary>
        private static readonly object FileLock = new object();

        /// <summary>
        /// Caches the machine-unique entropy to optimize performance and ensure thread-safe initialization.
        /// </summary>
        /// <remarks>
        /// This field uses <see cref="Lazy{T}"/> to ensure that the machine-specific entropy is only 
        /// retrieved from the registry once. Caching this value avoids redundant registry I/O operations 
        /// and string-to-byte conversions during subsequent calls to <see cref="GetKey"/> or <see cref="GetIV"/>.
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

        ///<inheritdoc/>
        public byte[] GetKey() => GetOrGenerate(_keyFilePath, 32);

        ///<inheritdoc/>
        public byte[] GetIV() => GetOrGenerate(_ivFilePath, 16);

        #endregion

        #region Private Helpers

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
        /// Retrieves the protected bytes from the specified file path, or generates new ones if the file is missing.
        /// Supports backward compatibility by falling back to no-entropy decryption.
        /// </summary>
        /// <param name="path">The full filesystem path where the protected data is stored.</param>
        /// <param name="length">The number of random bytes to generate if the file is missing.</param>
        /// <returns>
        /// A decrypted byte array containing the raw keying material.
        /// </returns>
        /// <remarks>
        /// This method implements a thread-safe Double-Check Locking pattern to prevent multiple threads from 
        /// generating conflicting keys simultaneously during initial setup.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the data cannot be unprotected. This usually occurs if the file was created 
        /// on a different machine or under a different security context that DPAPI cannot resolve.
        /// </exception>
        private byte[] GetOrGenerate(string path, int length)
        {
            // Double-check locking pattern to ensure only one thread generates the file
            if (!File.Exists(path))
            {
                lock (FileLock)
                {
                    if (!File.Exists(path))
                    {
                        var data = GenerateRandomBytes(length);
                        SaveProtected(path, data);
                        return data;
                    }
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
                    catch (IOException ex)
                    {
                        // If this was the last attempt, rethrow to be caught by BaseCommand
                        if (attempt == maxRetries - 1)
                        {
                            Logger.Error($"Failed to read file after {maxRetries} attempts: {path}", ex);
                            throw;
                        }

                        // Exponential backoff: 100ms, 200ms, 400ms
                        Thread.Sleep(100 * (1 << attempt));
                    }
                }

                // Defensive check: Even though the throw above prevents this, 
                // static analysis tools (and good practice) appreciate the explicit guard.
                if (encrypted is null)
                {
                    throw new FileNotFoundException($"Failed to read {path} after {maxRetries} attempts");
                }

                byte[] dynamicEntropy = MachineEntropy.Value;

                try
                {
                    // 1. Primary Attempt (v7.9+ logic): Use machine-unique entropy
                    var unprotectResult = ProtectedData.Unprotect(encrypted, dynamicEntropy, DataProtectionScope);

                    // Reset failure counter on successful read with modern entropy
                    MigrationFailureCounts.TryRemove(path, out _);

                    return unprotectResult;
                }
                catch (CryptographicException)
                {
                    // 2. Fallback Attempt (v7.8 compatibility): Try with NO entropy (null)
                    byte[] decryptedData = ProtectedData.Unprotect(encrypted, null, DataProtectionScope);

                    try
                    {
                        // 3. Automatic Migration: Re-save with the new machine-unique entropy
                        SaveProtected(path, decryptedData);

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

                            try
                            {
                                using (var eventLog = new EventLog(AppConfig.EventLogName))
                                {
                                    eventLog.Source = AppConfig.EventSource;
                                    eventLog.WriteEntry($"[{AppConfig.EventSource}] {escalatedMsg}\n\nError: {ex.Message}", EventLogEntryType.Error, EventIds.PersistentMigrationFailure);
                                }
                            }
                            catch { /* Silently ignore if direct event log creation fails */ }

                            Logger.Error(escalatedMsg, ex);
                        }
                        else
                        {
                            // Fix for #850: Actually emit the 3002 Warning to the Windows Event Log
                            string warningMsg = $"{baseMsg} (Attempt {failCount}/{MigrationFailureEscalationThreshold}): {ex.Message}";

                            try
                            {
                                using (var eventLog = new EventLog(AppConfig.EventLogName))
                                {
                                    eventLog.Source = AppConfig.EventSource;
                                    // Option (a): Mirror the escalated pattern with a Warning type and ID 3002
                                    eventLog.WriteEntry($"[{AppConfig.EventSource}] {warningMsg}", EventLogEntryType.Warning, EventIds.TransientMigrationWarning);
                                }
                            }
                            catch
                            {
                                // Fallback: If we can't write to the Event Log (e.g. permission issues), 
                                // the file logger is our only hope.
                            }

                            Logger.Warn($"[EventID: {EventIds.TransientMigrationWarning}] {warningMsg}");
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

                // FIX 1 (#712): Direct Event Log Surface
                try
                {
                    using (var eventLog = new EventLog(AppConfig.EventLogName))
                    {
                        eventLog.Source = AppConfig.EventSource;
                        eventLog.WriteEntry($"[{AppConfig.EventSource}] {errorMsg}\n\n{workaround}\n\nException: {ex.Message}", EventLogEntryType.Error, EventIds.KeyUnprotectFailed);
                    }
                }
                catch
                {
                    // Silently ignore if direct event log creation fails (e.g., missing registry permissions), 
                    // the static Logger below will still catch it.
                }

                var msg = $"{errorMsg}\n{workaround}";

                Logger.Error(msg, ex);

                // FIX 4 (#712): Set distinct Service Exit Code (13 = ERROR_INVALID_DATA)
                Environment.ExitCode = 13;

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
                byte[] dynamicEntropy = MachineEntropy.Value;

                // DataProtectionScope is usually LocalMachine for services
                encrypted = ProtectedData.Protect(data, dynamicEntropy, DataProtectionScope);

                var tempPath = path + ".tmp";

                try
                {
                    // Write to a temporary file first
                    File.WriteAllBytes(tempPath, encrypted);

                    // Apply explicit file-level ACLs to the temp file
                    var fs = new FileSecurity();
                    var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                    var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    SecurityIdentifier currentUserSid;
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        currentUserSid = identity.User;
                    }

                    // Match the folder's inheritance logic
                    fs.SetAccessRuleProtection(isProtected: !isChildOfRoot, preserveInheritance: isChildOfRoot);

                    // Explicitly grant Full Control to SYSTEM and Administrators
                    fs.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
                    fs.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, AccessControlType.Allow));

                    // Grant operational continuity to the current user (prevents self-lockout on external paths)
                    if (currentUserSid != null && !currentUserSid.Equals(systemSid) && !currentUserSid.Equals(adminSid))
                    {
                        fs.AddAccessRule(new FileSystemAccessRule(currentUserSid, FileSystemRights.FullControl, AccessControlType.Allow));
                    }

                    new FileInfo(tempPath).SetAccessControl(fs);

                    // Atomically replace the existing file
                    AtomicSecureMove(tempPath, path);
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
        private byte[] GenerateRandomBytes(int length)
        {
            var buffer = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }
            return buffer;
        }

        #endregion
    }
}
