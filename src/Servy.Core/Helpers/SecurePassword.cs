using Servy.Core.Security;
using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides methods to securely encrypt and decrypt passwords using AES encryption
    /// with a key and IV protected via Windows DPAPI.
    /// </summary>
    public class SecurePassword: ISecurePassword
    {
        private readonly IProtectedKeyProvider _protectedKeyProvider;

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
                return Convert.ToBase64String(encryptedBytes);
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

            using (var aes = Aes.Create())
            {
                aes.Key = _protectedKeyProvider.GetKey();
                aes.IV = _protectedKeyProvider.GetIV();

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                var cipherBytes = Convert.FromBase64String(cipherText);

                var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
        }
    }
}
