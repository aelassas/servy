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
        /// This validation step acts as a "Gatekeeper" prior to full deserialization. It ensures 
        /// the payload adheres to size constraints, utilizes safe deserialization settings 
        /// to prevent untrusted data exploits, and contains a valid service definition 
        /// compatible with the Windows Service Control Manager.
        /// </remarks>
        bool TryValidate(string json, out string errorMessage);
    }
}