using System;

namespace Servy.Core.Security
{
    /// <summary>
    /// Thrown when a ciphertext is refused by policy rather than failing validation - currently, a v1 or
    /// raw legacy payload encountered while <see cref="Config.AppConfig.AllowLegacyV1Decryption"/> is false.
    /// The payload was not examined; it is not evidence of corruption.
    /// </summary>
    public class SecureDataLegacyBlockedException : SecureDataIntegrityException
    {
        /// <summary>Initializes a new instance with the specified error message.</summary>
        /// <param name="message">The message that describes the policy refusal.</param>
        public SecureDataLegacyBlockedException(string message) : base(message) { }

        /// <summary>Initializes a new instance with the specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the policy refusal.</param>
        /// <param name="innerException">The exception that caused the policy refusal.</param>
        public SecureDataLegacyBlockedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
