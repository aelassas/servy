using System.Text;

namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Provides centralized, internal utility methods for parsing and tokenizing strings containing backslash-escaped characters.
    /// </summary>
    internal static class EscapedTokenizer
    {
        /// <summary>
        /// Defines the standard characters used to separate individual environment variable records.
        /// </summary>
        /// <remarks>
        /// These delimiters include the semicolon (standard for PATH-like variables) as well as
        /// carriage return and newline characters to support multi-line configuration.
        /// </remarks>
        internal static readonly char[] EnvVarRecordDelimiters = { ';', '\r', '\n' };

        /// <summary>
        /// Splits the input string by any of the specified delimiters, but only when the delimiter is not escaped by an odd number of backslashes. The backslash is preserved so later unescaping logic can handle sequences correctly.
        /// </summary>
        /// <param name="input">The input string to split.</param>
        /// <param name="delimiters">An array of delimiter characters to split on.</param>
        /// <returns>An array of string segments resulting from splitting the input by unescaped delimiters.</returns>
        internal static string[] SplitByUnescapedDelimiters(string input, char[] delimiters)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (delimiters == null)
                throw new ArgumentNullException(nameof(delimiters));

            var segments = new List<string>();
            var sb = new StringBuilder();
            int backslashRun = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                bool isEscaped = (backslashRun & 1) == 1;

                if (delimiters.Contains(c))
                {
                    if (!isEscaped)
                    {
                        segments.Add(sb.ToString());
                        sb.Clear();
                        backslashRun = 0; // Delimiter resets the running count
                        continue;
                    }
                }

                sb.Append(c);
                backslashRun = (c == '\\') ? backslashRun + 1 : 0;
            }

            segments.Add(sb.ToString());
            return segments.ToArray();
        }

        /// <summary>
        /// Finds the index of the first unescaped occurrence of a character in a string.
        /// </summary>
        /// <param name="input">The input string to search.</param>
        /// <param name="ch">The character to find.</param>
        /// <returns>The zero-based index of the first unescaped occurrence of the specified character, or negative one if not found.</returns>
        internal static int IndexOfUnescapedChar(string input, char ch)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            int backslashRun = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                bool isEscaped = (backslashRun & 1) == 1;

                if (c == ch)
                {
                    if (!isEscaped)
                    {
                        return i;
                    }
                }

                backslashRun = (c == '\\') ? backslashRun + 1 : 0;
            }

            return -1;
        }

        /// <summary>
        /// Unescapes backslash-escaped characters, converting escaped equals, semicolons, quotes, newlines, and backslashes into their literal equivalents.
        /// </summary>
        /// <param name="input">The input string to unescape.</param>
        /// <returns>The fully unescaped string.</returns>
        internal static string Unescape(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sb = new StringBuilder();
            var escape = false;

            foreach (var c in input)
            {
                if (escape)
                {
                    // NOTE: This checks for literal structural CR/LF bytes (line continuations),
                    // NOT the alphanumeric character strings 'r' or 'n'.
                    if (c == '=' || c == ';' || c == '\\' || c == '"' || c == '\r' || c == '\n')
                        sb.Append(c);
                    else
                    {
                        sb.Append('\\'); // Keep the backslash literal
                        sb.Append(c);
                    }
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else
                {
                    sb.Append(c);
                }
            }

            // If string ends with a backslash, keep it literally
            if (escape)
                sb.Append('\\');

            return sb.ToString();
        }

        /// <summary>
        /// Determines if the character at the specified index is escaped by an odd number of preceding backslashes.
        /// </summary>
        /// <param name="input">The string to examine.</param>
        /// <param name="index">The position of the character to check.</param>
        /// <returns>true if the character is escaped; otherwise, false.</returns>
        internal static bool IsEscapedAt(string input, int index)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            int backslashCount = 0;
            for (int j = index - 1; j >= 0 && j < input.Length && input[j] == '\\'; j--)
            {
                backslashCount++;
            }
            return backslashCount % 2 != 0;
        }
    }
}
