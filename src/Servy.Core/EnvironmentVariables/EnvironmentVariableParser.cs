using System;
using System.Collections.Generic;

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
<<<<<<< HEAD
        /// <exception cref="FormatException">Thrown if any variable is missing an unescaped equals sign, has an empty key, or carries forbidden literal newlines.</exception>
        public static List<EnvironmentVariable> Parse(string input)
=======
        /// <exception cref="FormatException">Thrown for any record that fails
        /// <see cref="EnvironmentVariablesValidator.ProcessAndValidateRecord"/>: a missing unescaped
        /// equals sign, an empty key, a forbidden newline in the key or value, a null terminator or
        /// equals sign in the key, or a null terminator in the value. See
        /// <see cref="EnvVarValidationResultKind"/>.</exception>
        public static List<EnvironmentVariable> Parse(string? input)
>>>>>>> 953384b6c (docs(core): EnvironmentVariablesValidator.cs / EnvironmentVariableParser.cs - list every rule ProcessAndValidateRecord enforces (#5906))
        {
            if (string.IsNullOrEmpty(input))
                return new List<EnvironmentVariable>();

            var result = new List<EnvironmentVariable>();

            // Sync delimiters with the Validator to support multi-line input
            var parts = EscapedTokenizer.SplitByUnescapedDelimiters(input, EscapedTokenizer.EnvVarRecordDelimiters);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (string.IsNullOrWhiteSpace(part))
                    continue;

                // Delegate execution to the centralized validation rules block to maintain perfect logic alignment
                if (!EnvironmentVariablesValidator.ProcessAndValidateRecord(part, out string key, out string value, out string errorMessage, out EnvVarValidationResultKind resultKind))
                {
                    // Map the structured validation result to a specific FormatException without leaking raw record values.
                    // Using the enum (rather than matching localized message text) keeps the
                    // mapping correct regardless of UI culture.
                    int recordPosition = i + 1;

                    switch (resultKind)
                    {
                        case EnvVarValidationResultKind.MissingEquals:
                            throw new FormatException($"Invalid environment variable at position {recordPosition} (no unescaped '=').");

                        case EnvVarValidationResultKind.EmptyKey:
                            throw new FormatException($"Environment variable key cannot be empty (record {recordPosition}).");

                        case EnvVarValidationResultKind.ForbiddenNewline:
                            throw new FormatException($"Environment variable '{key}' contains a forbidden newline character. Multi-line values are not supported.");

                        case EnvVarValidationResultKind.GeneralFailure:
                        default:
                            // Fallback safely surfaces the validator's native message context if an unmapped rule fails
                            throw new FormatException(!string.IsNullOrWhiteSpace(errorMessage)
                                ? errorMessage
                                : $"Environment variable record {recordPosition} failed validation tracking.");
                    }
                }

                result.Add(new EnvironmentVariable { Name = key, Value = value });
            }

            return result;
        }
    }
}
