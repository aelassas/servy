using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using System;
using System.Linq;
using System.Text;

namespace Servy.Core.Validators
{
    /// <summary>
    /// Provides a common base class for validating imported service definitions.
    /// Ensures consistent SCM rule enforcement, DoS protection, and logging across all supported import formats.
    /// </summary>
    /// <typeparam name="TException">The specific type of exception expected during parsing.</typeparam>
    public abstract class ServiceDtoImportValidator<TException> where TException : Exception
    {
        private readonly IServiceValidationRules _serviceValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceDtoImportValidator{TException}"/> class.
        /// </summary>
        /// <param name="serviceValidationRules">Provides rules for validating service properties.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceValidationRules"/> is null.</exception>
        protected ServiceDtoImportValidator(IServiceValidationRules serviceValidationRules)
        {
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
        }

        /// <summary>
        /// Gets the name of the format being validated (e.g., "XML", "JSON").
        /// Used for consistent logging and error messages.
        /// </summary>
        protected abstract string FormatName { get; }

        /// <summary>
        /// Parses the raw string content into a <see cref="ServiceDto"/>.
        /// </summary>
        /// <param name="content">The raw string content.</param>
        /// <returns>The deserialized <see cref="ServiceDto"/>, or null if deserialization yields no object.</returns>
        protected abstract ServiceDto Parse(string content);

        /// <summary>
        /// Validates the input content to ensure it can be deserialized and meets all service rules.
        /// </summary>
        /// <param name="content">The raw configuration string.</param>
        /// <param name="errorMessage">When this method returns, contains the error message if validation failed.</param>
        /// <returns><c>true</c> if validation succeeded; otherwise, <c>false</c>.</returns>
        public bool TryValidate(string content, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(content))
            {
                errorMessage = $"{FormatName} input cannot be null or empty.";
                return false;
            }

            // Prevent Memory Exhaustion / DoS
            // FIX: Convert to byte count for accurate protection against multibyte UTF-8 payloads
            long byteLength = Encoding.UTF8.GetByteCount(content);
            if (byteLength > AppConfig.MaxConfigFileSizeBytes)
            {
                errorMessage = $"{FormatName} payload exceeds the maximum allowed size of {AppConfig.MaxConfigFileSizeMB} MB.";
                Logger.Warn($"{FormatName} Import Blocked: Payload size limit exceeded.");
                return false;
            }

            // 1. Structural Validation & Deserialization
            ServiceDto dto;
            try
            {
                dto = Parse(content);
            }
            // ROBUSTNESS: Match the specific structural exception type or an InvalidOperationException 
            // that encapsulates the structural exception as an InnerException (common with XmlSerializer).
            // This prevents the first catch from consuming unrelated exceptions when TException is narrowed.
            catch (Exception ex) when (ex is TException || (ex is InvalidOperationException && ex.InnerException is TException))
            {
                errorMessage = $"Invalid {FormatName} structure: {ex.Message}";
                Logger.Error($"{FormatName} parsing error during import", ex);
                return false;
            }
            catch (Exception ex) // Catch-all for unexpected parser exceptions
            {
                errorMessage = $"{FormatName} structure error: {ex.Message}";
                Logger.Error($"{FormatName} parsing error during import", ex);
                return false;
            }

            if (dto == null)
            {
                errorMessage = $"{FormatName} deserialization resulted in an empty service definition.";
                return false;
            }

            // 2. DEEP DOMAIN VALIDATION
            var sanitizedName = (dto.Name ?? "Unknown").Replace("\r", "").Replace("\n", ""); // Sanitize the untrusted name to prevent log injection
            var validation = _serviceValidationRules.Validate(dto, importMode: true);
            if (validation.Errors.Any())
            {
                errorMessage = string.Join("\n", validation.Errors);

                Logger.Warn($"{FormatName} Import Blocked: Logical violation for service '{sanitizedName}'. Reason: {errorMessage}");
                return false;
            }

            return true;
        }
    }
}