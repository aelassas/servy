namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Provides validation methods for environment variables strings with escaping support.
    /// </summary>
    public static class EnvironmentVariablesValidator
    {
        /// <summary>
        /// Validates the format of the environment variables input. Supports variables separated by unescaped semicolons or new lines. Checks that each variable contains exactly one unescaped equals character, and that the variable key before the equals sign is not empty.
        /// </summary>
        /// <param name="environmentVariables">The raw environment variables string to validate.</param>
        /// <param name="errorMessage">When validation fails, contains the error message describing the issue; otherwise, an empty string.</param>
        /// <returns>A boolean value indicating true if the input is valid, or false if format violations were detected.</returns>
        public static bool Validate(string? environmentVariables, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(environmentVariables))
            {
                // No error if empty
                return true;
            }

            // Split input by unescaped semicolons and newlines
            var variables = EscapedTokenizer.SplitByUnescapedDelimiters(environmentVariables, new char[] { ';', '\r', '\n' });

            foreach (var variable in variables)
            {
                // Skip empty segments (possible if input ends with delimiter)
                if (string.IsNullOrWhiteSpace(variable))
                    continue;

                // Count unescaped '=' in variable
                int unescapedEqualsCount = EscapedTokenizer.CountUnescapedChar(variable, '=');

                if (unescapedEqualsCount < 1)
                {
                    errorMessage = "Each variable must contain an unescaped '=' character to separate the key from the value.";
                    return false;
                }

                // Find index of first unescaped '='
                int idx = EscapedTokenizer.IndexOfUnescapedChar(variable, '=');

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