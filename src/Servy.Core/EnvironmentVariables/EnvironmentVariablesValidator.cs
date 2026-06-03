using Servy.Core.Resources;

namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Provides validation methods for environment variables strings with escaping support.
    /// </summary>
    public static class EnvironmentVariablesValidator
    {
        /// <summary>
        /// Validates the format of the environment variables input. Supports variables separated by unescaped semicolons or new lines. 
        /// Checks that each variable contains at least one unescaped equals character, and that the variable key before the equals sign is not empty.
        /// </summary>
        /// <param name="environmentVariables">The raw environment variables string to validate.</param>
        /// <param name="errorMessage">When validation fails, contains the error message describing the issue; otherwise, an empty string.</param>
        /// <returns>A boolean value indicating true if the input is valid, or false if format violations were detected.</returns>
        public static bool Validate(string environmentVariables, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(environmentVariables))
            {
                // No error if empty
                return true;
            }

            // Split input by unescaped semicolons and newlines
            var variables = EscapedTokenizer.SplitByUnescapedDelimiters(environmentVariables, EscapedTokenizer.EnvVarRecordDelimiters);

            foreach (var variable in variables)
            {
                // Skip empty segments (possible if input ends with delimiter)
                if (string.IsNullOrWhiteSpace(variable))
                    continue;

                // Call the centralized grammar rule validation engine to guarantee parity with Parser checks
                if (!ProcessAndValidateRecord(variable, out _, out _, out errorMessage))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Shared syntax validation block used by both Validator and Parser to guarantee alignment.
        /// </summary>
        internal static bool ProcessAndValidateRecord(string part, out string key, out string value, out string errorMessage)
        {
            key = string.Empty;
            value = string.Empty;
            errorMessage = string.Empty;

            // Find first unescaped '='
            int eqIdx = EscapedTokenizer.IndexOfUnescapedChar(part, '=');
            if (eqIdx < 0)
            {
                errorMessage = Strings.Msg_EnvironmentVariableMissingEquals;
                return false;
            }

            // Extract raw key and value substrings
            var rawKey = part.Substring(0, eqIdx);
            var rawValue = part.Substring(eqIdx + 1);

            key = EscapedTokenizer.Unescape(rawKey).Trim();

            if (string.IsNullOrEmpty(key))
            {
                errorMessage = Strings.Msg_EnvironmentVariableKeyEmpty;
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
                return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the index of the first unescaped occurrence of a character in a string. This method is maintained as a public wrapper for backward compatibility.
        /// </summary>
        /// <param name="str">The input string to search.</param>
        /// <param name="ch">The character to find.</param>
        /// <returns>The zero-based index of the first unescaped occurrence of the target character, or negative one if not found.</returns>
        public static int IndexOfUnescapedChar(string str, char ch)
        {
            return EscapedTokenizer.IndexOfUnescapedChar(str, ch);
        }
    }
}