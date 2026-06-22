using System;
using System.Security.Cryptography;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides an abstraction for securely encrypting and decrypting data.
    /// Implementations are expected to be thread-safe and handle internal cryptographic key material lifecycle management.
    /// </summary>
    public interface ISecureData: IDisposable
    {
        /// <summary>
        /// Encrypts the specified plain text data.
        /// </summary>
        /// <param name="plainText">The plain text data to encrypt.</param>
        /// <returns>A versioned, base64-encoded encrypted string with a version/encryption marker prefix (e.g. <c>SERVY_ENC:v2:</c>).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="plainText"/> is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the underlying cryptographic provider instance has already been disposed.</exception>
        string Encrypt(string plainText);

        /// <summary>
        /// Decrypts a ciphertext string by automatically detecting the encryption version prefix or formatting layout.
        /// </summary>
        /// <remarks>
        /// Upstream callers (UI/CLI) must handle integrity failures explicitly to prevent processing tampered records 
        /// or falling back to dangerous default states during a downgrade attack vector.
        /// </remarks>
        /// <param name="cipherText">The versioned ciphertext string (containing structural markers) or a raw legacy string.</param>
        /// <returns>
        /// The decrypted original plain text on success, or the original <paramref name="cipherText"/> 
        /// unchanged only when the input string contains no version prefix marker and does not conform to strict Base64 formatting.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cipherText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="cipherText"/> is empty.</exception>
        /// <exception cref="SecureDataIntegrityException">
        /// Thrown under the following integrity compromise conditions:
        /// <list type="bullet">
        /// <item><description>A marked v2 payload fails HMAC authentication check or structural verification (tampering or corruption).</description></item>
        /// <item><description>A marked v1 payload is passed while legacy unauthenticated decryption is disabled via system configuration.</description></item>
        /// <item><description>The input lacks a prefix marker but parses as strict Base64 text while legacy decryption is disabled.</description></item>
        /// <item><description>The version marker prefix embedded within the ciphertext layout is unrecognized by the system.</description></item>
        /// </list>
        /// </exception>
        /// <exception cref="CryptographicException">Thrown when legacy unauthenticated v1 decryption is enabled but the cipher layer fails padding verification.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the underlying cryptographic provider instance has already been disposed.</exception>
        string Decrypt(string cipherText);
    }
}