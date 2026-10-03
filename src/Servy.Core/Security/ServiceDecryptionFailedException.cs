using System;

namespace Servy.Core.Security
{
    /// <summary>
    /// Thrown when a service's configuration would be written although at least one of its sensitive fields cannot be
    /// decrypted with the current encryption key, so the write would replace the stored ciphertext with blanks.
    /// </summary>
    /// <remarks>
    /// A field that cannot be decrypted is usually the sign of a corrupt or replaced <c>aes_key.dat</c>, or of a database
    /// copied from another machine. The row is left exactly as it is, so the database can be copied back to the machine
    /// that has the right key and the configuration exported from there.
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
    }
}
