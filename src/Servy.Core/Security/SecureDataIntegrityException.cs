using System.Security.Cryptography;

namespace Servy.Core.Security
{
    /// <summary>
    /// Thrown when a marked ciphertext fails integrity validation (HMAC or structure).
    /// </summary>
    public class SecureDataIntegrityException : CryptographicException
    {
        public SecureDataIntegrityException(string message) : base(message) { }
    }
}