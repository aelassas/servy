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
                var errorMsg = mode == FileMode.Open ? Strings.Msg_SecurityUncPathProhibited : Strings.Msg_SecurityUncPathExportProhibited;
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
                        var errorMsg = mode == FileMode.Open ? Strings.Msg_SecurityNetworkDriveProhibited : Strings.Msg_SecurityNetworkDriveExportProhibited;
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
                var errorMsg = mode == FileMode.Open ? Strings.Msg_SecurityDirReparsePointProhibited : Strings.Msg_SecurityDirReparsePointExportProhibited;
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
            }

            // Guard against file-level symbolic links
            var fileLinkInfo = new FileInfo(fullPath);
            fileLinkInfo.Refresh();
            if (fileLinkInfo.Exists && (fileLinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                var errorMsg = mode == FileMode.Open ? Strings.Msg_SecurityFileReparsePointProhibited : Strings.Msg_SecurityFileReparsePointExportProhibited;
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

            var violatedFolder = protectedFolders
                .FirstOrDefault(folder => !string.IsNullOrEmpty(folder) &&
                                          fullPath.StartsWith(
                                              folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                                              StringComparison.OrdinalIgnoreCase));

            if (violatedFolder != null)
            {
                var errorMsg = string.Format(mode == FileMode.Open ? Strings.Msg_SecurityProtectedDirectory : Strings.Msg_SecurityProtectedDirectoryExport, violatedFolder);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
            }

            // Extension Validation
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            string[] allowedExtensions = { ".json", ".xml" };

            if (!allowedExtensions.Contains(extension))
            {
                var errorMsg = string.Format(Strings.Msg_SecurityInvalidFileType, extension);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.InvalidArgument, errorMsg);
            }

            // Existence Check (Only required for Input/Import modes)
            if (mode == FileMode.Open && !fileLinkInfo.Exists)
            {
                var errorMsg = string.Format(Strings.Msg_ImportFileNotFound, fullPath);
                Logger.Error(errorMsg);
                return PathSecurityResult.Fail(PathSecurityFailureKind.InvalidArgument, errorMsg);
            }

            // Handle Resolution (Final Target Verification)
            FileStream fileStream = null;
            try
            {
                fileStream = new FileStream(fullPath, mode, access, share);
                var safeHandle = fileStream.SafeFileHandle;

                if (safeHandle.IsInvalid)
                {
                    fileStream.Dispose();
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, Strings.Msg_SecurityHandleInvalid);
                }

                uint requiredSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, null, 0, NativeMethods.VOLUME_NAME_DOS);

                // Fail closed if the win32 character size probe returns 0. 
                // This prevents resolution errors from silently bypassing target checks.
                if (requiredSize == 0)
                {
                    fileStream.Dispose();
                    var errorMsg = Strings.Msg_SecurityHandleSizeProbeFailed;
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

                var pathBuilder = new StringBuilder((int)requiredSize);
                uint resultSize = NativeMethods.GetFinalPathNameByHandle(safeHandle, pathBuilder, requiredSize, NativeMethods.VOLUME_NAME_DOS);

                // Fail closed if the string serialization pass returns 0.
                if (resultSize == 0)
                {
                    fileStream.Dispose();
                    var errorMsg = Strings.Msg_SecurityHandleSerializationFailed;
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

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

                if (unwrappedUnc || normalizedPath.StartsWith(@"\\", StringComparison.Ordinal) || finalIsUnc)
                {
                    fileStream.Dispose();
                    var errorMsg = mode == FileMode.Open ? Strings.Msg_SecurityResolvedUncDestination : Strings.Msg_SecurityResolvedUncDestinationExport;
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

                // Re-check protected folders against the RESOLVED native kernel path
                var resolvedViolation = protectedFolders.FirstOrDefault(folder =>
                    !string.IsNullOrEmpty(folder) &&
                    normalizedPath.StartsWith(
                        folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase));

                if (resolvedViolation != null)
                {
                    fileStream.Dispose();
                    var errorMsg = string.Format(mode == FileMode.Open ? Strings.Msg_SecurityProtectedDirectory : Strings.Msg_SecurityProtectedDirectoryExport, resolvedViolation);
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.Security, errorMsg);
                }

                stream = fileStream;
                fileStream = null; // ownership transferred to caller; don't dispose
                return PathSecurityResult.Success(fullPath);
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
            }
        }
    }
}