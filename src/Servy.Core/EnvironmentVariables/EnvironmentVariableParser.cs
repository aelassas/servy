using System;
using System.Collections.Generic;
using System.Text;

namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Provides methods to parse environment variables strings with escaping support.
    /// </summary>
    public static class EnvironmentVariableParser
    {
        /// <summary>
        /// Parses a normalized environment variables string into a list of <see cref="EnvironmentVariable"/> objects.
        /// Supports escaping of '=' and ';' characters with a backslash.
        /// </summary>
        /// <param name="input">Normalized environment variables string (semicolon-separated, with escapes).</param>
        /// <returns>List of parsed environment variables as <see cref="EnvironmentVariable"/> instances.</returns>
        /// <exception cref="FormatException">Thrown if any variable is missing an unescaped '=' or has an empty key.</exception>
        public static List<EnvironmentVariable> Parse(string input)
        {
            var result = new List<EnvironmentVariable>();

            if (string.IsNullOrEmpty(input))
                return result;

            // Split by unescaped semicolons
            var parts = SplitByUnescapedDelimiter(input, ';');

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                // Find first unescaped '='
                var eqIdx = IndexOfUnescapedChar(part, '=');

                if (eqIdx < 0)
                    throw new FormatException($"Invalid environment variable (no unescaped '='): {part}");

                // Extract raw key and value substrings
                var rawKey = part.Substring(0, eqIdx);
                var rawValue = part.Substring(eqIdx + 1);

                // Unescape both key and value
                var key = Unescape(rawKey).Trim();
                var value = Unescape(rawValue).Trim(' ', '"');

                if (string.IsNullOrEmpty(key))
                    throw new FormatException($"Environment variable key cannot be empty: {part}");

                result.Add(new EnvironmentVariable { Name = key, Value = value });
            }

            return result;
        }

        /// <summary>
        /// Splits a string by a delimiter character, but only when the delimiter is not escaped by a backslash.
        /// The backslash is preserved so later Unescape can handle sequences like \=, \;, and \\ correctly.
        /// </summary>
        /// <param name="input">Input string to split.</param>
        /// <param name="delimiter">Delimiter character to split on.</param>
        /// <returns>Array of split segments.</returns>
        private static string[] SplitByUnescapedDelimiter(string input, char delimiter)
        {
            var segments = new List<string>();
            var sb = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == delimiter)
                {
                    // Count backslashes before this delimiter
                    int backslashCount = 0;
                    int j = i - 1;
                    while (j >= 0 && input[j] == '\\')
                    {
                        backslashCount++;
                        j--;
                    }

                    if (backslashCount % 2 == 0)
                    {
                        // unescaped delimiter → split here
                        segments.Add(sb.ToString());
                        sb.Clear();
                        continue;
                    }
                }

                sb.Append(c);
            }

            segments.Add(sb.ToString());
            return segments.ToArray();
        }

        /// <summary>
        /// Finds the index of the first unescaped occurrence of a character in a string.
        /// </summary>
        /// <param name="str">Input string to search.</param>
        /// <param name="ch">Character to find.</param>
        /// <returns>The zero-based index of the first unescaped character, or -1 if not found.</returns>
        private static int IndexOfUnescapedChar(string str, char ch)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == ch)
                {
                    // Count how many backslashes are immediately before this char
                    int backslashCount = 0;
                    int j = i - 1;
                    while (j >= 0 && str[j] == '\\')
                    {
                        backslashCount++;
                        j--;
                    }

                    // If even number of backslashes before char → char is unescaped
                    // If odd number of backslashes before char → char is escaped
                    if (backslashCount % 2 == 0)
                    {
                        return i; // unescaped char found
                    }
                    // else char is escaped, skip it
                }
            }
            return -1;
        }

        /// <summary>
        /// Unescapes backslash-escaped characters in environment variable strings.
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///     Before <c>=</c> or <c>;</c>: keeps <c>N-1</c> backslashes, then appends the delimiter.  
        ///     (e.g., <c>\=</c> → <c>=</c>, <c>\\=</c> → <c>\=</c>, <c>\\\=</c> → <c>\\=</c>).
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     At the end of the string:  
        ///     - A single backslash (<c>\</c>) is preserved.  
        ///     - Two or more backslashes are collapsed to <c>⌊N/2⌋</c>.  
        ///     (e.g., <c>\</c> → <c>\</c>, <c>\\</c> → <c>\</c>, <c>\\\</c> → <c>\\</c>, <c>\\\\</c> → <c>\\</c>).
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     Elsewhere (not before <c>=</c>, <c>;</c>, or end-of-string), backslashes are preserved literally.
        ///     </description>
        ///   </item>
        /// </list>
        /// </summary>
        /// <param name="input">The input string to unescape.</param>
        /// <returns>The unescaped string with the above rules applied.</returns>
        private static string Unescape(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sb = new StringBuilder();
            int i = 0;

            while (i < input.Length)
            {
                if (input[i] == '\\')
                {
                    int start = i;
                    while (i < input.Length && input[i] == '\\')
                        i++;
                    int count = i - start;

                    char next = i < input.Length ? input[i] : '\0';

                    if (next == '=' || next == ';')
                    {
                        // Rule 1: before '=' or ';'
                        if (count > 1)
                            sb.Append(new string('\\', count - 1));

                        sb.Append(next);
                        i++; // consume delimiter
                    }
                    else if (next == '\0')
                    {
                        // Rule 2: at end-of-string
                        if (count == 1)
                        {
                            sb.Append('\\'); // keep single
                        }
                        else
                        {
                            sb.Append(new string('\\', count / 2));
                        }
                    }
                    else
                    {
                        // Rule 3: preserve literally
                        sb.Append(new string('\\', count));
                    }
                }
                else
                {
                    sb.Append(input[i]);
                    i++;
                }
            }

            return sb.ToString();
        }



    }
}
