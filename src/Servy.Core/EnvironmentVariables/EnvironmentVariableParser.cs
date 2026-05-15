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
        /// <exception cref="FormatException">Thrown if any variable is missing an unescaped equals sign or has an empty key.</exception>
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

                // Find first unescaped '='
                var eqIdx = EscapedTokenizer.IndexOfUnescapedChar(part, '=');

                if (eqIdx < 0)
                    throw new FormatException($"Invalid environment variable (no unescaped '='): {part}");

                // Extract raw key and value substrings
                var rawKey = part.Substring(0, eqIdx);
                var rawValue = part.Substring(eqIdx + 1);

                var key = EscapedTokenizer.Unescape(rawKey).Trim();

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
                var value = EscapedTokenizer.Unescape(trimmedValue);

                if (string.IsNullOrEmpty(key))
                    throw new FormatException($"Environment variable key cannot be empty: {part}");

                result.Add(new EnvironmentVariable { Name = key, Value = value });
            }

            return result;
        }
    }
}