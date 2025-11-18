using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Provides validation methods for environment variables strings with escaping support.
    /// </summary>
    public static class EnvironmentVariablesValidator
    {
        /// <summary>
        /// Validates the format of the environment variables input.
        /// Supports variables separated by unescaped semicolons or new lines.
        /// Checks that each variable contains exactly one unescaped '=' character,
        /// and that the variable key (before '=') is not empty.
        /// </summary>
        /// <param name="environmentVariables">The raw environment variables string to validate.</param>
        /// <param name="errorMessage">
        /// When validation fails, contains the error message describing the issue;
        /// otherwise, an empty string.
        /// </param>
        /// <returns>
        /// <c>true</c> if the input is valid; otherwise, <c>false</c>.
        /// </returns>
        public static bool Validate(string environmentVariables, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(environmentVariables))
            {
                // No error if empty
                return true;
            }

            // Split input by unescaped semicolons and newlines
            var variables = SplitByUnescapedDelimiters(environmentVariables, new char[] { ';', '\r', '\n' });

            foreach (var variable in variables)
            {
                // Skip empty segments (possible if input ends with delimiter)
                if (string.IsNullOrWhiteSpace(variable))
                    continue;

                // Count unescaped '=' in variable
                int unescapedEqualsCount = CountUnescapedChar(variable, '=');

                if (unescapedEqualsCount != 1)
                {
                    errorMessage = "Each variable must contain exactly one unescaped '=' character.";
                    return false;
                }

                // Find index of first unescaped '='
                int idx = IndexOfUnescapedChar(variable, '=');

                // Extract key and trim
                string key = variable.Substring(0, idx).Trim();

                if (string.IsNullOrEmpty(key))
                {
                    errorMessage = "Environment variable key cannot be empty.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Splits the input string by any of the specified delimiters,
        /// but only when the delimiter is not escaped by an odd number of backslashes.
        /// </summary>
        /// <param name="input">The input string to split.</param>
        /// <param name="delimiters">An array of delimiter characters to split on.</param>
        /// <returns>
        /// An array of string segments resulting from splitting the input by unescaped delimiters.
        /// </returns>
        private static string[] SplitByUnescapedDelimiters(string input, char[] delimiters)
        {
            var segments = new List<string>();
            var sb = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (delimiters.Contains(c))
                {
                    // Count backslashes immediately before this delimiter
                    int backslashCount = 0;
                    int j = i - 1;
                    while (j >= 0 && input[j] == '\\')
                    {
                        backslashCount++;
                        j--;
                    }

                    // If even number of backslashes -> delimiter is unescaped
                    if (backslashCount % 2 == 0)
                    {
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
        /// Counts occurrences of a character that are not escaped by an odd number of preceding backslashes.
        /// </summary>
        /// <param name="str">Input string to check.</param>
        /// <param name="ch">Character to count.</param>
        /// <returns>The number of unescaped occurrences of <paramref name="ch"/>.</returns>
        private static int CountUnescapedChar(string str, char ch)
        {
            int count = 0;

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == ch)
                {
                    // Count how many backslashes immediately precede this character
                    int backslashCount = 0;
                    int j = i - 1;
                    while (j >= 0 && str[j] == '\\')
                    {
                        backslashCount++;
                        j--;
                    }

                    // If even number of backslashes, this char is unescaped
                    if (backslashCount % 2 == 0)
                    {
                        count++;
                    }
                    // else escaped, do not count
                }
            }

            return count;
        }

        /// <summary>
        /// Finds the index of the first unescaped occurrence of a character in a string.
        /// </summary>
        /// <param name="str">Input string to search.</param>
        /// <param name="ch">Character to find.</param>
        /// <returns>
        /// The zero-based index of the first unescaped occurrence of <paramref name="ch"/>,
        /// or -1 if none found.
        /// </returns>
        public static int IndexOfUnescapedChar(string str, char ch)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == ch)
                {
                    // Count how many backslashes immediately precede this character
                    int backslashCount = 0;
                    int j = i - 1;
                    while (j >= 0 && str[j] == '\\')
                    {
                        backslashCount++;
                        j--;
                    }

                    // If even number of backslashes -> char is unescaped
                    if (backslashCount % 2 == 0)
                    {
                        return i;
                    }
                    // else char is escaped, skip it
                }
            }

            return -1;
        }
    }
}
