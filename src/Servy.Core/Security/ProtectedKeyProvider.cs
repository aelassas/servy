using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides secure storage and retrieval of an AES encryption key and IV using Windows DPAPI.
    /// Each instance manages its own key and IV file paths.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ProtectedKeyProvider : IProtectedKeyProvider
    {
        private readonly string _keyFilePath;
        private readonly string _ivFilePath;

        private static readonly object FileLock = new object(); // Prevents race conditions

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

        ///<inheritdoc/>
        public byte[] GetKey() => GetOrGenerate(_keyFilePath, 32);

        ///<inheritdoc/>
        public byte[] GetIV() => GetOrGenerate(_ivFilePath, 16);

        /// <summary>
        /// Retrieves the protected bytes from the specified file path, or generates and saves new ones if the file does not exist.
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

            byte[]? encrypted = null;
            try
            {
                encrypted = File.ReadAllBytes(path);
                // DataProtectionScope.LocalMachine allows any process on this computer to unprotect the data.
                return ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            }
            catch (CryptographicException ex)
            {
                // DPAPI is machine-specific; moving the file to another server will trigger this exception.
                throw new InvalidOperationException($"Failed to unprotect key at {path}. The file may have been moved from another machine.", ex);
            }
            finally
            {
                if (encrypted != null) Array.Clear(encrypted, 0, encrypted.Length);
            }
        }

        /// <summary>
        /// Protects the given byte array and writes it to the specified file path.
        /// </summary>
        /// <param name="path">The file path to store the protected data.</param>
        /// <param name="data">The data to protect.</param>
        private void SaveProtected(string path, byte[] data)
        {
            byte[]? encrypted = null;
            try
            {
                // Ensure the target folder exists before writing
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                encrypted = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
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
    }
}
