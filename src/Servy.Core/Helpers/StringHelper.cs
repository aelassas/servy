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
        /// Normalizes multi-line input into a single semicolon-delimited string while preserving 
        /// literal semicolons within the values.
        /// </summary>
        /// <param name="str">The raw multi-line input string to normalize.</param>
        /// <returns>
        /// A single-line string where lines are joined by semicolons, and any pre-existing 
        /// unescaped semicolons within the original lines are escaped with a backslash.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is specifically designed for environment variable input where a single entry 
        /// (like a PATH segment) may contain a literal semicolon. By using a negative lookbehind 
        /// regex <c>(?&lt;!\\);</c>, it ensures that semicolons are only escaped if they haven't 
        /// already been escaped by the user.
        /// </para>
        /// <para>
        /// This prevents the "Double-Escaping" trap that would otherwise break downstream 
        /// tokenization in the <c>EnvironmentVariableParser</c>.
        /// </para>
        /// </remarks>
        public static string NormalizeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            // 1. Split on any line break variant
            var lines = str.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            // 2. Escape only UNESCAPED semicolons within each line
            // Pattern: (?<!\\); matches a ';' not preceded by '\'
            var escapedLines = lines.Select(line =>
                Regex.Replace(line, @"(?<!\\);", @"\;"));

            // 3. Join with a raw semicolon as the record separator
            return string.Join(";", escapedLines);
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
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
