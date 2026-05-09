using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Validators;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides strict validation for JSON strings representing a <see cref="ServiceDto"/>.
    /// Ensures both structural integrity and Windows SCM compatibility.
    /// </summary>
    public class JsonServiceValidator: IJsonServiceValidator
    {
        private readonly IProcessHelper _processHelper;
        private readonly IServiceValidationRules _serviceValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonServiceValidator"/> class with the specified process helper.
        /// </summary>
        /// <param name="processHelper">Provides methods to validate executable paths and gather process metrics.</param>
        /// <param name="serviceValidationRules">Provides rules for validating service properties.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="processHelper"/> or <paramref name="serviceValidationRules"/> is null.</exception>
        public JsonServiceValidator(IProcessHelper processHelper, IServiceValidationRules serviceValidationRules)
        {
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
        }

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
            if (json.Length > AppConfig.MaxConfigFileSizeBytes)
            {
                errorMessage = $"JSON payload exceeds the maximum allowed size of {AppConfig.MaxConfigFileSizeMB} MB.";
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
            var sanitizedName = (dto.Name ?? "Unknown").Replace("\r", "").Replace("\n", ""); // Sanitize the untrusted name to prevent log injection
            var validation = _serviceValidationRules.Validate(dto);
            if (validation.Errors.Any())
            {
                errorMessage = string.Join("\n", validation.Errors);

                Logger.Warn($"JSON Import Blocked: Logical violation for service '{sanitizedName}'. Reason: {errorMessage}");
                return false;
            }

            if (validation.Warnings.Any())
            {
                Logger.Warn($"JSON Import succeeded with warnings for service '{sanitizedName}': {string.Join("\n", validation.Warnings)}");
            }

            return true;
        }
    }
}