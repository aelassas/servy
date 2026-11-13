using System;

namespace Servy.Core.Security
{
    /// <summary>
    /// Thrown when a service's configuration would be written although at least one of its sensitive fields cannot be
    /// decrypted with the current encryption key, so the write would replace the stored ciphertext with blanks. Also thrown
    /// when such a service is exported, because the file would be written without its parameters and environment variables.
    /// </summary>
    /// <remarks>
    /// A field that cannot be decrypted is usually the sign of a corrupt or replaced <c>aes_key.dat</c>, or of a database
    /// copied from another machine. The row is left exactly as it is, so the database can be copied back to the machine
    /// that has the right key and the configuration exported from there. A field refused because it still uses the
    /// pre-v2 encryption of an older Servy version is not a key problem: <see cref="ForLegacyEncryption"/> builds the
    /// refusal for that case, with a message that says so and names the upgrade route that works.
    /// </remarks>
    public class ServiceDecryptionFailedException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceDecryptionFailedException"/> class.
        /// </summary>
        /// <param name="serviceName">The service whose row cannot be decrypted.</param>
        /// <param name="fieldName">The first sensitive field that failed to decrypt, or <see langword="null"/> when unknown.</param>
        /// <param name="innerException">The decryption failure, or <see langword="null"/>.</param>
        public ServiceDecryptionFailedException(string serviceName, string fieldName, Exception innerException = null)
            : base(BuildMessage(serviceName, fieldName), innerException)
        {
            ServiceName = serviceName;
            FieldName = fieldName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceDecryptionFailedException"/> class with a message already built.
        /// </summary>
        /// <param name="serviceName">The service whose row cannot be decrypted.</param>
        /// <param name="fieldName">The first sensitive field that failed to decrypt, or <see langword="null"/> when unknown.</param>
        /// <param name="message">The message shown to the user.</param>
        /// <param name="innerException">The decryption failure, or <see langword="null"/>.</param>
        private ServiceDecryptionFailedException(string serviceName, string fieldName, string message, Exception innerException)
            : base(message, innerException)
        {
            ServiceName = serviceName;
            FieldName = fieldName;
        }

        /// <summary>
        /// Creates the refusal for a service whose stored row has a sensitive field that still uses the pre-v2 encryption of
        /// an older Servy version, which this version refuses to decrypt by policy.
        /// </summary>
        /// <param name="serviceName">The service whose row holds the pre-v2 ciphertext.</param>
        /// <param name="fieldName">The first sensitive field refused by the policy, or <see langword="null"/> when unknown.</param>
        /// <param name="innerException">The policy refusal.</param>
        /// <returns>
        /// The exception, whose message says that the encryption key is not involved and that the service must be removed
        /// before the file exported with a v1-compatible version of Servy is imported.
        /// </returns>
        /// <remarks>
        /// The payload was never examined, so naming <c>aes_key.dat</c> as the cause, as <see cref="BuildMessage"/> does,
        /// would send the user looking for another machine's key file. The row is left as it is: removing the service
        /// deletes it, and importing the exported file then writes the configuration with the current encryption.
        /// </remarks>
        public static ServiceDecryptionFailedException ForLegacyEncryption(string serviceName, string fieldName, SecureDataLegacyBlockedException innerException)
            => new ServiceDecryptionFailedException(serviceName, fieldName, BuildLegacyMessage(serviceName, fieldName), innerException);

        /// <summary>
        /// Gets the service whose row cannot be decrypted.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Gets the first sensitive field that failed to decrypt, or <see langword="null"/> when unknown.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Builds the message shown to the user.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="fieldName">The field name, or <see langword="null"/>.</param>
        /// <returns>The message.</returns>
        private static string BuildMessage(string serviceName, string fieldName)
        {
            var field = string.IsNullOrWhiteSpace(fieldName) ? "at least one sensitive field" : $"the sensitive field '{fieldName}'";
            return $"The configuration of service '{serviceName}' was not saved: {field} cannot be decrypted with the current " +
                   "encryption key (aes_key.dat). The database row was left unchanged. If the key file is corrupt or was replaced, " +
                   "copy Servy.db back to the machine that has the original aes_key.dat and export the configuration from there.";
        }

        /// <summary>
        /// Builds the message shown to the user when a sensitive field uses the disabled pre-v2 encryption.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="fieldName">The field name, or <see langword="null"/>.</param>
        /// <returns>The message.</returns>
        private static string BuildLegacyMessage(string serviceName, string fieldName)
        {
            var field = string.IsNullOrWhiteSpace(fieldName) ? "at least one sensitive field" : $"the sensitive field '{fieldName}'";
            return $"The configuration of service '{serviceName}' was not saved: {field} uses the pre-v2 encryption of an older " +
                   "Servy version, which this version refuses to decrypt. The encryption key (aes_key.dat) is not involved, and the " +
                   "database row was left unchanged. To upgrade the service, export its configuration with a v1-compatible version " +
                   "of Servy, remove the service here, then import the exported file.";
        }
    }
}
