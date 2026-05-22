using Servy.Core.EnvironmentVariables;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for normalizing strings, formatting environment variables,
    /// and formatting service dependencies for display or storage.
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// Normalizes a multi-line input string into a single-line, semicolon-delimited configuration string.
        /// Handles line endings and preserves trailing backslashes to prevent delimiter escaping.
        /// </summary>
        /// <param name="str">The multi-line input string to be normalized.</param>
        /// <returns>A single-line string with line breaks replaced by semicolons.</returns>
        /// <remarks>
        /// Semicolons within the input string must be manually escaped with a backslash. 
        /// Backslashes that appear immediately before a line break are automatically doubled to prevent them from 
        /// inadvertently escaping the semicolon delimiter during downstream tokenization.
        /// </remarks>
        public static string NormalizeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            // ROBUSTNESS: Detect and double any backslash that immediately precedes a line break or the end of the string.
            // This guarantees that the trailing backslash doesn't escape the substituted semicolon record delimiter down the line.
            string normalized = Regex.Replace(str, @"\\(?=\r|\n|$)", @"\\");

            // Replace line breaks with semicolons to flatten the multi-line input
            normalized = normalized
                .Replace("\r\n", ";")
                .Replace("\n", ";")
                .Replace("\r", ";");

            return normalized;
        }

        /// <summary>
        /// Parses and formats environment variables, one per line.
        /// </summary>
        /// <param name="vars">The raw environment variables string.</param>
        /// <returns>A string where each environment variable is on a separate line.</returns>
        public static string FormatEnvironmentVariables(string vars)
        {
            var normalizedEnvVars = EnvironmentVariableParser.Parse(vars)
                .Select(v => $"{Escape(v.Name)}={Escape(v.Value)}");

            return string.Join(Environment.NewLine, normalizedEnvVars);
        }

        /// <summary>
        /// Formats service dependencies by replacing semicolons with newlines.
        /// </summary>
        /// <param name="deps">The semicolon-separated list of service dependencies.</param>
        /// <returns>A string with each dependency on a separate line, or null if input is null.</returns>
        public static string FormatServiceDependencies(string deps)
        {
            return deps?.Replace(";", Environment.NewLine);
        }

        /// <summary>
        /// Escapes special characters in environment variable keys/values.
        /// Hardened to safely process and translate carriage returns and line feeds.
        /// </summary>
        private static string Escape(string value)
        {
            if (value == null)
                return string.Empty;

            var sb = new StringBuilder(value.Length);

            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        sb.Append(@"\\");
                        break;
                    case '=':
                        sb.Append(@"\=");
                        break;
                    case ';':
                        sb.Append(@"\;");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\r':
                        sb.Append('\\'); sb.Append('\r');
                        break;
                    case '\n':
                        sb.Append('\\'); sb.Append('\n');
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
