namespace Servy.Core.Services
{
    /// <summary>
    /// Defines a contract for validating XML service configuration strings before they are 
    /// persisted to the repository or applied to the system.
    /// </summary>
    public interface IXmlServiceValidator
    {
        /// <summary>
        /// Attempts to validate the provided XML string against structural, security, 
        /// and domain-specific business rules.
        /// </summary>
        /// <param name="xml">The raw XML configuration string to validate.</param>
        /// <param name="errorMessage">When this method returns <c>false</c>, contains a descriptive error message; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the XML is valid, safe from XXE attacks, and meets domain requirements; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This validation step typically precedes deserialization into a <see cref="DTOs.ServiceDto"/>.
        /// It ensures that the payload does not exceed size limits and that all required 
        /// service properties (like ExecutablePath) are present and valid.
        /// </remarks>
        bool TryValidate(string? xml, out string? errorMessage);
    }
}