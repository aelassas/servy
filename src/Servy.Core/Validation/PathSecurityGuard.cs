using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Servy.Core.Validation
{
    /// <summary>
    /// Provides a centralized static security gate used to evaluate, resolve, and sanitize filesystem paths.
    /// Protects the application against structural exploit vectors including symlink loops, UNC boundary escapes, and DOS device vulnerabilities.
    /// </summary>
    public static class PathSecurityGuard
    {
        private const string ExtendedPrefix = @"\\?\";
        private const string ExtendedUncPrefix = @"\\?\UNC\";

        /// <summary>
        /// Audits path metadata without opening file handles or probing/creating directories.
        /// Enforces pure path-based security checks (UNC path blocking, network drive detection, reparse points, reserved device names, protected system directories, and allowed file extensions).
        /// </summary>
        /// <param name="path">The unverified relative or absolute file path to audit.</param>
        /// <param name="mode">The <see cref="FileMode"/> configuration tracking contextual intent (e.g., import vs. export semantics).</param>
        /// <returns>A <see cref="PathSecurityResult"/> indicating whether pure path validation passed or failed.</returns>
        public static PathSecurityResult ValidatePathOnly(string path, FileMode mode)
        {
            bool isImport = mode == FileMode.Open;

            // Helper local function to pick between import-flavored and export-flavored message strings.
            string Pick(string importMsg, string exportMsg) => isImport ? importMsg : exportMsg;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                var errorMsg = $"{Strings.Msg_InvalidPath}: {ex.Message}";
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.InvalidArgument, errorMsg);
            }

            // UNC Path Block (Infiltration / Exfiltration Guard)
            bool isUncUri = Uri.TryCreate(fullPath, UriKind.Absolute, out var uri) && uri.IsUnc;
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || isUncUri)
            {
                var errorMsg = Pick(Strings.Msg_SecurityUncPathProhibited, Strings.Msg_SecurityUncPathExportProhibited);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
            }

            // Mapped Network Drive Guard
            try
            {
                string root = Path.GetPathRoot(fullPath);
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.DriveType == DriveType.Network)
                    {
                        var errorMsg = Pick(Strings.Msg_SecurityNetworkDriveProhibited, Strings.Msg_SecurityNetworkDriveExportProhibited);
                        Logger.Error(errorMsg);
                        return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
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
                var errorMsg = Pick(Strings.Msg_SecurityDirReparsePointProhibited, Strings.Msg_SecurityDirReparsePointExportProhibited);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
            }

            // Guard against file-level symbolic links
            var fileLinkInfo = new FileInfo(fullPath);
            fileLinkInfo.Refresh();
            if (fileLinkInfo.Exists && (fileLinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                var errorMsg = Pick(Strings.Msg_SecurityFileReparsePointProhibited, Strings.Msg_SecurityFileReparsePointExportProhibited);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
            }

            // Reserved Device Name Block (DOS Guard)
            string fileName = Path.GetFileName(fullPath);
            int firstDotIndex = fileName.IndexOf('.');
            string firstSegment = firstDotIndex >= 0 ? fileName.Substring(0, firstDotIndex) : fileName;

            // Strip trailing spaces, periods, and tabs to match Win32's internal behavior
            string normalizedSegment = firstSegment.TrimEnd(' ', '.', '\t');

            if (ReservedNames.ReservedDeviceNames.Contains(normalizedSegment))
            {
                var errorMsg = string.Format(Strings.Msg_SecurityReservedDeviceName, normalizedSegment);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.InvalidArgument, errorMsg);
            }

            // System Protection Guard
            string[] protectedFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            // Centralized Protected Directory Matcher Local Function
            string FindProtectedFolderViolation(string candidate) =>
                protectedFolders.FirstOrDefault(folder =>
                    !string.IsNullOrEmpty(folder) &&
                    candidate.StartsWith(
                        folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase));

            var violatedFolder = FindProtectedFolderViolation(fullPath);

            if (violatedFolder != null)
            {
                var errorMsg = string.Format(Pick(Strings.Msg_SecurityProtectedDirectory, Strings.Msg_SecurityProtectedDirectoryExport), violatedFolder);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
            }

            // Extension Validation
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();

            if (!AppConfig.AllowedConfigFileExtensions.Contains(extension))
            {
                var errorMsg = string.Format(Strings.Msg_SecurityInvalidFileType, extension);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.InvalidArgument, errorMsg);
            }

            // Existence Check (Only required for Input/Import modes)
            if (isImport && !fileLinkInfo.Exists)
            {
                var errorMsg = string.Format(Strings.Msg_ImportFileNotFound, fullPath);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.InvalidArgument, errorMsg);
            }

            return PathSecurityResult.Success(fullPath);
        }

        /// <summary>
        /// Enforces unified validation layers shared across both input and output operations.
        /// Guarantees that any defensive hardening immediately benefits both import and export workflows.
        /// </summary>
        /// <param name="path">The unverified relative or absolute file path to audit.</param>
        /// <param name="mode">The <see cref="FileMode"/> configuration tracking contextual intent (e.g., import vs. export semantics).</param>
        /// <param name="access">The <see cref="FileAccess"/> permissions required for the resolved target stream.</param>
        /// <param name="share">The <see cref="FileShare"/> rule limits mapping concurrent thread constraints.</param>
        /// <param name="stream">When this method returns, contains an opened, active <see cref="FileStream"/> instance pointing to the verified file layout if validation succeeded; otherwise, <c>null</c>. <b>On successful validation, the caller assumes absolute ownership of this instance and is responsible for its disposal.</b></param>
        /// <returns>A <see cref="PathSecurityResult"/> indicating whether the validation pipeline passed or failed, along with outcome tokens.</returns>
        public static PathSecurityResult ValidatePath(string path, FileMode mode, FileAccess access, FileShare share, out FileStream stream)
        {
            stream = null;
            bool isImport = mode == FileMode.Open;

            // Helper local function to pick between import-flavored and export-flavored message strings.
            string Pick(string importMsg, string exportMsg) => isImport ? importMsg : exportMsg;

            // Run pure path-only preflight checks upfront
            var pathOnlyResult = ValidatePathOnly(path, mode);
            if (!pathOnlyResult.IsValid)
            {
                return pathOnlyResult;
            }

            string fullPath = pathOnlyResult.ValidPath.ResolvedPath;
            var fileLinkInfo = new FileInfo(fullPath);

            // Protected directory matcher local function for resolved path check below
            string[] protectedFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            string FindProtectedFolderViolation(string candidate) =>
                protectedFolders.FirstOrDefault(folder =>
                    !string.IsNullOrEmpty(folder) &&
                    candidate.StartsWith(
                        folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase));

            // Handle Resolution (Final Target Verification)
            bool existedBefore = File.Exists(fullPath);
            bool createdByUs = !existedBefore;
            FileStream fileStream = null;
            bool success = false;

            try
            {
                fileStream = new FileStream(fullPath, mode, access, share);
                var safeHandle = fileStream.SafeFileHandle;

                // Note on Native Fail-Closed Guards:
                // The following Win32 P/Invoke handle validation checks (IsInvalid, size probe <= 0,
                // serialization failure, and the surrounding catch block) are defensive native fallbacks.
                // They ensure that kernel-level handle failures or buffer allocation errors fail closed
                // rather than bypassing path checks. They are inherently unreachable via managed FileStream
                // APIs in unit test environments without mocking native P/Invoke calls.
                if (safeHandle.IsInvalid)
                {
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, Strings.Msg_SecurityHandleInvalid);
                }

                uint requiredSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, null, 0, NativeMethods.VOLUME_NAME_DOS);

                // Fail closed if the win32 character size probe returns 0 or exceeds maximum integer string buffer capacity.
                // This prevents resolution errors and uint overflow from silently bypassing target checks.
                if (requiredSize == 0 || requiredSize > int.MaxValue)
                {
                    var errorMsg = Strings.Msg_SecurityHandleSizeProbeFailed;
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

                var pathBuilder = new StringBuilder((int)requiredSize);
                uint resultSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, pathBuilder, requiredSize, NativeMethods.VOLUME_NAME_DOS);

                // Fail closed if the string serialization pass returns 0.
                if (resultSize == 0)
                {
                    var errorMsg = Strings.Msg_SecurityHandleSerializationFailed;
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

                string finalPathName = pathBuilder.ToString();
                string normalizedPath = finalPathName;
                bool unwrappedUnc = false;

                if (normalizedPath.StartsWith(ExtendedUncPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPath = @"\\" + normalizedPath.Substring(ExtendedUncPrefix.Length);
                    unwrappedUnc = true;
                }
                else if (normalizedPath.StartsWith(ExtendedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPath = normalizedPath.Substring(ExtendedPrefix.Length);
                }

                bool finalIsUnc = unwrappedUnc || (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var finalUri) && finalUri.IsUnc);

                if (normalizedPath.StartsWith(@"\\", StringComparison.Ordinal) || finalIsUnc)
                {
                    var errorMsg = Pick(Strings.Msg_SecurityResolvedUncDestination, Strings.Msg_SecurityResolvedUncDestinationExport);
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

                // Re-check protected folders against the RESOLVED native kernel path using unified logic
                var resolvedViolation = FindProtectedFolderViolation(normalizedPath);

                if (resolvedViolation != null)
                {
                    var errorMsg = string.Format(Pick(Strings.Msg_SecurityProtectedDirectory, Strings.Msg_SecurityProtectedDirectoryExport), resolvedViolation);
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

                stream = fileStream;
                fileStream = null; // ownership transferred to caller; don't dispose
                success = true;
                return PathSecurityResult.Success(normalizedPath); // normalizedPath is the kernel-resolved target that the security re-checks above validated
            }
            catch (Exception ex)
            {
                var errorMsg = string.Format(Strings.Msg_SecurityHandleValidationFailed, ex.Message);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
            }
            finally
            {
                fileStream?.Dispose();

                if (!success && createdByUs)
                {
                    try { File.Delete(fullPath); } catch { /* Best-effort cleanup of stub files created by OpenOrCreate */ }
                }
            }
        }
    }
}
