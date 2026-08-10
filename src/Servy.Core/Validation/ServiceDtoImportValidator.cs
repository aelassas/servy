using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Resources;
using System.Text;

namespace Servy.Core.Validation
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
        protected abstract ServiceDto? Parse(string content);

        /// <summary>
        /// Validates the input content to ensure it can be deserialized and meets all service rules.
        /// </summary>
        /// <param name="content">The raw configuration string.</param>
        /// <param name="errorMessage">When this method returns, contains the error message if validation failed.</param>
        /// <returns><c>true</c> if validation succeeded; otherwise, <c>false</c>.</returns>
        public bool TryValidate(string? content, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(content))
            {
                errorMessage = string.Format(Strings.Msg_ImportInputEmptyOrWhitespace, FormatName);
                Logger.Warn($"{FormatName} Import Blocked: input was empty or whitespace.");
                return false;
            }

            // Prevent Memory Exhaustion / DoS
            // Convert to byte count for accurate protection against multibyte UTF-8 payloads
            long byteLength = Encoding.UTF8.GetByteCount(content);
            if (byteLength > AppConfig.MaxConfigFileSizeBytes)
            {
                errorMessage = string.Format(Strings.Msg_ImportPayloadTooLarge, FormatName, AppConfig.MaxConfigFileSizeMB);
                Logger.Error(errorMessage);
                return false;
            }

            // 1. Structural Validation & Deserialization
            ServiceDto? dto;
            try
            {
                dto = Parse(content);
            }
            // ROBUSTNESS: Match the specific structural exception type or an InvalidOperationException
            // that encapsulates the structural exception as an InnerException (common with XmlSerializer).
            // This prevents the first catch from consuming unrelated exceptions when TException is narrowed.
            catch (Exception ex) when (ex is TException || (ex is InvalidOperationException && ex.InnerException is TException))
            {
                string detailMessage = ex.InnerException != null ? $"{ex.InnerException.Message} ({ex.Message})" : ex.Message;
                errorMessage = string.Format(Strings.Msg_ImportInvalidStructure, FormatName, detailMessage);
                Logger.Error($"{FormatName} import rejected: malformed document structure", ex);
                return false;
            }
            catch (Exception ex) // Catch-all for unexpected parser exceptions
            {
                string detailMessage = ex.InnerException != null ? $"{ex.InnerException.Message} ({ex.Message})" : ex.Message;
                errorMessage = string.Format(Strings.Msg_ImportStructureError, FormatName, detailMessage);
                Logger.Error($"{FormatName} import failed with an unexpected parser exception ({ex.GetType().Name})", ex);
                return false;
            }

            if (dto == null)
            {
                errorMessage = string.Format(Strings.Msg_ImportEmptyDefinition, FormatName);
                Logger.Warn($"{FormatName} Import Blocked: parser returned no service definition.");
                return false;
            }

            // 2. DEEP DOMAIN VALIDATION
            var validation = _serviceValidationRules.Validate(dto, importMode: true);
            if (!validation.IsValid)
            {
                errorMessage = string.Join("\n", validation.Errors);

                Logger.Warn($"{FormatName} Import Blocked: Logical violation for service '{dto.Name ?? "Unknown"}'. Reason: {errorMessage}");
                return false;
            }

            return true;
        }
    }
}
