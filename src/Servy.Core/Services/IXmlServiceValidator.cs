using System.Diagnostics.CodeAnalysis;

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
        /// This method performs the deserialization into a <see cref="DTOs.ServiceDto"/> itself rather
        /// than preceding it: it parses the payload with the same hardened settings the import path
        /// uses, runs the shared domain validation rules over the materialised service definition,
        /// and then discards it. It enforces the configured size limit, rejects unsafe or unknown
        /// content, and requires the properties the Windows Service Control Manager needs (such as
        /// ExecutablePath). A caller that validates and then deserializes the same payload parses it twice.
        /// </remarks>
        bool TryValidate(string? xml, [NotNullWhen(false)] out string? errorMessage);

        /// <summary>
        /// Attempts to validate the provided XML string and hands back the definition it parsed.
        /// </summary>
        /// <param name="xml">The raw XML configuration string to validate.</param>
        /// <param name="errorMessage">When this method returns <c>false</c>, contains a descriptive error message; otherwise, <c>null</c>.</param>
        /// <param name="dto">When this method returns <c>true</c>, contains the parsed service definition with
        /// defaults hydrated and the Global Identity Reset on Import applied; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the XML is valid and meets domain requirements; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This is the overload the import pipelines use: it performs the single parse of the payload, so a
        /// caller no longer has to hand the same string to a deserializer afterwards.
        /// </remarks>
        bool TryValidate(string? xml, out string? errorMessage, out DTOs.ServiceDto? dto);
    }
}
