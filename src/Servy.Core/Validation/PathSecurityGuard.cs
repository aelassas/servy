using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.Resources;
using Microsoft.Win32.SafeHandles;
using System.Security.AccessControl;
using System.Security.Principal;
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
        /// Audits a path before any handle is opened: no <see cref="FileStream"/> is created and nothing is created on disk.
        /// Read-only filesystem metadata is queried where a check requires it (volume type, ancestor reparse points, target existence and attributes).
        /// Covers UNC path blocking, network drive detection, reparse points, reserved device names, protected system directories, and allowed file extensions.
        /// </summary>
        /// <param name="path">The unverified relative or absolute file path to audit.</param>
        /// <param name="mode">The <see cref="FileMode"/> configuration tracking contextual intent (e.g., import vs. export semantics).</param>
        /// <returns>A <see cref="PathSecurityResult"/> indicating whether pre-handle validation passed or failed.</returns>
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
                string? root = Path.GetPathRoot(fullPath);
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
            if (!string.IsNullOrEmpty(fileLinkInfo.LinkTarget) || (fileLinkInfo.Exists && (fileLinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint))
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

            if (ReservedNames.IsReservedDeviceName(firstSegment))
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
            string? FindProtectedFolderViolation(string candidate) =>
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
        /// <param name="share">The <see cref="FileShare"/> level granted to other handles while the returned stream is open.</param>
        /// <param name="stream">When this method returns, contains an opened, active <see cref="FileStream"/> instance pointing to the verified file layout if validation succeeded; otherwise, <c>null</c>. <b>On successful validation, the caller assumes absolute ownership of this instance and is responsible for its disposal.</b></param>
        /// <returns>A <see cref="PathSecurityResult"/> indicating whether the validation pipeline passed or failed, along with outcome tokens.</returns>
        public static PathSecurityResult ValidatePath(string path, FileMode mode, FileAccess access, FileShare share, out FileStream? stream)
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

            string fullPath = pathOnlyResult.ValidPath!.ResolvedPath;
            var fileLinkInfo = new FileInfo(fullPath);

            // Protected directory matcher local function for resolved path check below
            string[] protectedFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            string? FindProtectedFolderViolation(string candidate) =>
                protectedFolders.FirstOrDefault(folder =>
                    !string.IsNullOrEmpty(folder) &&
                    candidate.StartsWith(
                        folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase));

            // Handle Resolution (Final Target Verification)
            bool existedBefore = File.Exists(fullPath);
            bool createdByUs = !existedBefore;
            FileStream? fileStream = null;
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

                if (!TryGetFinalPathByHandle(safeHandle, out string finalPathName))
                {
                    var errorMsg = Strings.Msg_SecurityHandleSizeProbeFailed;
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

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

        /// <summary>
        /// Resolves an open file handle to its final DOS path, failing closed if the path cannot be determined.
        /// </summary>
        /// <param name="handle">The open file handle to resolve.</param>
        /// <param name="finalPath">When this method returns <c>true</c>, the normalized final path for <paramref name="handle"/>.</param>
        /// <returns><c>true</c> when the handle path was resolved; otherwise, <c>false</c>.</returns>
        public static bool TryGetFinalPathByHandle(SafeFileHandle handle, out string finalPath)
        {
            finalPath = string.Empty;

            if (handle == null || handle.IsInvalid || handle.IsClosed)
            {
                return false;
            }

            uint requiredSize = NativeMethods.GetFinalPathNameByHandle(handle, null!, 0, NativeMethods.VOLUME_NAME_DOS);
            if (requiredSize == 0 || requiredSize > int.MaxValue)
            {
                return false;
            }

            var pathBuilder = new StringBuilder((int)requiredSize);
            uint resultSize = NativeMethods.GetFinalPathNameByHandle(handle, pathBuilder, requiredSize, NativeMethods.VOLUME_NAME_DOS);
            if (resultSize == 0 || resultSize >= requiredSize)
            {
                return false;
            }

            string path = pathBuilder.ToString();
            if (path.StartsWith(ExtendedUncPrefix, StringComparison.OrdinalIgnoreCase))
            {
                finalPath = @"\\" + path.Substring(ExtendedUncPrefix.Length);
            }
            else if (path.StartsWith(ExtendedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                finalPath = path.Substring(ExtendedPrefix.Length);
            }
            else
            {
                finalPath = path;
            }

            return !string.IsNullOrEmpty(finalPath);
        }

        /// <summary>
        /// Validates that a target file path resolves strictly within the specified base directory,
        /// rejecting UNC paths, rooted paths pointing outside the base, and reparse points.
        /// </summary>
        /// <param name="targetPath">The path to validate.</param>
        /// <param name="baseDirectory">The trusted application directory.</param>
        /// <returns><c>true</c> if the path is safely contained within the base directory; otherwise, <c>false</c>.</returns>
        public static bool IsSafelyContainedWithinAppDirectory(string targetPath, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(baseDirectory))
            {
                Logger.Warn("Path security validation failed: Target path or base directory is empty.");
                return false;
            }

            try
            {
                // Reject UNC paths immediately
                if (targetPath.StartsWith(@"\\", StringComparison.Ordinal) || targetPath.StartsWith("//", StringComparison.Ordinal))
                {
                    Logger.Warn($"Security refusal: Path '{targetPath}' is a UNC path.");
                    return false;
                }

                string fullBaseDir = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                string fullTargetPath = Helper.IsAbsolute(targetPath)
                    ? Path.GetFullPath(targetPath)
                    : Path.GetFullPath(Path.Combine(fullBaseDir, targetPath));

                // Reject if path traverses outside application directory
                if (!fullTargetPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warn($"Security refusal: Path '{fullTargetPath}' resolves outside application directory '{fullBaseDir}'.");
                    return false;
                }

                // Reject reparse points (junctions/symlinks)
                if (Helper.HasAncestorReparsePoint(fullTargetPath))
                {
                    Logger.Warn($"Security refusal: Path '{fullTargetPath}' traverses a junction or symbolic link.");
                    return false;
                }

                var fileLinkInfo = new FileInfo(fullTargetPath);
                fileLinkInfo.Refresh();
                if (!string.IsNullOrEmpty(fileLinkInfo.LinkTarget) ||
                    (fileLinkInfo.Exists && (fileLinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint))
                {
                    Logger.Warn($"Security refusal: Path '{fullTargetPath}' is a symbolic link.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception during path containment validation for '{targetPath}': {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Inspects the target file or directory ACL to verify standard non-admin users do not possess write or modify rights.
        /// </summary>
        /// <param name="path">The directory or file path to evaluate.</param>
        /// <returns><c>true</c> if the ACL is hardened against standard user modification; otherwise, <c>false</c>.</returns>
        public static bool IsDirectoryAclHardened(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string targetDir = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
                if (!Directory.Exists(targetDir))
                    return true;

                var dirInfo = new DirectoryInfo(targetDir);
                DirectorySecurity security = dirInfo.GetAccessControl(AccessControlSections.Access);
                AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

                var nonAdminSids = new[]
                {
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null) // Everyone
                };
                const FileSystemRights WriteClass =
                    FileSystemRights.WriteData |
                    FileSystemRights.AppendData |
                    FileSystemRights.WriteAttributes |
                    FileSystemRights.WriteExtendedAttributes |
                    FileSystemRights.Delete |
                    FileSystemRights.DeleteSubdirectoriesAndFiles |
                    FileSystemRights.ChangePermissions |
                    FileSystemRights.TakeOwnership;

                foreach (FileSystemAccessRule rule in rules)
                {
                    if (nonAdminSids.Contains(rule.IdentityReference as SecurityIdentifier))
                    {
                        if (rule.AccessControlType == AccessControlType.Allow)
                        {
                            bool hasWriteModify = (rule.FileSystemRights & WriteClass) != 0;
                            if (hasWriteModify)
                            {
                                Logger.Warn($"ACL Security Notice: Directory '{targetDir}' grants Write/Modify access to standard users ({rule.IdentityReference.Value}).");
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn($"ACL security check notice for '{path}': {ex.Message}");
                return true;
            }
        }
    }
}
