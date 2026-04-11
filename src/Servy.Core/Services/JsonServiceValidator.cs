using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Helpers;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides strict validation for JSON strings representing a <see cref="ServiceDto"/>.
    /// Ensures both structural integrity and Windows SCM compatibility.
    /// </summary>
    public static class JsonServiceValidator
    {
        /// <summary>
        /// Validates that the input JSON is a valid <see cref="ServiceDto"/>, contains required fields,
        /// and adheres to strict domain safety limits.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <param name="errorMessage">If validation fails, contains the specific domain or security error.</param>
        /// <returns>True if valid and safe; otherwise false.</returns>
        public static bool TryValidate(string? json, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "JSON input cannot be null or empty.";
                return false;
            }

            // 1. Structural Validation & Deserialization
            ServiceDto? dto;
            try
            {
                // Note: We continue using JsonSecurity.UntrustedDataSettings 
                // to prevent TypeNameHandling and other injection vulnerabilities.
                dto = JsonConvert.DeserializeObject<ServiceDto>(json, JsonSecurity.UntrustedDataSettings);
            }
            catch (Exception ex)
            {
                errorMessage = $"Invalid JSON structure: {ex.Message}";
                Logger.Error("JSON parsing error during import", ex);
                return false;
            }

            if (dto == null)
            {
                errorMessage = "Deserialization resulted in an empty service definition.";
                return false;
            }

            // 2. DEEP DOMAIN VALIDATION
            // This applies the centralized rules for lengths, timeouts, and numeric ranges.
            var validation = ServiceValidator.ValidateDto(dto);
            if (!validation.IsValid)
            {
                errorMessage = validation.ErrorMessage;
                Logger.Warn($"JSON Import Blocked: Logical violation for service '{dto.Name}'. Reason: {errorMessage}");
                return false;
            }

            // 3. Executable Path Integrity
            // Ensuring the imported path is at least syntactically valid for the host OS.
            if (!ProcessHelper.ValidatePath(dto.ExecutablePath))
            {
                errorMessage = "The provided executable path is invalid or inaccessible.";
                return false;
            }

            return true;
        }
    }
}