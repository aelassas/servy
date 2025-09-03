using Servy.Core.EnvironmentVariables;
using System;
using System.Linq;
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
        /// Normalizes an input string for use in service configuration values such as 
        /// environment variables or service dependencies.
        /// <list type="bullet">
        ///   <item>
        ///     <description>Replaces CR, LF, or CRLF line breaks with semicolons (';').</description>
        ///   </item>
        ///   <item>
        ///     <description>Doubles every sequence of backslashes that appears immediately 
        ///     before a line break (e.g., <c>\</c> → <c>\\</c>, <c>\\</c> → <c>\\\\</c>), 
        ///     ensuring that trailing backslashes are preserved correctly.</description>
        ///   </item>
        ///   <item>
        ///     <description>Returns an empty string if the input is <c>null</c> or empty.</description>
        ///   </item>
        /// </list>
        /// </summary>
        /// <param name="str">The input string to normalize.</param>
        /// <returns>
        /// A normalized string where line breaks are replaced with semicolons, and backslashes 
        /// immediately before line breaks are doubled to preserve their meaning in environment 
        /// variables, service dependencies, and similar configuration values.
        /// </returns>
        public static string NormalizeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            string doubled = str;

            // Double every run of backslashes immediately before a line break,
            // but only if the string looks like an environment variable assignment
            if (doubled.Contains("="))
            {
                doubled = Regex.Replace(
                    doubled,
                    @"\\+(?=\r\n|\r|\n)",
                    m => new string('\\', m.Value.Length * 2)
                );
            }

            // Replace line breaks with semicolons
            string normalized = doubled
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
        public static string FormatEnvirnomentVariables(string vars)
        {
            var normalizedEnvVars = EnvironmentVariableParser.Parse(vars).Select(v => $"{v.Name}={v.Value}");
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
    }
}
