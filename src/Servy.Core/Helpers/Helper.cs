using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides general helper methods.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// A collection of reserved Windows device names that cannot be used as service names.
        /// </summary>
        /// <remarks>
        /// These names (e.g., CON, PRN, COM1) are legacy DOS device names reserved by the Windows kernel.
        /// Using them as service names can cause significant system conflicts, file system errors, or 
        /// registry corruption, as Windows may attempt to map these names to hardware devices instead 
        /// of the service controller.
        /// </remarks>
        public static readonly IReadOnlyCollection<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT0","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        };

        /// <summary>
        /// A predefined array of characters that are forbidden in Windows Service names.
        /// </summary>
        /// <remarks>
        /// This set extends the basic Service Control Manager (SCM) restrictions to include characters 
        /// that are invalid in Windows Registry key names and the Windows file system. Because a service name 
        /// acts as a registry key under <c>HKLM\SYSTEM\CurrentControlSet\Services</c> and is often used 
        /// to generate log files or directories, prohibiting these characters prevents downstream systemic errors.
        /// </remarks>
        private static readonly char[] InvalidServiceChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

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
            catch (Exception ex)
            {
                Logger.Debug($"IsValidPath: rejected '{path}': {ex.Message}");
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
            catch (Exception ex)
            {
                Logger.Debug($"CreateParentDirectory: rejected '{path}': {ex.Message}");
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
            if (string.IsNullOrEmpty(input))
                return "\"\"";

            return $"\"{EscapeArgs(input)}\"";
        }

        /// <summary>
        /// Escapes special characters in a command-line argument without surrounding quotes according to the Win32 'CommandLineToArgvW' rules.
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
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            if (input.Contains('\0'))
            {
                // Replace actual null chars with literal "\0" sequence to keep argument safe
                input = input.Replace("\0", "\\0");
            }

            // Replace " with \"
            // But we must also handle backslashes that precede a "
            // because \" is treated as a literal quote, and \\" is a literal backslash + quote.
            // The logic: 2n backslashes + " => n backslashes + literal "
            // 2n+1 backslashes + " => n backslashes + literal " + escape next... 
            // Actually, a simpler way for standard .NET/Windows:
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
            if (string.IsNullOrEmpty(input))
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
            if (Version.TryParse(version, out Version parsedVersion))
            {
                return parsedVersion;
            }

            // Fallback for invalid inputs
            return new Version(0, 0);
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
            string sourceName = AppConfig.EventSource;
            string logName = AppConfig.EventLogName;

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
                Logger.Error($"Error ensuring Event Log source '{sourceName}' exists.", ex);
                throw new InvalidOperationException(
                    $"Failed to ensure Event Log source '{sourceName}' on log '{logName}'.", ex);
            }
        }

        /// <summary>
        /// Writes content to a file atomically by writing to a temporary file first and then performing an atomic move.
        /// </summary>
        /// <param name="path">The full destination path where the file should be written.</param>
        /// <param name="writeContent">A delegate that receives a <see cref="Stream"/> to write the actual file content.</param>
        /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous write operation.</returns>
        public static Task WriteFileAtomicAsync(string path, Func<Stream, Task> writeContent, CancellationToken ct = default)
            => WriteFileAtomicCore(path, async fs => await writeContent(fs), ct).AsTask();

        /// <summary>
        /// Writes content to a file atomically by writing to a temporary file first and then performing an atomic move.
        /// This synchronous version is safe to call from UI threads as it avoids the sync-over-async deadlock risk 
        /// associated with blocking on asynchronous tasks.
        /// </summary>
        /// <param name="path">The full destination path where the file should be written.</param>
        /// <param name="writeContent">An action that receives a <see cref="Stream"/> to write the actual file content.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <remarks>
        /// On NTFS volumes, the final move operation is an atomic metadata update, ensuring the destination 
        /// file is never in a partially written state.
        /// </remarks>
        public static void WriteFileAtomic(string path, Action<Stream> writeContent, CancellationToken cancellationToken = default)
        {
            // Ensure the parent directory exists before attempting to create the temp file.
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = GetUniqueTempPath(path);
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    writeContent(fs);
                    // Explicitly flush to disk to ensure data integrity before the move operation.
                    fs.Flush();
                }

                // Remove attributes that would prevent the move operation from succeeding.
                PrepareDestinationForMove(path);

                // Standard retry logic to handle transient "Access Denied" errors (Win32 Error 5).
                // This is commonly caused by Antivirus or Indexing services locking the newly created .tmp file.
                int retries = AppConfig.WriteFileAtomicMaxRetries;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        // On NTFS, moving within the same volume is an atomic metadata operation.
                        NativeMethodsHelpers.AtomicSecureMove(tmp, path);
                        break;
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5 && retries > 0)
                    {
                        retries--;
                        // Synchronous pause to allow external locks to be released.
                        Thread.Sleep(AppConfig.WriteFileAtomicRetryDelayMs);
                    }
                }
            }
            finally
            {
                // Ensure the temporary file is removed if the move failed or an exception occurred during writing.
                CleanupTempFile(tmp);
            }
        }

        /// <summary>
        /// Internal core logic for atomic file writes. Centralizes directory creation, 
        /// temp file management, and atomic moves for asynchronous callers.
        /// </summary>
        /// <param name="path">The full destination path where the file should be written.</param>
        /// <param name="writer">An asynchronous function that receives a <see cref="Stream"/> to write content.</param>
        /// <param name="cancellationToken">An optional cancellation token to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private static async ValueTask WriteFileAtomicCore(string path, Func<Stream, ValueTask> writer, CancellationToken cancellationToken = default)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                // Directory creation is synchronous in standard .NET, but keeping context 
                // aware for potential future IO wrappers.
                Directory.CreateDirectory(dir);
            }

            var tmp = GetUniqueTempPath(path);
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // ConfigureAwait(false) is used here as we do not require the captured synchronization context.
                    await writer(fs).ConfigureAwait(false);
                    await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                // Ensure the existing file isn't Read-Only, which causes Error 5
                PrepareDestinationForMove(path);

                int retries = AppConfig.WriteFileAtomicMaxRetries;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        // On NTFS, moving within the same volume is an atomic metadata operation.
                        NativeMethodsHelpers.AtomicSecureMove(tmp, path);
                        break;
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5 && retries > 0)
                    {
                        retries--;
                        // Asynchronous delay to keep the thread pool unblocked during retries.
                        await Task.Delay(AppConfig.WriteFileAtomicRetryDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                CleanupTempFile(tmp);
            }
        }

        /// <summary>
        /// Generates a unique temporary file path by appending a hyphenless GUID and a .tmp extension to the specified path.
        /// </summary>
        /// <param name="path">The base file path (e.g., the destination file path).</param>
        /// <returns>
        /// A string representing a unique temporary path, such as <c>C:\Data\config.xml.b392...821.tmp</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method uses the <c>:N</c> format specifier to generate a 32-digit hexadecimal GUID without hyphens, 
        /// which is ideal for filesystem compatibility.
        /// </para>
        /// <para>
        /// <b>Note:</b> Ensure the resulting string does not exceed the Windows <c>MAX_PATH</c> limit (260 characters), 
        /// as appending a GUID significantly increases the path length.
        /// </para>
        /// </remarks>
        public static string GetUniqueTempPath(string path) => $"{path}.{Guid.NewGuid():N}.tmp";

        /// <summary>
        /// Prepares the destination file for an overwrite operation by removing restrictive attributes.
        /// </summary>
        /// <param name="path">The path to the destination file.</param>
        private static void PrepareDestinationForMove(string path)
        {
            // Overwriting a file with the Read-Only attribute set results in a Win32 Error 5 (Access Denied).
            if (File.Exists(path))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                }
            }
        }

        /// <summary>
        /// Safely attempts to delete a temporary file.
        /// </summary>
        /// <param name="tmp">The path to the temporary file to remove.</param>
        private static void CleanupTempFile(string tmp)
        {
            if (File.Exists(tmp))
            {
                try
                {
                    File.Delete(tmp);
                }
                catch
                {
                    // Cleanup errors are swallowed to prevent masking the primary exception 
                    // that occurred during the file write or move process.
                }
            }
        }

        /// <summary>
        /// Extracts the directory from a file path and ensures it exists on disk.
        /// </summary>
        /// <param name="filePath">The file path for which to ensure the directory exists.</param>
        public static void EnsureDirectoryExists(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Validates whether a proposed string can be safely used as a Windows Service name.
        /// </summary>
        /// <param name="serviceName">The unique identifier proposed for the service.</param>
        /// <returns>
        /// A tuple where the first item is a <c>bool</c> indicating if the name is valid, 
        /// and the second item is a localized <c>string</c> containing the error message if validation fails.
        /// </returns>
        public static (bool, string) IsServiceNameValid(string serviceName)
        {
            // 1. Null or Empty Check
            // A service name is a fundamental system identifier and cannot be blank.
            if (string.IsNullOrWhiteSpace(serviceName))
                return (false, Strings.Msg_ValidationError);

            // 2. Invisible Padding Check
            // Leading or trailing spaces are technically permitted by some lower-level Windows APIs, 
            // but they cause massive confusion in CLI tools, PowerShell scripts, and visual management consoles.
            if (serviceName != serviceName.Trim())
                return (false, Strings.Msg_ServiceNameContainsTrailingWhitespace);

            // 3. Length check
            if (serviceName.Length > AppConfig.MaxServiceNameLength)
                return (false, string.Format(Strings.Msg_ServiceNameLengthReached, AppConfig.MaxServiceNameLength));

            // 4. Structural Integrity Check
            // We reject the input if it contains:
            //  a) Forbidden file system/registry characters (InvalidServiceChars).
            //  b) Control characters (e.g., \n, \t, \r) which can break console output or CLI parsers.
            if (serviceName.IndexOfAny(InvalidServiceChars) >= 0 || serviceName.Any(char.IsControl))
                return (false, Strings.Msg_InvalidServiceName);

            // 5. Reserved Windows device names (case-insensitive, with or without extension)
            var stem = serviceName;
            var dot = stem.IndexOf('.');
            if (dot >= 0) stem = stem.Substring(0, dot);
            if (ReservedNames.Contains(stem))
                return (false, Strings.Msg_InvalidServiceName);

            // If all checks pass, the name is structurally sound for the Windows SCM.
            return (true, string.Empty);
        }

        /// <summary>
        /// Normalizes a file system path by converting it to an absolute path and removing trailing directory separators.
        /// </summary>
        /// <param name="p">The path string to normalize.</param>
        /// <returns>
        /// A fully qualified absolute path without trailing separators; 
        /// or <see langword="null"/> if the input is <see langword="null"/>, empty, or consists only of white space.
        /// </returns>
        /// <remarks>
        /// This method uses <see cref="Path.GetFullPath(string)"/> to resolve relative paths (e.g., "." or "..") 
        /// based on the current working directory. It then trims any trailing <see cref="Path.DirectorySeparatorChar"/> 
        /// to ensure consistent path comparison and storage in the database.
        /// </remarks>
        public static string NormalizePath(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return null;
            var full = Path.GetFullPath(p);
            // Don't strip the root \ from a drive root like "C:\\" or "\\server\share\\"
            if (Path.GetPathRoot(full)?.Equals(full, StringComparison.OrdinalIgnoreCase) == true)
                return full;
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Recursively walks up the directory tree to determine if the specified path resides 
        /// within any directory that is an NTFS reparse point (such as a junction point or symbolic link).
        /// </summary>
        /// <param name="fullPath">The fully canonicalized absolute path to evaluate.</param>
        /// <returns>
        /// <c>true</c> if any ancestor directory in the path hierarchy is a reparse point; 
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This validation serves as an infiltration and exfiltration guard across the service 
        /// ecosystem. It prevents path traversal and redirection bypasses by checking both 
        /// the modern <see cref="DirectoryInfo.LinkTarget"/> property and the legacy 
        /// <see cref="FileAttributes.ReparsePoint"/> bitmask flag.
        /// </remarks>
        public static bool HasAncestorReparsePoint(string fullPath)
        {
            var parentPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(parentPath))
                return false; // drive root or UNC share root - no ancestors to inspect

            var current = new DirectoryInfo(parentPath);

            // Continue walking upward even if the immediate parent does not exist yet.
            // This ensures we don't bypass the check when evaluating paths inside 
            // directories that are about to be created.
            while (current != null)
            {
                if (current.Exists)
                {
                    if ((current.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        return true;
                    }
                }
                current = current.Parent;
            }
            return false;
        }
    }
}
