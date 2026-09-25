using Servy.Core.Resources;

namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Provides methods to parse environment variables strings with escaping support.
    /// </summary>
    public static class EnvironmentVariableParser
    {
        /// <summary>
        /// Parses a normalized environment variables string into a list of environment variable objects.
        /// Supports escaping of equals signs and semicolons with a backslash, and supports both semicolon and newline delimiters.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Quote Handling:</b> Unescaped double quotes surrounding a value are automatically stripped to support
        /// common configuration conventions (e.g., <c>KEY="value"</c> becomes <c>value</c>).
        /// To enforce a value that literally begins and ends with double quotes, escape the quotes
        /// (e.g., <c>KEY=\"value\"</c>). Outer structural quotes may also wrap escaped inner quotes -
        /// <c>KEY="\"value\""</c> yields <c>"value"</c> - see Parse_NestedQuotes_PreservedWhenOuterAreStructural.
        /// </para>
        /// </remarks>
        /// <param name="input">The normalized environment variables string containing semicolon or newline separators with optional escapes.</param>
        /// <returns>A list of parsed environment variables as instantiated objects.</returns>
        /// <exception cref="FormatException">Thrown for any record that fails
        /// <see cref="EnvironmentVariablesValidator.ProcessAndValidateRecord"/>: a missing unescaped
        /// equals sign, an empty key, a forbidden newline in the key or value, a null terminator or
        /// equals sign in the key, or a null terminator in the value. See
        /// <see cref="EnvVarValidationResultKind"/>.</exception>
        public static List<EnvironmentVariable> Parse(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<EnvironmentVariable>();

            var result = new List<EnvironmentVariable>();

            // Sync delimiters with the Validator to support multi-line input;
            // normalize \r\n to \n first so Windows line endings are treated as a single record boundary.
            var parts = EscapedTokenizer.SplitByUnescapedDelimiters(input.Replace("\r\n", "\n"), EscapedTokenizer.EnvVarRecordDelimiters);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (string.IsNullOrWhiteSpace(part))
                    continue;

                int recordPosition = i + 1;

                // Delegate execution to the centralized validation rules block to maintain perfect logic alignment
                if (!EnvironmentVariablesValidator.ProcessAndValidateRecord(part, recordPosition, out string key, out string value, out string errorMessage, out EnvVarValidationResultKind resultKind))
                {
                    // Map the structured validation result to a specific FormatException without leaking raw record values.
                    // Using the enum (rather than matching localized message text) keeps the
                    // mapping correct regardless of UI culture.

                    switch (resultKind)
                    {
                        case EnvVarValidationResultKind.MissingEquals:
                            throw new FormatException(string.Format(Strings.Msg_EnvironmentVariableMissingEquals, recordPosition));

                        case EnvVarValidationResultKind.EmptyKey:
                            throw new FormatException(string.Format(Strings.Msg_EnvironmentVariableKeyEmpty, recordPosition));

                        case EnvVarValidationResultKind.ForbiddenNewline:
                            throw new FormatException(string.Format(Strings.Msg_EnvironmentVariableForbiddenNewline, key));

                        case EnvVarValidationResultKind.GeneralFailure:
                        default:
                            throw new FormatException(errorMessage);
                    }
                }

                result.Add(new EnvironmentVariable { Name = key, Value = value });
            }

            return result;
        }
    }
}
