using Servy.Core.Security;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides methods to securely encrypt and decrypt passwords using AES encryption
    /// with a key and IV protected via Windows DPAPI.
    /// </summary>
    public class SecurePassword : ISecurePassword
    {
        private readonly IProtectedKeyProvider _protectedKeyProvider;

        private const string EncryptMarker = "SERVY_ENC:";

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurePassword"/> class.
        /// </summary>
        /// <param name="protectedKeyProvider">The provider for the AES key and IV.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="protectedKeyProvider"/> is null.</exception>
        public SecurePassword(IProtectedKeyProvider protectedKeyProvider)
        {
            _protectedKeyProvider = protectedKeyProvider ?? throw new ArgumentNullException(nameof(protectedKeyProvider));
        }

        /// <summary>
        /// Encrypts the specified plain text password.
        /// </summary>
        /// <param name="plainText">The plain text password to encrypt.</param>
        /// <returns>A base64-encoded encrypted string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainText"/> is null or empty.</exception>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));

            using (var aes = Aes.Create())
            {
                aes.Key = _protectedKeyProvider.GetKey();
                aes.IV = _protectedKeyProvider.GetIV();

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                var plainBytes = Encoding.UTF8.GetBytes(plainText);

                var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return EncryptMarker + Convert.ToBase64String(encryptedBytes);
            }
        }

        /// <summary>
        /// Decrypts the specified AES-encrypted password.
        /// </summary>
        /// <param name="cipherText">The base64-encoded encrypted password.</param>
        /// <returns>The original plain text password.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cipherText"/> is null or empty.</exception>
        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentNullException(nameof(cipherText));

            bool hasMarker = cipherText.StartsWith(EncryptMarker, StringComparison.Ordinal);
            var payload = hasMarker ? cipherText.Substring(EncryptMarker.Length) : cipherText;

            // Fast path: try base64 decode first
            if (!IsBase64(payload))
            {
                // Plain text (or invalid legacy data)
                return payload;
            }

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _protectedKeyProvider.GetKey();
                    aes.IV = _protectedKeyProvider.GetIV();

                    var decryptor = aes.CreateDecryptor();
                    var cipherBytes = Convert.FromBase64String(payload);
                    var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch
            {
                return payload;
            }
        }

        /// <summary>
        /// Determines whether the specified string is a valid Base64-encoded value.
        /// </summary>
        /// <param name="value">
        /// The string to evaluate. Leading and trailing whitespace is not allowed.
        /// </param>
        /// <returns>
        /// <c>true</c> if the string is a valid Base64 representation; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method performs a strict validation using <see cref="Convert.TryFromBase64String"/>.
        /// It does not allocate managed memory and does not throw exceptions for invalid input.
        /// </remarks>
        private static bool IsBase64(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Base64 length must be divisible by 4
            if (value.Length % 4 != 0)
                return false;

            try
            {
                Convert.FromBase64String(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

    }
}
