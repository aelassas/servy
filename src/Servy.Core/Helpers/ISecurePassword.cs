using System;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides an abstraction for securely encrypting and decrypting passwords.
    /// </summary>
    public interface ISecurePassword
    {
        /// <summary>
        /// Encrypts the specified plain text password.
        /// </summary>
        /// <param name="plainText">The plain text password to encrypt.</param>
        /// <returns>A base64-encoded encrypted string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainText"/> is null or empty.</exception>
        string Encrypt(string plainText);

        /// <summary>
        /// Decrypts the specified AES-encrypted password.
        /// </summary>
        /// <param name="cipherText">The base64-encoded encrypted password.</param>
        /// <returns>The original plain text password.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cipherText"/> is null or empty.</exception>
        string Decrypt(string cipherText);
    }
}
