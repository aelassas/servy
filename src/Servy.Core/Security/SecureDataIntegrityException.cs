using System.Security.Cryptography;

namespace Servy.Core.Security
{
    /// <summary>
    /// Thrown when a marked ciphertext fails integrity validation (HMAC or structure).
    /// </summary>
    public class SecureDataIntegrityException : CryptographicException
    {
        /// <summary>Initializes a new instance with the specified error message.</summary>
        /// <param name="message">The message that describes the integrity failure.</param>
        public SecureDataIntegrityException(string message) : base(message) { }

        /// <summary>Initializes a new instance with the specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the integrity failure.</param>
        /// <param name="innerException">The exception that caused the integrity failure.</param>
        public SecureDataIntegrityException(string message, Exception innerException) : base(message, innerException) { }
    }
}