using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Resources;
using System.Diagnostics.CodeAnalysis;
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
        /// Gets the maximum allowed payload size in bytes for configuration import documents.
        /// Defaults to <see cref="AppConfig.MaxConfigFileSizeBytes"/> and can be overridden for testing.
        /// </summary>
        protected virtual long MaxPayloadBytes => AppConfig.MaxConfigFileSizeBytes;

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
        /// Composes the detail fragment appended to an import failure message: the inner
        /// exception's message with the wrapper's in parentheses when there is one, and the
        /// exception's own message otherwise.
        /// </summary>
        /// <param name="ex">The exception raised while parsing the import payload.</param>
        /// <returns>The detail fragment for the caller-facing error message.</returns>
        private static string DetailMessage(Exception ex) =>
            ex.InnerException != null ? $"{ex.InnerException.Message} ({ex.Message})" : ex.Message;

        /// <summary>
        /// Validates the input content to ensure it can be deserialized and meets all service rules.
        /// </summary>
        /// <param name="content">The raw configuration string.</param>
        /// <param name="errorMessage">When this method returns, contains the error message if validation failed.</param>
        /// <returns><c>true</c> if validation succeeded; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// The parsed definition is discarded. Callers that also need it should use
        /// <see cref="TryValidate(string?, out string?, out ServiceDto?)"/>, which parses the payload once.
        /// </remarks>
        public bool TryValidate(string? content, [NotNullWhen(false)] out string? errorMessage) =>
            TryValidateCore(content, out errorMessage, out _);

        /// <summary>
        /// Validates the input content and hands back the definition it parsed, so that a caller which
        /// needs the object does not have to parse the same payload a second time.
        /// </summary>
        /// <param name="content">The raw configuration string.</param>
        /// <param name="errorMessage">When this method returns, contains the error message if validation failed.</param>
        /// <param name="dto">When this method returns <c>true</c>, contains the parsed definition with
        /// <see cref="ServiceDtoHelper.ApplyDefaultsAndResetIdentity"/> already applied; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if validation succeeded; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Hydration runs after validation, never before, so the domain rules still see the raw parse and
        /// both overloads accept and reject exactly the same payloads.
        /// </remarks>
        public bool TryValidate(string? content, out string? errorMessage, out ServiceDto? dto)
        {
            if (!TryValidateCore(content, out errorMessage, out dto) || dto == null)
            {
                dto = null;
                return false;
            }

            // Reproduce what ServiceDtoSerializer.Deserialize did for the caller that used to re-parse:
            // hydrate absent optional fields from AppConfig and apply the Global Identity Reset on Import.
            ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);

            return true;
        }

        /// <summary>
        /// Performs the size, structural and domain validation shared by both <c>TryValidate</c> overloads
        /// and returns the raw parse, with no defaults hydrated and no identity reset applied.
        /// </summary>
        /// <param name="content">The raw configuration string.</param>
        /// <param name="errorMessage">When this method returns, contains the error message if validation failed.</param>
        /// <param name="dto">When this method returns <c>true</c>, contains the raw parsed definition; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if validation succeeded; otherwise, <c>false</c>.</returns>
        private bool TryValidateCore(string? content, out string? errorMessage, out ServiceDto? dto)
        {
            errorMessage = null;
            dto = null;

            if (string.IsNullOrWhiteSpace(content))
            {
                errorMessage = string.Format(Strings.Msg_ImportInputEmptyOrWhitespace, FormatName);
                Logger.Warn($"{FormatName} import blocked: input was empty or whitespace.");
                return false;
            }

            // Prevent Memory Exhaustion / DoS
            // Note: File-based imports are pre-bounded by ImportGuard at AppConfig.MaxConfigFileSizeBytes.
            // This check provides defense-in-depth for direct callers of TryValidate passing raw strings.
            long byteLength = Encoding.UTF8.GetByteCount(content);
            if (byteLength > MaxPayloadBytes)
            {
                errorMessage = string.Format(Strings.Msg_ImportPayloadTooLarge, FormatName, AppConfig.MaxConfigFileSizeMB);
                Logger.Error(errorMessage);
                return false;
            }

            // 1. Structural Validation & Deserialization
            try
            {
                dto = Parse(content);
            }
            // ROBUSTNESS: Match the specific structural exception type or an InvalidOperationException
            // that encapsulates the structural exception as an InnerException (common with XmlSerializer).
            // This prevents the first catch from consuming unrelated exceptions when TException is narrowed.
            catch (Exception ex) when (ex is TException || (ex is InvalidOperationException && ex.InnerException is TException))
            {
                string detailMessage = DetailMessage(ex);
                errorMessage = string.Format(Strings.Msg_ImportInvalidStructure, FormatName, detailMessage);
                Logger.Error($"{FormatName} import blocked: malformed document structure.", ex);
                return false;
            }
            catch (Exception ex) // Catch-all for unexpected parser exceptions
            {
                string detailMessage = DetailMessage(ex);
                errorMessage = string.Format(Strings.Msg_ImportStructureError, FormatName, detailMessage);
                Logger.Error($"{FormatName} import blocked: unexpected parser exception ({ex.GetType().Name}).", ex);
                return false;
            }

            if (dto == null)
            {
                errorMessage = string.Format(Strings.Msg_ImportEmptyDefinition, FormatName);
                Logger.Warn($"{FormatName} import blocked: parser returned no service definition.");
                return false;
            }

            // 2. DEEP DOMAIN VALIDATION
            var validation = _serviceValidationRules.Validate(dto, importMode: true);
            if (!validation.IsValid)
            {
                errorMessage = string.Join("\n", validation.Errors);

                Logger.Warn($"{FormatName} import blocked: logical violation for service '{dto.Name ?? "Unknown"}'. Reason: {errorMessage}");
                dto = null;
                return false;
            }

            return true;
        }
    }
}
