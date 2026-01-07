using Servy.Core.Config;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides general helper methods.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Checks if the provided path is valid.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if the path is valid, otherwise false.</returns>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (path.Contains("..")) // no directory traversal
            {
                return false;
            }

            try
            {
                // Check for invalid characters
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    return false;
                }

                // Check if the path is absolute
                if (!Path.IsPathRooted(path))
                {
                    return false;
                }

                // Try to normalize the path (throws if invalid)
                _ = Path.GetFullPath(path);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures the parent directory of the given file path exists, creating it if necessary.
        /// </summary>
        /// <param name="path">The full file path.</param>
        /// <returns>True if the directory exists or was created successfully; false otherwise.</returns>
        public static bool CreateParentDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return false;
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Quotes and escapes a string for safe use as a Windows process argument.
        /// </summary>
        /// <param name="input">The string to quote. Can be <c>null</c> or empty.</param>
        /// <returns>
        /// A properly quoted string where:
        /// <list type="bullet">
        ///   <item>All double quotes are escaped with a backslash.</item>
        ///   <item>All backslashes preceding a quote or the end of the string are doubled.</item>
        ///   <item>Trailing backslashes are doubled to avoid truncation.</item>
        ///   <item>Any null characters (<c>\0</c>) are replaced with the literal sequence <c>\\0</c> for safety.</item>
        /// </list>
        /// For example, <c>C:\Path\"File</c> becomes <c>"C:\Path\\\"File"</c>.
        /// </returns>
        /// <remarks>
        /// This method ensures that strings passed to <see cref="System.Diagnostics.Process"/>
        /// or Windows services are interpreted correctly by the command-line parser.
        /// </remarks>
        public static string Quote(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "\"\"";

            return $"\"{EscapeArgs(input)}\"";
        }

        /// <summary>
        /// Escapes special characters in a command-line argument without surrounding quotes.
        /// </summary>
        /// <param name="input">The argument string to escape. May be <see langword="null"/> or empty.</param>
        /// <returns>
        /// The escaped string, safe for inclusion inside a quoted command-line argument.
        /// If <paramref name="input"/> is <see langword="null"/> or whitespace, returns an empty string.
        /// </returns>
        /// <remarks>
        /// - Escapes all backslashes preceding a quote.  
        /// - Doubles trailing backslashes before the closing quote.  
        /// - Replaces any null characters (<c>\0</c>) with the literal sequence <c>\\0</c>.
        /// </remarks>
        public static string EscapeArgs(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            if (input.Contains('\0'))
            {
                // Replace actual null chars with literal "\0" sequence to keep argument safe
                input = input.Replace("\0", "\\0");
            }

            var sb = new StringBuilder();

            int backslashCount = 0;
            foreach (char c in input)
            {
                if (c == '\\')
                {
                    backslashCount++;
                }
                else if (c == '"')
                {
                    // Escape all backslashes before a quote
                    sb.Append('\\', backslashCount * 2 + 1);
                    sb.Append('"');
                    backslashCount = 0;
                }
                else
                {
                    // Normal character - just flush any backslashes
                    if (backslashCount > 0)
                    {
                        sb.Append('\\', backslashCount);
                        backslashCount = 0;
                    }
                    sb.Append(c);
                }
            }

            // Escape trailing backslashes before closing quote
            if (backslashCount > 0)
                sb.Append('\\', backslashCount * 2);

            return sb.ToString();
        }

        /// <summary>
        /// Escapes only backslashes that appear immediately before double quotes.
        /// </summary>
        /// <param name="input">The input string to escape.</param>
        /// <returns>The escaped string.</returns>
        public static string EscapeBackslashes(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            if (input.Contains('\0'))
            {
                // Replace actual null chars with literal "\0" sequence to keep argument safe
                input = input.Replace("\0", "\\0");
            }

            var sb = new StringBuilder();
            int backslashCount = 0;

            foreach (char c in input)
            {
                if (c == '\\')
                {
                    backslashCount++;
                }
                else if (c == '"')
                {
                    // Double only the backslashes that come right before a quote
                    sb.Append('\\', backslashCount * 2);
                    sb.Append('"');
                    backslashCount = 0;
                }
                else
                {
                    // Write any previous backslashes as-is
                    if (backslashCount > 0)
                    {
                        sb.Append('\\', backslashCount);
                        backslashCount = 0;
                    }
                    sb.Append(c);
                }
            }

            // Flush any remaining backslashes unchanged
            if (backslashCount > 0)
                sb.Append('\\', backslashCount);

            return sb.ToString();
        }

        /// <summary>
        /// Helper method to convert a version string like "v1.2" or "1.2" into a comparable double (e.g. 1.2).
        /// Only the major and minor parts are considered; any additional segments are ignored.
        /// Major version can be any non-negative integer.
        /// Minor version is a single digit between 1 and 9.
        /// Returns 0 if the input is invalid or cannot be parsed.
        /// </summary>
        /// <param name="version">Version string in the format "v1.2" or "1.2".</param>
        /// <returns>The parsed version as a double, or 0 on failure.</returns>
        public static double ParseVersion(string version)
        {
            version = version.TrimStart('v', 'V');
            var parts = version.Split('.');
            if (parts.Length < 2) return 0;

            var major = parts[0];
            var minor = parts[1];
            return double.TryParse($"{major}.{minor}", NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        /// <summary>
        /// Returns the target framework the assembly was built with, e.g., ".NET Framework 4.8".
        /// </summary>
        /// <param name="assembly">Executing assembly.</param>
        /// <returns>
        /// A friendly string such as ".NET Framework 4.8", or "Unknown" if the metadata is missing.
        /// </returns>
        [ExcludeFromCodeCoverage]
        public static string GetBuiltWithFramework(Assembly assembly = null)
        {
            if (assembly == null)
                assembly = Assembly.GetExecutingAssembly();

            var attr = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            if (attr == null || string.IsNullOrWhiteSpace(attr.FrameworkName))
                return "Unknown";

            return ParseFrameworkName(attr.FrameworkName);
        }

        /// <summary>
        /// Converts a target framework string into a friendly display name.
        /// </summary>
        /// <param name="name">The target framework string, e.g., ".NETFramework,Version=v4.8".</param>
        /// <returns>
        /// A human-readable framework name, such as ".NET Framework 4.8",
        /// or "Unknown" if the input is null, empty, or whitespace.
        /// If the version cannot be determined, returns ".NET Framework unknown".
        /// Otherwise, returns the input string as-is.
        /// </returns>
        internal static string ParseFrameworkName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unknown";

            if (name.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase))
            {
                int vIndex = name.IndexOf("Version=v", StringComparison.OrdinalIgnoreCase);
                if (vIndex >= 0)
                {
                    string version = name.Substring(vIndex + "Version=v".Length);
                    return $".NET Framework {version}";
                }
                return ".NET Framework unknown";
            }

            return name; // fallback
        }

        /// <summary>
        /// Ensures that the Event Log source for the service exists.
        /// If the source does not exist, it is created under the Application log.
        /// </summary>
        /// <remarks>
        /// This must be run with administrator privileges. 
        /// Event Log sources are machine-wide and typically only need to be created once per machine.
        /// </remarks>
        [ExcludeFromCodeCoverage]
        public static void EnsureEventSourceExists()
        {
            string sourceName = AppConfig.ServiceNameEventSource;
            string logName = "Application";

            try
            {
                // Check if the source already exists
                if (!EventLog.SourceExists(sourceName))
                {
                    EventLog.CreateEventSource(sourceName, logName);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to ensure Event Log source '{sourceName}' on log '{logName}'.", ex);
            }
        }

        /// <summary>
        /// Determines whether the current process is running under a unit test framework.
        /// </summary>
        /// <remarks>This method checks for the presence of assemblies commonly used by unit test
        /// frameworks, such as xUnit, to infer whether the code is executing within a test environment. The result may
        /// not be accurate if custom or less common test runners are used.</remarks>
        /// <returns>true if a known unit test runner is detected in the current application domain; otherwise, false.</returns>
        [ExcludeFromCodeCoverage]
        public static bool IsRunningInUnitTest()
        {
            // Checks if common test runners are loaded in the process
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName != null && a.FullName.StartsWith("xunit"));
        }

    }
}
