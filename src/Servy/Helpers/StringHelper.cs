using Servy.Core.EnvironmentVariables;
using System;
using System.Linq;

namespace Servy.Helpers
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
            string normalizedStr = str?.Replace("\r\n", ";").Replace("\n", ";").Replace("\r", ";") ?? string.Empty;
            return normalizedStr;
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
