using Servy.Core.Resources;

namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Provides validation methods for environment variables strings with escaping support.
    /// </summary>
    public static class EnvironmentVariablesValidator
    {
        /// <summary>
        /// Validates the format of the environment variables input. Supports variables separated by
        /// unescaped semicolons or new lines. Each record must contain at least one unescaped equals
        /// character and a non-empty key; after unescaping, the key may not contain a newline, a null
        /// terminator or an equals sign, and the value may not contain a newline or a null terminator.
        /// See <see cref="EnvVarValidationResultKind"/> for the full rule set.
        /// </summary>
        /// <param name="environmentVariables">The raw environment variables string to validate.</param>
        /// <param name="errors">When validation fails, contains the error messages describing the issues; otherwise, an empty list.</param>
        /// <returns>A boolean value indicating true if the input is valid, or false if format violations were detected.</returns>
        public static bool Validate(string? environmentVariables, out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(environmentVariables))
            {
                // No error if empty
                return true;
            }

            // Split input by unescaped semicolons and newlines
            var variables = EscapedTokenizer.SplitByUnescapedDelimiters(environmentVariables.Replace("\r\n", "\n"), EscapedTokenizer.EnvVarRecordDelimiters);

            for (int i = 0; i < variables.Length; i++)
            {
                var variable = variables[i];

                // Skip empty segments (possible if input ends with delimiter)
                if (string.IsNullOrWhiteSpace(variable))
                    continue;

                // Same 1-based split index the parser reports, so both callers name the same record
                if (!ProcessAndValidateRecord(variable, i + 1, out _, out _, out string errorMessage, out _))
                {
                    errors.Add(errorMessage);
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Shared syntax validation block used by both Validator and Parser to guarantee alignment.
        /// </summary>
        internal static bool ProcessAndValidateRecord(string part, int recordPosition, out string key, out string value, out string errorMessage, out EnvVarValidationResultKind resultKind)
        {
            key = string.Empty;
            value = string.Empty;
            errorMessage = string.Empty;

            // Initialize the machine-readable discriminator to a safe failure baseline default
            resultKind = EnvVarValidationResultKind.GeneralFailure;

            // Find first unescaped '='
            int eqIdx = EscapedTokenizer.IndexOfUnescapedChar(part, '=');
            if (eqIdx < 0)
            {
                errorMessage = string.Format(Strings.Msg_EnvironmentVariableMissingEquals, recordPosition);
                resultKind = EnvVarValidationResultKind.MissingEquals;
                return false;
            }

            // Extract raw key and value substrings
            var rawKey = part.Substring(0, eqIdx);
            var rawValue = part.Substring(eqIdx + 1);

            key = EscapedTokenizer.Unescape(rawKey.Trim());

            if (string.IsNullOrEmpty(key))
            {
                errorMessage = string.Format(Strings.Msg_EnvironmentVariableKeyEmpty, recordPosition);
                resultKind = EnvVarValidationResultKind.EmptyKey;
                return false;
            }

            // ROBUSTNESS: Guard the unescaped key against illegal character injections
            // (CR, LF, Null-Terminator, and '=' which Windows forbids in a variable NAME).
            if (key.Contains("\n") || key.Contains("\r"))
            {
                errorMessage = string.Format(Strings.Msg_EnvironmentVariableForbiddenNewline, key);
                resultKind = EnvVarValidationResultKind.ForbiddenNewline;
                return false;
            }

            if (key.Contains("\0") || key.Contains("="))
            {
                errorMessage = string.Format(Strings.Msg_EnvironmentVariableKeyInvalidChars, key);
                resultKind = EnvVarValidationResultKind.GeneralFailure;
                return false;
            }

            // 1. Trim whitespace first to expose structural quotes
            var trimmedValue = rawValue.Trim();

            // 2. Remove surrounding quotes ONLY. By doing this BEFORE unescaping,
            // we allow users to pass escaped quotes (e.g., \"hello\") that bypass this
            // structural strip and survive into the final value.
            if (trimmedValue.Length >= 2
               && trimmedValue[0] == '"'
               && trimmedValue[trimmedValue.Length - 1] == '"'
               && !EscapedTokenizer.IsEscapedAt(trimmedValue, trimmedValue.Length - 1))
            {
                trimmedValue = trimmedValue.Substring(1, trimmedValue.Length - 2);
            }

            // 3. Finally, unescape the inner content
            value = EscapedTokenizer.Unescape(trimmedValue);

            if (value.Contains("\n") || value.Contains("\r"))
            {
                errorMessage = string.Format(Strings.Msg_EnvironmentVariableForbiddenNewline, key);
                resultKind = EnvVarValidationResultKind.ForbiddenNewline;
                return false;
            }

            if (value.Contains("\0"))
            {
                errorMessage = string.Format(Strings.Msg_EnvironmentVariableValueInvalidChars, key);
                resultKind = EnvVarValidationResultKind.GeneralFailure;
                return false;
            }

            // Return clean success state across all validation boundaries
            resultKind = EnvVarValidationResultKind.Success;
            return true;
        }
    }
}
