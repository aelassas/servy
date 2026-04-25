using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides strict validation for JSON strings representing a <see cref="ServiceDto"/>.
    /// Ensures both structural integrity and Windows SCM compatibility.
    /// </summary>
    public class JsonServiceValidator: IJsonServiceValidator
    {
        /// <inheritdoc/>
        public bool TryValidate(string? json, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "JSON input cannot be null or empty.";
                return false;
            }

            // Prevent Memory Exhaustion / DoS
            if (json.Length > AppConfig.MaxImportPayloadSizeChars)
            {
                errorMessage = $"JSON payload exceeds the maximum allowed size of {AppConfig.MaxImportPayloadSizeChars} characters.";
                Logger.Warn("JSON Import Blocked: Payload size limit exceeded.");
                return false;
            }

            // 1. Structural Validation & Deserialization
            ServiceDto? dto;
            try
            {
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
            var validation = ServiceValidator.ValidateDto(dto);
            if (!validation.IsValid)
            {
                errorMessage = validation.ErrorMessage;

                // Sanitize the untrusted name to prevent log injection
                // We strip newlines and carriage returns to keep the log entry on a single line.
                var sanitizedName = (dto.Name ?? "Unknown").Replace("\r", "").Replace("\n", "");

                Logger.Warn($"JSON Import Blocked: Logical violation for service '{sanitizedName}'. Reason: {errorMessage}");
                return false;
            }

            // 3. Executable Path Integrity
            if (!ProcessHelper.ValidatePath(dto.ExecutablePath))
            {
                errorMessage = "The provided executable path is invalid or inaccessible.";
                return false;
            }

            return true;
        }
    }
}