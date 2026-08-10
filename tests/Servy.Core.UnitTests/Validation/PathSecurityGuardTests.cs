using Servy.Core.Resources;
using Servy.Core.Validation;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Servy.Core.UnitTests.Validation
{
    public class PathSecurityGuardTests : TempDirectoryTestBase
    {
        private static class NativeTestMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName, string lpTargetPath);
        }

        #region Common Security Rules

        [Fact]
        public void ValidatePath_InvalidPathChars_ReturnsFail()
        {
            // Arrange
            string invalidPath = new string(Path.GetInvalidPathChars());

            // Act
            var result = PathSecurityGuard.ValidatePath(invalidPath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Strings.Msg_InvalidPath, result.ErrorMessage);
            Assert.Null(stream);
        }

        [Theory]
        [InlineData(@"\\server\share\config.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData(@"\\127.0.0.1\c$\config.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData(@"\\server\share\export.json", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        public void ValidatePath_UncPath_ReturnsFail(string uncPath, FileMode mode, FileAccess access, FileShare share)
        {
            // Act
            var result = PathSecurityGuard.ValidatePath(uncPath, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(stream);
            var expected = mode == FileMode.Open
                ? Strings.Msg_SecurityUncPathProhibited
                : Strings.Msg_SecurityUncPathExportProhibited;

            Assert.Equal(expected, result.ErrorMessage);
        }

        [Theory]
        [InlineData("CON.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData("PRN.xml", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        [InlineData("COM1.json", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData("LPT1.xml", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        public void ValidatePath_ReservedDeviceName_ReturnsFail(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            // Arrange & Act
            var result = PathSecurityGuard.ValidatePath(fileName, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(stream);

            // Assert the reserved-device guard specifically: the message must name the device it rejected.
            // NOTE: On .NET Framework 4.8, OS path expansion forces reserved device names into the
            // NT namespace format (\\.\CON), causing them to hit the UNC protection gate first.
            bool hitDosGuard = result.ErrorMessage.IndexOf(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase) >= 0;
            bool hitUncGuard = result.ErrorMessage.IndexOf("UNC paths", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               result.ErrorMessage.IndexOf("UNC destination", StringComparison.OrdinalIgnoreCase) >= 0;

            Assert.True(hitDosGuard || hitUncGuard,
                $"Expected DOS device payload to be intercepted by either the DOS guard or the UNC guard. Actual error: {result.ErrorMessage}");
        }

        [Theory]
        [InlineData(FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        public void ValidatePath_ProtectedFolder_ReturnsFail(FileMode mode, FileAccess access, FileShare share)
        {
            // Arrange
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string filePath = Path.Combine(protectedDir, "sys_config.json");

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(protectedDir, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Null(stream);
        }

        [Theory]
        [InlineData("config.txt", FileMode.Open, FileAccess.Read, FileShare.Read)]
        [InlineData("export.exe", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)]
        [InlineData("config.yaml", FileMode.Open, FileAccess.Read, FileShare.Read)]
        public void ValidatePath_InvalidExtension_ReturnsFail(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, mode, access, share, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Path.GetExtension(fileName).ToLowerInvariant(), result.ErrorMessage);
            Assert.Null(stream);
        }

        [Fact]
        public void ValidatePath_Symlink_ReturnsFail()
        {
            // Arrange
            string targetPath = Path.Combine(TempDirectory, "real_target.json");
            string symlinkPath = Path.Combine(TempDirectory, "symlink_target.json");

            File.WriteAllText(targetPath, "{}");

            try
            {
                Testing.Helper.CreateFileSymlink(symlinkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Skip test gracefully if the runner platform environment lacks symlink creation tokens
                return;
            }

            // Act
            var result = PathSecurityGuard.ValidatePath(symlinkPath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(Strings.Msg_SecurityFileReparsePointProhibited, result.ErrorMessage);
            Assert.Null(stream);
        }

        [Fact]
        public void ValidatePath_ResolvedProtectedFolder_ReturnsFail()
        {
            // Arrange: Find an existing .json or .xml file inside %WINDIR% or System32.
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string targetFile = FindAnySystemConfigFile(winDir);

            if (string.IsNullOrEmpty(targetFile))
            {
                // Skip test gracefully if no system config files are available on this runner
                return;
            }

            string targetDir = Path.GetDirectoryName(targetFile);
            string fileName = Path.GetFileName(targetFile);

            char driveLetter = GetUnusedDriveLetter();
            if (driveLetter == '\0')
            {
                return;
            }

            string drivePath = driveLetter + ":";
            if (!NativeTestMethods.DefineDosDevice(0, drivePath, targetDir))
            {
                return;
            }

            try
            {
                string substFilePath = Path.Combine(drivePath + @"\", fileName);

                // Act: Pre-open check sees "Z:\<file>.xml" (bypassing pre-open string check).
                // Handle resolution unrolls "Z:" to C:\Windows\System32\..., catching it in the post-resolution check.
                var result = PathSecurityGuard.ValidatePath(substFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

                // Assert
                Assert.False(result.IsValid);
                Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
                Assert.NotNull(result.ErrorMessage);
                Assert.Contains(winDir, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
                Assert.Null(stream);
            }
            finally
            {
                // Clean up virtual DOS drive mapping (DDD_REMOVE_DEFINITION = 0x2)
                NativeTestMethods.DefineDosDevice(2, drivePath, null);
            }
        }

        [Fact]
        public void ValidatePath_ResolvedProtectedFolderExport_ReturnsFail()
        {
            // Arrange: Find an existing .json or .xml file inside %WINDIR% or System32.
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string targetFile = FindAnySystemConfigFile(winDir);

            if (string.IsNullOrEmpty(targetFile))
            {
                // Skip test gracefully if no system config files are available on this runner
                return;
            }

            string targetDir = Path.GetDirectoryName(targetFile);
            string fileName = Path.GetFileName(targetFile);

            char driveLetter = GetUnusedDriveLetter();
            if (driveLetter == '\0')
            {
                return;
            }

            string drivePath = driveLetter + ":";
            if (!NativeTestMethods.DefineDosDevice(0, drivePath, targetDir))
            {
                return;
            }

            try
            {
                string substFilePath = Path.Combine(drivePath + @"\", fileName);

                // Act: Pass FileMode.OpenOrCreate to test the export path security logic, using FileAccess.Read
                // to avoid OS-level Access Denied exceptions on existing system files.
                var result = PathSecurityGuard.ValidatePath(substFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read, out var stream);

                // Assert
                Assert.False(result.IsValid);
                Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
                Assert.NotNull(result.ErrorMessage);
                Assert.Contains(winDir, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
                Assert.Null(stream);
            }
            finally
            {
                // Clean up virtual DOS drive mapping (DDD_REMOVE_DEFINITION = 0x2)
                NativeTestMethods.DefineDosDevice(2, drivePath, null);
            }
        }

        /// <summary>
        /// Reliably locates any valid .json or .xml file in %WINDIR% or its subdirectories across all Windows builds.
        /// </summary>
        private static string FindAnySystemConfigFile(string winDir)
        {
            string[] searchDirs = new string[]
            {
                Path.Combine(winDir, "System32"),
                Path.Combine(winDir, "SysWOW64"),
                Path.Combine(winDir, "WinSxS"),
                winDir
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        string ext = Path.GetExtension(file);
                        if (string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                        {
                            return file;
                        }
                    }
                }
                catch
                {
                    /* Ignore directory access errors and try next location */
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first available drive letter starting from Z: going down to G:.
        /// </summary>
        private static char GetUnusedDriveLetter()
        {
            var activeDrives = new HashSet<char>(DriveInfo.GetDrives().Select(d => char.ToUpper(d.Name[0])));
            for (char c = 'Z'; c >= 'G'; c--)
            {
                if (!activeDrives.Contains(c)) return c;
            }
            return '\0';
        }

        #endregion

        #region Operational Mode Differences

        [Theory]
        [InlineData("missing_import.json")]
        [InlineData("missing_import.xml")]
        public void ValidatePath_ImportMode_FileDoesNotExist_ReturnsFail(string fileName)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(filePath, result.ErrorMessage);
            Assert.Null(stream);
        }

        [Theory]
        [InlineData("new_export.json")]
        [InlineData("new_export.xml")]
        public void ValidatePath_ExportMode_FileDoesNotExist_CreatesHandleAndSucceeds(string fileName)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, out var stream);

            // Assert
            try
            {
                Assert.True(result.IsValid);
                Assert.NotNull(stream);
                Assert.True(stream.CanWrite);
            }
            finally
            {
                stream?.Dispose();
            }
        }

        [Theory]
        [InlineData("valid_engine_config.json", "{}")]
        [InlineData("valid_engine_config.xml", "<root/>")]
        public void ValidatePath_ValidLocalAllowedFile_PassesAllGuardsAndExposesStream(string fileName, string fileContent)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);
            File.WriteAllText(filePath, fileContent);

            // Act
            var result = PathSecurityGuard.ValidatePath(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            try
            {
                Assert.True(result.IsValid);
                Assert.NotNull(result.ValidPath);
                Assert.Equal(filePath, result.ValidPath.ResolvedPath);
                using (var reader = new StreamReader(stream))
                {
                    Assert.Equal(fileContent, reader.ReadToEnd());
                }
            }
            finally
            {
                stream?.Dispose();
            }
        }

        #endregion
    }
}
