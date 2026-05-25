using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using Servy.Core.Security;
using System.Text;

namespace Servy.Core.Validators
{
    /// <summary>
    /// A secure token representing a file path that has successfully passed all defense-in-depth security invariants.
    /// The constructor is intentionally hidden to prevent arbitrary instantiation outside of the security gate.
    /// </summary>
    public sealed class ValidatedImportPath
    {
        public string ResolvedPath { get; }

        internal ValidatedImportPath(string resolvedPath)
        {
            ResolvedPath = resolvedPath;
        }
    }

    /// <summary>
    /// Represents the outcome of the path security validation pipeline.
    /// </summary>
    public sealed class PathSecurityResult
    {
        public bool IsValid { get; }
        public ValidatedImportPath? ValidPath { get; }
        public string? ErrorMessage { get; }

        private PathSecurityResult(ValidatedImportPath path)
        {
            IsValid = true;
            ValidPath = path;
        }

        private PathSecurityResult(string error)
        {
            IsValid = false;
            ErrorMessage = error;
        }

        internal static PathSecurityResult Success(string path) => new PathSecurityResult(new ValidatedImportPath(path));
        internal static PathSecurityResult Fail(string error) => new PathSecurityResult(error);
    }

    /// <summary>
    /// Provides shared validation logic for importing configuration files.
    /// </summary>
    public static class ImportGuard
    {
        /// <summary>
        /// Validates that a configuration file exists, stays within a safe size threshold, and returns a structured validation result.
        /// </summary>
        /// <param name="path">The file system path to evaluate.</param>
        /// <param name="maxFileSizeMb">The maximum allowed size in MB.</param>
        /// <param name="sizeLimitFormat">The localized format string for the size error (expects {0} for path).</param>
        /// <returns>A <see cref="PathSecurityResult"/> containing a secure path token on success, or an explicit error message on failure.</returns>
        public static PathSecurityResult ValidatePathAndSize(
            string path,
            int maxFileSizeMb,
            string sizeLimitFormat)
        {
            string fullPath;
            try
            {
                // Canonicalize path and validate characters
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                Logger.Error($"Invalid path provided for file size validation: {path}", ex);
                return PathSecurityResult.Fail(Strings.Msg_InvalidPath);
            }

            var fileInfo = new FileInfo(fullPath);

            // 1. Existence Check
            if (!fileInfo.Exists)
            {
                var errorMsg = string.Format(Strings.Msg_ImportFileNotFound, fullPath);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // 2. Size Guard (Safety threshold)
            if (fileInfo.Length > AppConfig.ToBytes(maxFileSizeMb))
            {
                var errorMsg = string.Format(sizeLimitFormat, fullPath);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Success: Return the sealed token wrapping the evaluated path
            return PathSecurityResult.Success(fullPath);
        }

        /// <summary>
        /// Enforces defense-in-depth security guards against path traversal, UNC bypasses, and unauthorized system reads.
        /// </summary>
        /// <param name="path">The file path to validate.</param>
        /// <returns>A strongly-typed result containing the secure path token on success, or a rejection reason on failure.</returns>
        public static PathSecurityResult ValidatePathSecurity(string path)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                var errorMsg = $"{Strings.Msg_InvalidPath}: {ex.Message}";
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // UNC Path Block (Infiltration Guard)
            bool isUncUri = Uri.TryCreate(fullPath, UriKind.Absolute, out var uri) && uri.IsUnc;
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || isUncUri)
            {
                var errorMsg = Strings.Msg_SecurityUncPathProhibited;
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Mapped Network Drive Guard
            try
            {
                string? root = Path.GetPathRoot(fullPath);
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.DriveType == DriveType.Network)
                    {
                        var errorMsg = Strings.Msg_SecurityNetworkDriveProhibited;
                        Logger.Error(errorMsg);
                        return PathSecurityResult.Fail(errorMsg);
                    }
                }
            }
            catch (ArgumentException)
            {
                /* Invalid or unprobed drive root - fall through to subsequent security blocks */
            }

            // Reparse Point Guard (Directory and File Level)
            if (Helper.HasAncestorReparsePoint(fullPath))
            {
                var errorMsg = Strings.Msg_SecurityDirReparsePointProhibited;
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Guard against file-level symbolic links
            var fileLinkInfo = new FileInfo(fullPath);
            fileLinkInfo.Refresh();
            if (!string.IsNullOrEmpty(fileLinkInfo.LinkTarget) || (fileLinkInfo.Exists && (fileLinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint))
            {
                var errorMsg = Strings.Msg_SecurityFileReparsePointProhibited;
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Reserved Device Name Block (DOS Guard)
            string fileName = Path.GetFileName(fullPath);
            int firstDotIndex = fileName.IndexOf('.');
            string firstSegment = firstDotIndex >= 0 ? fileName.Substring(0, firstDotIndex) : fileName;

            if (ReservedNames.ReservedDeviceNames.Contains(firstSegment))
            {
                var errorMsg = string.Format(Strings.Msg_SecurityReservedDeviceName, firstSegment);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // System Protection Guard
            string[] protectedFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            var violatedFolder = protectedFolders
                .FirstOrDefault(folder => !string.IsNullOrEmpty(folder) &&
                                          fullPath.StartsWith(
                                              folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                                              StringComparison.OrdinalIgnoreCase));

            if (violatedFolder != null)
            {
                var errorMsg = string.Format(Strings.Msg_SecurityProtectedDirectory, violatedFolder);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Extension Validation
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            string[] allowedExtensions = { ".json", ".xml" };

            if (!allowedExtensions.Contains(extension))
            {
                var errorMsg = string.Format(Strings.Msg_SecurityInvalidFileType, extension);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Existence Check
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                var errorMsg = string.Format(Strings.Msg_ImportFileNotFound, fullPath);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Handle Resolution (Final Target Verification)
            try
            {
                using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var safeHandle = fileStream.SafeFileHandle;

                    // Replaced nativeHandle != NativeMethods.INVALID_HANDLE_VALUE
                    if (!safeHandle.IsInvalid)
                    {
                        uint requiredSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, null!, 0, NativeMethods.VOLUME_NAME_DOS);

                        if (requiredSize > 0)
                        {
                            var pathBuilder = new StringBuilder((int)requiredSize);
                            uint resultSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, pathBuilder, requiredSize, NativeMethods.VOLUME_NAME_DOS);

                            if (resultSize > 0)
                            {
                                string finalPathName = pathBuilder.ToString();
                                string normalizedPath = finalPathName;
                                bool unwrappedUnc = false;

                                if (normalizedPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                                {
                                    normalizedPath = @"\\" + normalizedPath.Substring(@"\\?\UNC\".Length);
                                    unwrappedUnc = true;
                                }
                                else if (normalizedPath.StartsWith(@"\\?\", StringComparison.Ordinal))
                                {
                                    normalizedPath = normalizedPath.Substring(4);
                                }

                                bool finalIsUnc = unwrappedUnc || (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var finalUri) && finalUri.IsUnc);

                                if (unwrappedUnc ||
                                    normalizedPath.StartsWith(@"\\", StringComparison.Ordinal) ||
                                    finalIsUnc)
                                {
                                    var errorMsg = Strings.Msg_SecurityResolvedUncDestination;
                                    Logger.Error(errorMsg);
                                    return PathSecurityResult.Fail(errorMsg);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMsg = string.Format(Strings.Msg_SecurityHandleValidationFailed, ex.Message);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(errorMsg);
            }

            // Success: Return the sealed token wrapping the evaluated path
            return PathSecurityResult.Success(fullPath);
        }
    }
}