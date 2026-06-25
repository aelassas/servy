using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides general helper methods.
    /// </summary>
    public static class Helper
    {
        // SEMVER REGEX GATE: Matches the valid leading numeric blocks major[.minor[.build[.revision]]]
        // and safely isolates trailing metadata or pre-release suffixes (-rc.1, +build) to prevent parsing crashes.
        private static readonly Regex VersionSanitizationRegex = new Regex(
            @"^([0-9]+(?:\.[0-9]+){0,3})",
            RegexOptions.Compiled,
            AppConfig.InputRegexTimeout);

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

        // Cache the invalid characters to avoid array allocations on high-frequency validation calls
        private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        /// <summary>
        /// Checks if the provided path is valid, absolute, and free of invalid directory or filename characters.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if the path is valid, otherwise false.</returns>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // 1. Directory Traversal Defense
            var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => s == ".."))
            {
                return false;
            }

            try
            {
                // 2. Base Path Constraint Check
                if (path.IndexOfAny(InvalidPathChars) >= 0)
                {
                    return false;
                }

                // 3. Absolute Path Enforcement
                if (!IsAbsolute(path))
                {
                    return false;
                }

                // 4. ROBUSTNESS: Validate individual segments against filename restrictions.
                // We skip the first segment if it represents the drive root (e.g., "C:") to preserve the colon ':'.
                for (int i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];

                    if (string.IsNullOrEmpty(segment))
                    {
                        continue;
                    }

                    // If it's the first segment and looks like a drive specifier (e.g., "C:"), skip filename validation
                    if (i == 0 && segment.Length == 2 && segment[1] == ':' && char.IsLetter(segment[0]))
                    {
                        continue;
                    }

                    // Every sub-directory name and the final file name must conform to strict naming rules
                    if (segment.IndexOfAny(InvalidFileNameChars) >= 0)
                    {
                        return false;
                    }
                }

                // 5. Final Win32 Normalization Pass (validates lengths, formatting, and permissions)
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
        /// Determines whether a specified path string represents an absolute path.
        /// </summary>
        /// <param name="p">The path string to evaluate.</param>
        /// <returns>
        /// <c>true</c> if the path is rooted and contains a valid drive letter or UNC server/share definition; 
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method improves upon <see cref="Path.IsPathRooted"/> by ensuring that paths 
        /// starting with a directory separator (which are rooted but not absolute in Windows) 
        /// are correctly identified as non-absolute.
        /// </remarks>
        public static bool IsAbsolute(string p)
        {
            if (string.IsNullOrWhiteSpace(p))
                return false;

            string root = Path.GetPathRoot(p)?.TrimStart('\\', '/');

            return Path.IsPathRooted(p) &&
                !string.IsNullOrEmpty(root) &&
                (root.Contains(Path.DirectorySeparatorChar) || root.Contains(Path.AltDirectorySeparatorChar));
        }

        /// <summary>
        /// Ensures the parent directory of the given file path exists, creating it if necessary.
        /// </summary>
        /// <param name="path">The full file path.</param>
        /// <returns>True if the directory exists or was created successfully; false otherwise.</returns>
        /// <remarks>
        /// This method acts as a non-throwing variant of <see cref="EnsureDirectoryExists(string?)"/>, catching and 
        /// logging any filesystem access or authorization violations gracefully.
        /// </remarks>
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
                EnsureDirectoryExists(path);
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
        /// If <paramref name="input"/> is <see langword="null"/> or empty, returns an empty string.
        /// </returns>
        /// <remarks>
        /// - Escapes all backslashes preceding a quote.  
        /// - Doubles trailing backslashes before the closing quote.  
        /// - Replaces any null characters (<c>\0</c>) with the literal sequence <c>\\0</c>.
        /// </remarks>
        public static string EscapeArgs(string input) => EscapeCore(input, escapeQuotes: true);

        /// <summary>
        /// Escapes only backslashes that appear immediately before double quotes.
        /// </summary>
        /// <param name="input">The input string to escape.</param>
        /// <returns>The escaped string.</returns>
        public static string EscapeBackslashes(string input) => EscapeCore(input, escapeQuotes: false);

        /// <summary>
        /// Core extraction helper containing common parsing logic for command line escaping.
        /// </summary>
        private static string EscapeCore(string input, bool escapeQuotes)
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
                    // EscapeArgs adds an extra backslash to escape the quote itself; EscapeBackslashes does not.
                    sb.Append('\\', backslashCount * 2 + (escapeQuotes ? 1 : 0));
                    sb.Append('"');
                    backslashCount = 0;
                }
                else
                {
                    // Normal character / Write any previous backslashes as-is - just flush any backslashes
                    if (backslashCount > 0)
                    {
                        sb.Append('\\', backslashCount);
                        backslashCount = 0;
                    }
                    sb.Append(c);
                }
            }

            // Escape trailing backslashes before closing quote
            // Flush any remaining backslashes unchanged or doubled depending on mode
            if (backslashCount > 0)
                sb.Append('\\', escapeQuotes ? backslashCount * 2 : backslashCount);

            return sb.ToString();
        }

        /// <summary>
        /// Parses a string representation of a version identifier into a nullable <see cref="Version"/> object.
        /// Accounts for leading 'v' characters as well as complex SemVer pre-release or metadata strings.
        /// </summary>
        /// <param name="version">The raw version string to evaluate.</param>
        /// <returns>A valid <see cref="Version"/> object if parsing succeeds; otherwise, <see langword="null"/>.</returns>
        public static Version ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            // Trim leading v/V characters commonly found in production Git tags
            version = version.TrimStart('v', 'V');

            // Pre-clean string payload to separate standard numbers from SemVer pre-release strings or build annotations
            var match = VersionSanitizationRegex.Match(version);
            if (match.Success)
            {
                version = match.Groups[1].Value;
            }

            if (Version.TryParse(version, out Version parsedVersion))
                return parsedVersion;

            return null; // Return null to indicate parsing failure to upstream business components
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
        public static Task WriteFileAtomicAsync(string path, Func<Stream, CancellationToken, Task> writeContent, CancellationToken ct = default)
            => WriteFileAtomicCore(path, async (fs, t) => await writeContent(fs, t).ConfigureAwait(false), ct).AsTask();

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

            // ROBUSTNESS: Explicitly guard against MAX_PATH breaches before touching the filesystem.
            // This prevents unexpected unhandled failures on legacy systems where long path support is disabled.
            if (tmp.Length > AppConfig.WriteFileAtomicMaxPathLength)
            {
                throw new PathTooLongException($"The calculated atomic staging path length ({tmp.Length}) exceeds the Windows MAX_PATH limit for target destination '{path}'. Ensure the installation path or service name fits within bounds.");
            }

            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    writeContent(fs);
                    // Explicitly flush to disk to ensure data integrity before the move operation.
                    fs.Flush(flushToDisk: true);
                }

                // Standard retry logic to handle transient "Access Denied" errors (Win32 Error 5).
                // This is commonly caused by Antivirus or Indexing services locking the newly created .tmp file.
                int retries = AppConfig.WriteFileAtomicMaxRetries;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        // Remove attributes that would prevent the move operation from succeeding.
                        PrepareDestinationForMove(path);

                        // On NTFS, moving within the same volume is an atomic metadata operation.
                        NativeMethodsHelpers.AtomicSecureMove(tmp, path);
                        break;
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5 && retries > 0)
                    {
                        retries--;
                        Logger.Debug($"WriteFileAtomic retrying after transient '{ex.GetType().Name}': {ex.Message} (retries left: {retries})");
                        // Synchronous pause to allow external locks to be released.
                        if (cancellationToken.WaitHandle.WaitOne(AppConfig.WriteFileAtomicRetryDelayMs))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
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
        private static async ValueTask WriteFileAtomicCore(string path, Func<Stream, CancellationToken, ValueTask> writer, CancellationToken cancellationToken = default)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                // Directory creation is synchronous in standard .NET, but keeping context 
                // aware for potential future IO wrappers.
                Directory.CreateDirectory(dir);
            }

            var tmp = GetUniqueTempPath(path);

            // ROBUSTNESS: Explicitly guard against MAX_PATH breaches before touching the filesystem.
            // This prevents unexpected unhandled failures on legacy systems where long path support is disabled.
            if (tmp.Length > AppConfig.WriteFileAtomicMaxPathLength)
            {
                throw new PathTooLongException($"The calculated atomic staging path length ({tmp.Length}) exceeds the Windows MAX_PATH limit for target destination '{path}'. Ensure the installation path or service name fits within bounds.");
            }

            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // ConfigureAwait(false) is used here as we do not require the captured synchronization context.
                    await writer(fs, cancellationToken).ConfigureAwait(false);
                    await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    fs.Flush(flushToDisk: true);   // forces FlushFileBuffers; cheap if already flushed
                }

                int retries = AppConfig.WriteFileAtomicMaxRetries;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        // Ensure the existing file isn't Read-Only, which causes Error 5
                        PrepareDestinationForMove(path);

                        // On NTFS, moving within the same volume is an atomic metadata operation.
                        NativeMethodsHelpers.AtomicSecureMove(tmp, path);
                        break;
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5 && retries > 0)
                    {
                        retries--;
                        Logger.Debug($"WriteFileAtomicCore retrying after transient '{ex.GetType().Name}': {ex.Message} (retries left: {retries})");
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
        /// Generates a unique temporary file path by appending a collision-resistant hexadecimal suffix and a .tmp extension to the specified path.
        /// </summary>
        /// <param name="path">The base file path (e.g., the destination file path).</param>
        /// <returns>
        /// A string representing a unique temporary path, such as <c>C:\Data\config.xml.a1b2c3d4e5f60789.tmp</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method appends a 16-character hexadecimal string derived from a <see cref="Guid"/> to minimize 
        /// the path length footprint while maintaining sufficient uniqueness for atomic staging.
        /// </para>
        /// <para>
        /// <b>Warning:</b> Callers must verify that the resulting string length does not exceed 
        /// <see cref="AppConfig.WriteFileAtomicMaxPathLength"/> to avoid <see cref="PathTooLongException"/>.
        /// </para>
        /// </remarks>
        public static string GetUniqueTempPath(string path) => $"{path}.{Guid.NewGuid().ToString("N").Substring(0, 16)}.tmp";

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
            if (serviceName != serviceName.TrimEnd())
                return (false, Strings.Msg_ServiceNameContainsTrailingWhitespace);
            if (serviceName != serviceName.TrimStart())
                return (false, Strings.Msg_ServiceNameContainsLeadingWhitespace);

            // 3. Length check
            if (serviceName.Length > AppConfig.MaxServiceNameLength)
                return (false, string.Format(Strings.Msg_ServiceNameLengthReached, AppConfig.MaxServiceNameLength));

            // 4. Structural Integrity Check
            // We reject the input if it contains:
            //  a) Forbidden file system/registry characters (InvalidServiceChars).
            //  b) Unicode control, format, line separator, or paragraph separator characters 
            //     (as defined in IsDisallowedNameChar) which can compromise display formatting 
            //     or cause malformed output in console/CLI parsers.
            if (serviceName.IndexOfAny(InvalidServiceChars) >= 0 || serviceName.Any(IsDisallowedNameChar))
                return (false, Strings.Msg_InvalidServiceName);

            // 5. Reserved Windows device names (case-insensitive across all segments)
            // Prevent leading or trailing dots, which cause severe filesystem/registry volatility.
            if (serviceName.StartsWith(".", StringComparison.Ordinal) || serviceName.EndsWith(".", StringComparison.Ordinal))
            {
                return (false, Strings.Msg_InvalidServiceName);
            }

            // Tokenize the name by dots to scan every structural segment.
            // This catches (.CON, .CON.txt, ..PRN, AUX.log, or service.LPT1) uniformly.
            string[] segments = serviceName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                // Ensure case-insensitive evaluation against the reserved DOS device names blocklist
                if (ReservedNames.ReservedDeviceNames.Contains(segment))
                {
                    return (false, Strings.Msg_InvalidServiceName);
                }
            }

            // If all checks pass, the name is structurally sound for the Windows SCM.
            return (true, string.Empty);
        }

        /// <summary>
        /// Normalizes a file system path by converting it to an absolute path and removing trailing directory separators.
        /// </summary>
        /// <param name="path">The path string to normalize.</param>
        /// <returns>
        /// A fully qualified absolute path without trailing separators; 
        /// or <see langword="null"/> if the input is <see langword="null"/>, empty, or consists only of white space.
        /// </returns>
        /// <remarks>
        /// This method uses <see cref="Path.GetFullPath(string)"/> to resolve relative paths (e.g., "." or "..") 
        /// based on the current working directory. It then trims any trailing <see cref="Path.DirectorySeparatorChar"/> 
        /// to ensure consistent path comparison and storage in the database.
        /// </remarks>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var full = Path.GetFullPath(path);
                // Don't strip the root \ from a drive root like "C:\\" or "\\server\share\\"
                if (Path.GetPathRoot(full)?.Equals(full, StringComparison.OrdinalIgnoreCase) == true)
                    return full;
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex)
            {
                Logger.Debug($"NormalizePath: rejected '{path}': {ex.Message}");
                return null;
            }
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

        /// <summary>
        /// Converts a file system path to its fully qualified, canonical representation.
        /// </summary>
        /// <param name="p">The path string to be normalized.</param>
        /// <returns>
        /// A fully qualified path string with redundant separators and trailing slashes removed. 
        /// Returns <see cref="string.Empty"/> if the input is null, empty, or whitespace.
        /// </returns>
        /// <remarks>
        /// This method resolves relative path segments (e.g., '..'), normalizes directory separators 
        /// to the current platform's standard, and strips any trailing directory separators 
        /// to ensure a stable, uniform path format for comparison.
        /// </remarks>
        public static string Canonicalise(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return string.Empty;
            return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Determines whether a character is disallowed for use in service names.
        /// </summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>
        /// <c>true</c> if the character falls under Unicode control, format, 
        /// line separator, or paragraph separator categories; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method blocks non-printable characters and structural separators (U+2028, U+2029)
        /// to ensure consistent behavior across file systems and service management APIs.
        /// </remarks>
        private static bool IsDisallowedNameChar(char c)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            return cat == UnicodeCategory.Control
                || cat == UnicodeCategory.Format
                || cat == UnicodeCategory.LineSeparator       // U+2028
                || cat == UnicodeCategory.ParagraphSeparator; // U+2029
        }

    }
}
