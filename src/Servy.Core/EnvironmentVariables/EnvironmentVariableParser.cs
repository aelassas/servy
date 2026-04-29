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
        /// Parses a normalized environment variables string into a list of environment variable objects. Supports escaping of equals signs and semicolons with a backslash, and supports both semicolon and newline delimiters.
        /// </summary>
        /// <param name="input">The normalized environment variables string containing semicolon or newline separators with optional escapes.</param>
        /// <returns>A list of parsed environment variables as instantiated objects.</returns>
        /// <exception cref="FormatException">Thrown if any variable is missing an unescaped equals sign or has an empty key.</exception>
        public static List<EnvironmentVariable> Parse(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<EnvironmentVariable>();

            var result = new List<EnvironmentVariable>();

            // Sync delimiters with the Validator to support multi-line input
            char[] delimiters = new char[] { ';', '\r', '\n' };
            var parts = EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);

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

                // Unescape both key and value
                var key = EscapedTokenizer.Unescape(rawKey).Trim();
                var unescaped = EscapedTokenizer.Unescape(rawValue).Trim();

                // Remove surrounding quotes only if both start and end with a quote
                if (unescaped.Length >= 2 && unescaped[0] == '"' && unescaped[unescaped.Length - 1] == '"')
                {
                    unescaped = unescaped.Substring(1, unescaped.Length - 2);
                }

                var value = unescaped;

                if (string.IsNullOrEmpty(key))
                    throw new FormatException($"Environment variable key cannot be empty: {part}");

                result.Add(new EnvironmentVariable { Name = key, Value = value });
            }

            return result;
        }
    }
}