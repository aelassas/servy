namespace Servy.Core.Services
{
    /// <summary>
    /// Defines a contract for validating JSON service configuration strings before they are
    /// persisted to the repository or applied to the system.
    /// </summary>
    public interface IJsonServiceValidator
    {
        /// <summary>
        /// Attempts to validate the provided JSON string against structural, security,
        /// and domain-specific business rules.
        /// </summary>
        /// <param name="json">The raw JSON configuration string to validate.</param>
        /// <param name="errorMessage">When this method returns <c>false</c>, contains a descriptive error message; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the JSON is structurally sound and meets all domain requirements; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method performs the deserialization itself rather than preceding it: it parses the
        /// payload with the same hardened settings the import path uses, runs the shared domain
        /// validation rules over the materialised service definition, and then discards it.
        /// It enforces the configured size limit, rejects unsafe or unknown content, and requires
        /// the properties the Windows Service Control Manager needs (such as ExecutablePath).
        /// A caller that validates and then deserializes the same payload parses it twice.
        /// </remarks>
        bool TryValidate(string json, out string errorMessage);
    }
}
