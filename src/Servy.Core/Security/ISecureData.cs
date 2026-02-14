namespace Servy.Core.Security
{
    /// <summary>
    /// Provides an abstraction for securely encrypting and decrypting data.
    /// </summary>
    public interface ISecureData
    {
        /// <summary>
        /// Encrypts the specified plain text data.
        /// </summary>
        /// <param name="plainText">The plain text data to encrypt.</param>
        /// <returns>A base64-encoded encrypted string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="plainText"/> is empty.</exception>
        string Encrypt(string plainText);

        /// <summary>
        /// Decrypts the specified AES-encrypted data.
        /// </summary>
        /// <param name="cipherText">The base64-encoded encrypted data.</param>
        /// <returns>The original plain text data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cipherText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="cipherText"/> is empty.</exception>
        string Decrypt(string cipherText);
    }
}
