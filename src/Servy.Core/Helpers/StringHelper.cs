using Servy.Core.EnvironmentVariables;
using System;
using System.Linq;
using System.Text;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for normalizing strings, formatting environment variables,
    /// and formatting service dependencies for display or storage.
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// Normalizes line breaks in a string by replacing CR, LF, or CRLF with semicolons.
        /// Returns an empty string if the input is null.
        /// </summary>
        /// <param name="str">The input string to normalize.</param>
        /// <returns>A string with all line breaks replaced by semicolons.</returns>
        public static string NormalizeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            // Replace line breaks with semicolons
            string normalized = str
                .Replace("\r\n", ";")
                .Replace("\n", ";")
                .Replace("\r", ";");

            //if (normalized.Contains("="))
            //{
            //    var x = EnvironmentVariableParser.Parse(normalized);
            //}

            return normalized;
        }

        /// <summary>
        /// Parses and formats environment variables, one per line.
        /// </summary>
        /// <param name="vars">The raw environment variables string.</param>
        /// <returns>A string where each environment variable is on a separate line.</returns>
        public static string FormatEnvirnomentVariables(string vars)
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
