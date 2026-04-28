using Servy.Core.Config;
using Servy.Core.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
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

            if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s == "..")) // no directory traversal
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
        public static bool CreateParentDirectory(string? path)
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
        public static string Quote(string? input)
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
        public static string EscapeArgs(string? input)
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
        public static string EscapeBackslashes(string? input)
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
        /// Helper method to convert a version string like "v1.2" or "1.2.3" into a comparable System.Version object.
        /// Returns a default Version (0.0) if the input is invalid or cannot be parsed.
        /// </summary>
        /// <param name="version">Version string in the format "v1.2", "1.2", or "1.2.3".</param>
        /// <returns>The parsed System.Version, or 0.0 on failure.</returns>
        public static Version ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new Version(0, 0);

            // Remove leading 'v' or 'V'
            version = version.TrimStart('v', 'V');

            // Version.TryParse requires at least a Major.Minor format to succeed.
            if (Version.TryParse(version, out Version? parsedVersion))
            {
                return parsedVersion;
            }

            // Fallback for invalid inputs
            return new Version(0, 0);
        }

        /// <summary>
        /// Returns the .NET framework Servy was built with,
        /// using the "BuiltWithFramework" assembly metadata attribute.
        /// </summary>
        /// <param name="assembly">Executing assembly.</param>
        /// <returns>
        /// A friendly string such as ".NET 8.0", or "Unknown" if the metadata is missing.
        /// </returns>
        public static string GetBuiltWithFramework(Assembly? assembly = null)
        {
            assembly ??= Assembly.GetExecutingAssembly();

            var attr = assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuiltWithFramework");

            if (attr == null)
                return "Unknown";

            var tfm = attr.Value;

            // TFM can be null
            if (string.IsNullOrWhiteSpace(tfm))
                return "Unknown";

            // Normalize: remove platform suffix (e.g. "net8.0-windows" -> "net8.0")
            var dashIndex = tfm.IndexOf('-');
            if (dashIndex > 0)
                tfm = tfm.Substring(0, dashIndex);

            // Convert "net8.0" to ".NET 8.0"
            if (tfm.StartsWith("net") && tfm.Length > 3)
                return $".NET {tfm.Substring(3)}";

            // Fallback: return raw value
            return tfm;
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
            string sourceName = AppConfig.EventSource;
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
                Logger.Error($"Error ensuring Event Log source '{sourceName}' exists: {ex.Message}", ex);
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

        // Add to src/Servy.Core/Helpers/Helper.cs

        /// <summary>
        /// Writes content to a file atomically by writing to a temporary file first and then performing an atomic move.
        /// </summary>
        /// <param name="path">The full destination path where the file should be written.</param>
        /// <param name="writeContent">A delegate that receives a <see cref="Stream"/> to write the actual file content.</param>
        /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous write operation.</returns>
        /// <remarks>
        /// This method prevents file corruption or partial writes (zero-byte files) by ensuring the target path 
        /// is only updated via an atomic <see cref="File.Move(string, string, bool)"/> after the stream is fully 
        /// flushed to disk. On NTFS, a move on the same volume is an atomic metadata operation.
        /// </remarks>
        public static async Task WriteFileAtomicAsync(string path, Func<Stream, Task> writeContent, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await writeContent(fs);
                    await fs.FlushAsync(ct); // Ensure all bytes hit the disk before closing
                }

                File.Move(tmp, path, overwrite: true);
            }
            finally
            {
                // Cleanup the temporary file if the move failed or an exception occurred during writing
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); } catch { /* swallow cleanup errors to avoid masking primary exceptions */ }
                }
            }
        }

        /// <summary>
        /// Writes content to a file atomically by writing to a temporary file first and then performing an atomic move.
        /// </summary>
        /// <param name="path">The full destination path where the file should be written.</param>
        /// <param name="writeContent">An action that receives a <see cref="Stream"/> to write the actual file content.</param>
        /// <remarks>
        /// This method prevents file corruption or partial writes by ensuring the target path is only updated 
        /// via an atomic move after the stream is successfully flushed. If the write fails, the original file 
        /// at <paramref name="path"/> remains untouched.
        /// </remarks>
        public static void WriteFileAtomic(string path, Action<Stream> writeContent)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    writeContent(fs);
                    fs.Flush(); // Ensure all bytes hit the disk before closing
                }

                File.Move(tmp, path, overwrite: true);
            }
            finally
            {
                // Cleanup the temporary file if the move failed or an exception occurred during writing
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); } catch { /* swallow cleanup errors */ }
                }
            }
        }

        /// <summary>
        /// Extracts the directory from a file path and ensures it exists on disk.
        /// </summary>
        /// <param name="filePath">The file path for which to ensure the directory exists.</param>
        public static void EnsureDirectoryExists(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

    }
}
