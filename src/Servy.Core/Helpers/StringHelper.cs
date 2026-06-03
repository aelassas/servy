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
        /// Normalizes a multi-line input string into a single-line, semicolon-delimited configuration string.
        /// Handles line endings and preserves trailing backslashes to prevent delimiter escaping.
        /// </summary>
        /// <param name="str">The multi-line input string to be normalized.</param>
        /// <returns>A single-line string with line breaks replaced by semicolons.</returns>
        /// <remarks>
        /// Semicolons within the input string must be manually escaped with a backslash. 
        /// Backslashes that appear immediately before a line break are dynamically evaluated based on parity;
        /// only odd-length sequences are padded to prevent them from inadvertently escaping the semicolon delimiter 
        /// during downstream tokenization.
        /// </remarks>
        public static string NormalizeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            // Perform a parity-aware pass over the string sequence.
            // Counting contiguous backslashes guarantees we only append a padding escape backslash 
            // if the existing backslash run has an odd parity when it intersects a line break or EOF.
            var sb = new StringBuilder(str.Length);
            int run = 0;

            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == '\\')
                {
                    run++;
                    sb.Append(c);
                    continue;
                }

                bool atBreak = c == '\r' || c == '\n';
                if (atBreak && (run & 1) == 1)
                {
                    sb.Append('\\'); // Normalize/neutralize odd-parity run
                }

                sb.Append(c);
                run = 0;
            }

            if ((run & 1) == 1)
            {
                sb.Append('\\'); // EOF fence after an odd-parity run
            }

            // Flatten multi-line input into a single-line configuration mapping
            string normalized = sb.ToString()
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
        /// <returns>A string with each dependency on a separate line; <see cref="string.Empty"/> when <paramref name="deps"/> is null or empty.</returns>
        public static string FormatServiceDependencies(string deps)
        {
            if (string.IsNullOrEmpty(deps)) return string.Empty;
            return deps.Replace(";", Environment.NewLine);
        }

        /// <summary>
        /// Escapes special characters in environment variable keys/values.
        /// </summary>
        /// <remarks>
        /// Newline characters ('\n') and carriage returns ('\r') are strictly forbidden 
        /// in environment variable values. Attempting to parse strings containing these 
        /// characters will result in a <see cref="FormatException"/>.
        /// </remarks>
        public static string Escape(string value)
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