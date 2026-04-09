using Microsoft.Win32;
using Servy.Core.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

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
        private static readonly Lazy<byte[]> MachineEntropy = new Lazy<byte[]>(GetMachineEntropy);

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
            // Retrieve the unique MachineGuid from the Windows Registry.
            // This makes the entropy unique to the machine without requiring a hardcoded string.
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                var guid = key?.GetValue("MachineGuid") as string;

                // Fallback to a secondary unique seed if the registry is somehow unreachable.
                // Environment.MachineName is a secondary machine-specific fallback to prevent decryption failure.
                return Encoding.UTF8.GetBytes(guid ?? Environment.MachineName);
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
                            Logger.Error(string.Format("Failed to read file after {0} attempts: {1}", maxRetries, path), ex);
                            throw;
                        }

                        // Exponential backoff: Wait longer with each failure
                        Thread.Sleep(100 * (attempt + 1));
                    }
                }

                // Defensive check: Even though the throw above prevents this, 
                // static analysis tools (and good practice) appreciate the explicit guard.
                if (encrypted == null)
                {
                    throw new FileNotFoundException(string.Format("Configuration file could not be loaded: {0}", path));
                }

                byte[] dynamicEntropy = MachineEntropy.Value;

                try
                {
                    // 1. Primary Attempt (v7.9+ logic): Use machine-unique entropy
                    return ProtectedData.Unprotect(encrypted, dynamicEntropy, DataProtectionScope);
                }
                catch (CryptographicException)
                {
                    // 2. Fallback Attempt (v7.8 compatibility): Try with NO entropy (null)
                    byte[] decryptedData = ProtectedData.Unprotect(encrypted, null, DataProtectionScope);

                    try
                    {
                        // 3. Automatic Migration: Re-save with the new machine-unique entropy
                        SaveProtected(path, decryptedData);
                        return decryptedData;
                    }
                    catch (Exception ex)
                    {
                        // Trip the warning so admins can diagnose I/O or permission issues.
                        // We still return the data so the service remains operational.
                        Logger.Warn($"Key migration to entropy-protected format failed for '{path}': {ex.Message}");
                        return decryptedData;
                    }
                }
            }
            catch (CryptographicException ex)
            {
                // DPAPI is machine-specific; moving the file to another server will trigger this exception.
                Logger.Error("Failed to unprotect encryption key. The key file may have been moved from another machine.", ex);
                throw new InvalidOperationException($"Failed to unprotect encryption key. The file may have been moved from another machine.", ex);
            }
            finally
            {
                if (encrypted != null) Array.Clear(encrypted, 0, encrypted.Length);
            }
        }

        /// <summary>
        /// Protects the given byte array using LocalMachine scope and additional entropy.
        /// </summary>
        /// <param name="path">The file path to store the protected data.</param>
        /// <param name="data">The data to protect.</param>
        private void SaveProtected(string path, byte[] data)
        {
            byte[] encrypted = null;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    SecurityHelper.CreateSecureDirectory(directory);
                }

                // Encrypt data with DPAPI using the machine-specific key and additional entropy.
                byte[] dynamicEntropy = MachineEntropy.Value;
                encrypted = ProtectedData.Protect(data, dynamicEntropy, DataProtectionScope);
                File.WriteAllBytes(path, encrypted);
            }
            finally
            {
                if (encrypted != null) Array.Clear(encrypted, 0, encrypted.Length);
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
