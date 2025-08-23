using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectedKeyProvider"/> class.
        /// </summary>
        /// <param name="keyFilePath">The file path to store the protected AES key.</param>
        /// <param name="ivFilePath">The file path to store the protected AES IV.</param>
        public ProtectedKeyProvider(string keyFilePath, string ivFilePath)
        {
            _keyFilePath = keyFilePath ?? throw new ArgumentNullException(nameof(keyFilePath));
            _ivFilePath = ivFilePath ?? throw new ArgumentNullException(nameof(ivFilePath));
        }

        ///<inheritdoc/>
        public byte[] GetKey()
        {
            if (File.Exists(_keyFilePath))
            {
                var encrypted = File.ReadAllBytes(_keyFilePath);
                return ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            }

            var key = GenerateRandomBytes(32);
            SaveProtected(_keyFilePath, key);
            return key;
        }

        ///<inheritdoc/>
        public byte[] GetIV()
        {
            if (File.Exists(_ivFilePath))
            {
                var encrypted = File.ReadAllBytes(_ivFilePath);
                return ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            }

            var iv = GenerateRandomBytes(16);
            SaveProtected(_ivFilePath, iv);
            return iv;
        }

        /// <summary>
        /// Protects the given byte array and writes it to the specified file path.
        /// </summary>
        /// <param name="path">The file path to store the protected data.</param>
        /// <param name="data">The data to protect.</param>
        private void SaveProtected(string path, byte[] data)
        {
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(path, encrypted);
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
