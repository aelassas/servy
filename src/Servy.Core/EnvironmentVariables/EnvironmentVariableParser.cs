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
        /// (e.g., <c>KEY=\"value\"</c>).
        /// The <c>KEY="\"value\""</c> form is impossible with the current escape rules.
        /// </para>
        /// </remarks>
        /// <param name="input">The normalized environment variables string containing semicolon or newline separators with optional escapes.</param>
        /// <returns>A list of parsed environment variables as instantiated objects.</returns>
        /// <exception cref="FormatException">Thrown if any variable is missing an unescaped equals sign, has an empty key, or carries forbidden literal newlines.</exception>
        public static List<EnvironmentVariable> Parse(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<EnvironmentVariable>();

            var result = new List<EnvironmentVariable>();

            // Sync delimiters with the Validator to support multi-line input
            var parts = EscapedTokenizer.SplitByUnescapedDelimiters(input, EscapedTokenizer.EnvVarRecordDelimiters);

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                // Delegate execution to the centralized validation rules block to maintain perfect logic alignment
                if (!EnvironmentVariablesValidator.ProcessAndValidateRecord(part, out string key, out string value, out string errorMessage, out EnvVarValidationResultKind resultKind))
                {
                    // Map the structured validation result to a specific FormatException.
                    // Using the enum (rather than matching localized message text) keeps the
                    // mapping correct regardless of UI culture.
                    switch (resultKind)
                    {
                        case EnvVarValidationResultKind.MissingEquals:
                            throw new FormatException($"Invalid environment variable (no unescaped '='): {part}");

                        case EnvVarValidationResultKind.EmptyKey:
                            throw new FormatException($"Environment variable key cannot be empty: {part}");

                        case EnvVarValidationResultKind.ForbiddenNewline:
                            throw new FormatException($"Environment variable '{key}' contains a forbidden newline character. Multi-line values are not supported.");

                        case EnvVarValidationResultKind.GeneralFailure:
                        default:
                            // Fallback safely surfaces the validator's native message context if an unmapped rule fails
                            throw new FormatException(!string.IsNullOrWhiteSpace(errorMessage)
                                ? errorMessage
                                : $"Environment variable record failed validation tracking: {part}");
                    }
                }

                result.Add(new EnvironmentVariable { Name = key, Value = value });
            }

            return result;
        }
    }
}