using Servy.Core.Resources;
using Servy.Core.Validation;
using Servy.Testing;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.UnitTests.Validation
{
    public class PathSecurityGuardTests : TempDirectoryTestBase
    {
        private static class NativeTestMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName, string? lpTargetPath);
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

        [Fact]
        public void ValidatePathOnly_InvalidPathChars_ReturnsFail()
        {
            // Arrange
            string invalidPath = new string(Path.GetInvalidPathChars());

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(invalidPath, FileMode.Open);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Strings.Msg_InvalidPath, result.ErrorMessage);
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
        [InlineData(@"\\server\share\config.json", FileMode.Open)]
        [InlineData(@"\\127.0.0.1\c$\config.json", FileMode.Open)]
        [InlineData(@"\\server\share\export.json", FileMode.OpenOrCreate)]
        public void ValidatePathOnly_UncPath_ReturnsFail(string uncPath, FileMode mode)
        {
            // Act
            var result = PathSecurityGuard.ValidatePathOnly(uncPath, mode);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
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
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(stream);

            // Assert the reserved-device guard specifically: the message must name the device it rejected.
            bool hitDosGuard = result.ErrorMessage.IndexOf(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase) >= 0;

            Assert.True(hitDosGuard,
                $"Expected DOS device payload to be intercepted directly by the local reserved device name guard filter. Actual error message: {result.ErrorMessage}");
        }

        [Theory]
        [InlineData("CON.json", FileMode.Open)]
        [InlineData("PRN.xml", FileMode.OpenOrCreate)]
        [InlineData("COM1.json", FileMode.Open)]
        [InlineData("LPT1.xml", FileMode.OpenOrCreate)]
        public void ValidatePathOnly_ReservedDeviceName_ReturnsFail(string fileName, FileMode mode)
        {
            // Arrange & Act
            var result = PathSecurityGuard.ValidatePathOnly(fileName, mode);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);

            bool hitDosGuard = result.ErrorMessage.IndexOf(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase) >= 0;

            Assert.True(hitDosGuard,
                $"Expected DOS device payload to be intercepted directly by the local reserved device name guard filter. Actual error message: {result.ErrorMessage}");
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
        [InlineData(FileMode.Open)]
        [InlineData(FileMode.OpenOrCreate)]
        public void ValidatePathOnly_ProtectedFolder_ReturnsFail(FileMode mode)
        {
            // Arrange
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string filePath = Path.Combine(protectedDir, "sys_config.json");

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(filePath, mode);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(protectedDir, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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

        [Theory]
        [InlineData("config.txt", FileMode.Open)]
        [InlineData("export.exe", FileMode.OpenOrCreate)]
        [InlineData("config.yaml", FileMode.Open)]
        public void ValidatePathOnly_InvalidExtension_ReturnsFail(string fileName, FileMode mode)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(filePath, mode);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(Path.GetExtension(fileName).ToLowerInvariant(), result.ErrorMessage);
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
                File.CreateSymbolicLink(symlinkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Skip test gracefully if the runner platform environment lacks symlink creation tokens
                Assert.Skip("Symlink creation unavailable on this runner");
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
        public void ValidatePathOnly_Symlink_ReturnsFail()
        {
            // Arrange
            string targetPath = Path.Combine(TempDirectory, "real_target.json");
            string symlinkPath = Path.Combine(TempDirectory, "symlink_target.json");

            File.WriteAllText(targetPath, "{}");

            try
            {
                File.CreateSymbolicLink(symlinkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Skip test gracefully if the runner platform environment lacks symlink creation tokens
                Assert.Skip("Symlink creation unavailable on this runner");
            }

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(symlinkPath, FileMode.Open);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(Strings.Msg_SecurityFileReparsePointProhibited, result.ErrorMessage);
        }

        [Fact]
        public void ValidatePath_ResolvedProtectedFolder_ReturnsFail()
        {
            // Arrange: Find an existing .json or .xml file inside %WINDIR% or System32.
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string? targetFile = FindAnySystemConfigFile(winDir);

            if (string.IsNullOrEmpty(targetFile))
            {
                Assert.Skip("No accessible system .json or .xml configuration file found in Windows directory.");
            }

            string targetDir = Path.GetDirectoryName(targetFile)!;
            string fileName = Path.GetFileName(targetFile);

            char driveLetter = GetUnusedDriveLetter();
            if (driveLetter == '\0')
            {
                Assert.Skip("No unused drive letter available for subst mapping.");
            }

            string drivePath = $"{driveLetter}:";
            if (!NativeTestMethods.DefineDosDevice(0, drivePath, targetDir))
            {
                Assert.Skip($"Failed to define virtual drive {drivePath}. Win32 Error: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                string substFilePath = Path.Combine($"{drivePath}\\", fileName);

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
            string? targetFile = FindAnySystemConfigFile(winDir);

            if (string.IsNullOrEmpty(targetFile))
            {
                Assert.Skip("No accessible system .json or .xml configuration file found in Windows directory.");
            }

            string targetDir = Path.GetDirectoryName(targetFile)!;
            string fileName = Path.GetFileName(targetFile);

            char driveLetter = GetUnusedDriveLetter();
            if (driveLetter == '\0')
            {
                Assert.Skip("No unused drive letter available for subst mapping.");
            }

            string drivePath = $"{driveLetter}:";
            if (!NativeTestMethods.DefineDosDevice(0, drivePath, targetDir))
            {
                Assert.Skip($"Failed to define virtual drive {drivePath}. Win32 Error: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                string substFilePath = Path.Combine($"{drivePath}\\", fileName);

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

        [Fact]
        public void ValidatePath_ExportMode_PostResolutionRejection_DeletesStubFile()
        {
            // Arrange
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string targetDir = Path.Combine(winDir, "System32");

            char driveLetter = GetUnusedDriveLetter();
            if (driveLetter == '\0')
            {
                Assert.Skip("No unused drive letter available for subst mapping.");
            }

            string drivePath = $"{driveLetter}:";
            if (!NativeTestMethods.DefineDosDevice(0, drivePath, targetDir))
            {
                Assert.Skip($"Failed to define virtual drive {drivePath}. Win32 Error: {Marshal.GetLastWin32Error()}");
            }

            string stubName = $"servy_stub_{Guid.NewGuid():N}.json";
            string substFilePath = Path.Combine($"{drivePath}\\", stubName);
            string realFilePath = Path.Combine(targetDir, stubName);

            try
            {
                // Act: Open file with OpenOrCreate mode targeting a non-existent path on a virtual drive that maps to a protected system folder.
                // Pre-open checks pass (as the virtual drive path hides System32), allowing the stream to be opened/created.
                // Post-resolution check unrolls the kernel path to System32, failing security validation and triggering the cleanup block.
                var result = PathSecurityGuard.ValidatePath(
                    substFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, out var stream);

                // Assert
                Assert.False(result.IsValid);
                Assert.Null(stream);

                // Verify the stub file created during handle resolution was cleaned up upon validation failure.
                Assert.False(File.Exists(realFilePath), "Rejected export left a stub file behind in the target directory.");
            }
            finally
            {
                try { File.Delete(realFilePath); } catch { }
                NativeTestMethods.DefineDosDevice(2, drivePath, null);
            }
        }

        [Fact]
        public void ValidatePath_AncestorDirectorySymlink_ReturnsFail()
        {
            string realDir = Path.Combine(TempDirectory, "real_dir");
            string linkDir = Path.Combine(TempDirectory, "link_dir");
            Directory.CreateDirectory(realDir);
            File.WriteAllText(Path.Combine(realDir, "config.json"), "{}");

            try
            {
                Directory.CreateSymbolicLink(linkDir, realDir);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Assert.Skip("Symlink creation unavailable on this runner");
            }

            var result = PathSecurityGuard.ValidatePath(
                Path.Combine(linkDir, "config.json"), FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.Equal(Strings.Msg_SecurityDirReparsePointProhibited, result.ErrorMessage);
            Assert.Null(stream);
        }

        [Fact]
        public void ValidatePathOnly_AncestorDirectorySymlink_ReturnsFail()
        {
            string realDir = Path.Combine(TempDirectory, "real_dir");
            string linkDir = Path.Combine(TempDirectory, "link_dir");
            Directory.CreateDirectory(realDir);
            File.WriteAllText(Path.Combine(realDir, "config.json"), "{}");

            try
            {
                Directory.CreateSymbolicLink(linkDir, realDir);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Assert.Skip("Symlink creation unavailable on this runner");
            }

            var result = PathSecurityGuard.ValidatePathOnly(
                Path.Combine(linkDir, "config.json"), FileMode.Open);

            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.Equal(Strings.Msg_SecurityDirReparsePointProhibited, result.ErrorMessage);
        }

        [Fact(Skip = "Requires a physical or virtual mapped network drive (DriveType.Network) mounted in the test runner environment.")]
        public void ValidatePath_MappedNetworkDrive_ReturnsFail()
        {
            // Arrange: Path on a mapped network drive (e.g. M:\config.json where M: is DriveType.Network)
            string networkDrivePath = @"M:\config.json";

            // Act
            var result = PathSecurityGuard.ValidatePath(networkDrivePath, FileMode.Open, FileAccess.Read, FileShare.Read, out var stream);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(Strings.Msg_SecurityNetworkDriveProhibited, result.ErrorMessage);
            Assert.Null(stream);
        }

        [Fact(Skip = "Requires a physical or virtual mapped network drive (DriveType.Network) mounted in the test runner environment.")]
        public void ValidatePathOnly_MappedNetworkDrive_ReturnsFail()
        {
            // Arrange: Path on a mapped network drive (e.g. M:\config.json where M: is DriveType.Network)
            string networkDrivePath = @"M:\config.json";

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(networkDrivePath, FileMode.Open);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.Security, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(Strings.Msg_SecurityNetworkDriveProhibited, result.ErrorMessage);
        }

        /// <summary>
        /// Reliably locates any valid .json or .xml file in %WINDIR% or its subdirectories across all Windows builds.
        /// </summary>
        private static string? FindAnySystemConfigFile(string winDir)
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            // Probe standard System32/WinSxS directories where XML/JSON files exist on all Windows installations
            string[] searchDirs = new[]
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
                    var match = Directory.EnumerateFiles(dir, "*.xml", options).FirstOrDefault()
                             ?? Directory.EnumerateFiles(dir, "*.json", options).FirstOrDefault();

                    if (match != null) return match;
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
        [InlineData("missing_import.json")]
        [InlineData("missing_import.xml")]
        public void ValidatePathOnly_ImportMode_FileDoesNotExist_ReturnsFail(string fileName)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(filePath, FileMode.Open);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(PathSecurityFailureKind.InvalidArgument, result.FailureKind);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(filePath, result.ErrorMessage);
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

        [Fact]
        public void TryGetFinalPathByHandle_OpenLocalFile_ReturnsNormalizedPath()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "resolved_path.json");
            File.WriteAllText(filePath, "{}");

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Act
                bool resolved = PathSecurityGuard.TryGetFinalPathByHandle(stream.SafeFileHandle, out string finalPath);

                // Assert
                Assert.True(resolved);
                Assert.Equal(Path.GetFullPath(filePath), Path.GetFullPath(finalPath), ignoreCase: true);
            }
        }

        [Theory]
        [InlineData("new_export.json")]
        [InlineData("new_export.xml")]
        public void ValidatePathOnly_ExportMode_FileDoesNotExist_SucceedsWithoutCreatingFile(string fileName)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(filePath, FileMode.OpenOrCreate);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.ValidPath);
            Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
            Assert.False(File.Exists(filePath), "ValidatePathOnly should audit path metadata without creating stub files.");
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
                Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
                using (var reader = new StreamReader(stream!))
                {
                    Assert.Equal(fileContent, reader.ReadToEnd());
                }
            }
            finally
            {
                stream?.Dispose();
            }
        }

        [Theory]
        [InlineData("valid_engine_config.json", "{}")]
        [InlineData("valid_engine_config.xml", "<root/>")]
        public void ValidatePathOnly_ValidLocalAllowedFile_PassesAllGuards(string fileName, string fileContent)
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, fileName);
            File.WriteAllText(filePath, fileContent);

            // Act
            var result = PathSecurityGuard.ValidatePathOnly(filePath, FileMode.Open);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.ValidPath);
            Assert.Equal(filePath, result.ValidPath!.ResolvedPath);
        }

        #endregion

        #region Application Containment Guards

        [Theory]
        [InlineData(null, @"C:\Program Files\Servy")]
        [InlineData("", @"C:\Program Files\Servy")]
        [InlineData("   ", @"C:\Program Files\Servy")]
        [InlineData("Servy.Manager.exe", null)]
        [InlineData("Servy.Manager.exe", "")]
        [InlineData("Servy.Manager.exe", "   ")]
        public void IsSafelyContainedWithinAppDirectory_NullOrWhitespace_ReturnsFalse(string? targetPath, string? baseDir)
        {
            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(targetPath!, baseDir!);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(@"\\server\share\payload.exe")]
        [InlineData(@"//remote/share/payload.exe")]
        public void IsSafelyContainedWithinAppDirectory_UncPath_ReturnsFalse(string uncPath)
        {
            // Arrange
            string baseDir = TempDirectory;

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(uncPath, baseDir);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_RelativeChildPath_ReturnsTrue()
        {
            // Arrange
            string baseDir = TempDirectory;
            string targetPath = "Servy.Manager.exe";

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(targetPath, baseDir);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_SubdirectoryChildPath_ReturnsTrue()
        {
            // Arrange
            string baseDir = TempDirectory;
            string subDir = Path.Combine(baseDir, "bin");
            Directory.CreateDirectory(subDir);
            string targetPath = Path.Combine("bin", "Servy.Manager.exe");

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(targetPath, baseDir);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_AbsoluteChildPath_ReturnsTrue()
        {
            // Arrange
            string baseDir = Path.Combine(TempDirectory, "App");
            Directory.CreateDirectory(baseDir);
            string targetPath = Path.Combine(baseDir, "Servy.Manager.exe");

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(targetPath, baseDir);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_ExternalRootedPath_ReturnsFalse()
        {
            // Arrange
            string baseDir = Path.Combine(TempDirectory, "App");
            Directory.CreateDirectory(baseDir);
            string externalPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(externalPath, baseDir);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_DirectoryTraversal_ReturnsFalse()
        {
            // Arrange
            string baseDir = Path.Combine(TempDirectory, "App");
            Directory.CreateDirectory(baseDir);
            string traversalPath = @"..\..\Windows\System32\cmd.exe";

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(traversalPath, baseDir);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_SiblingDirectoryWithBasePrefix_ReturnsFalse()
        {
            // Arrange
            string baseDir = Path.Combine(TempDirectory, "App");
            string siblingDir = Path.Combine(TempDirectory, "AppEvil");
            Directory.CreateDirectory(baseDir);
            Directory.CreateDirectory(siblingDir);
            string targetPath = Path.Combine(siblingDir, "payload.exe");

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(targetPath, baseDir);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_InvalidPathChars_ReturnsFalse()
        {
            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory("Servy.Manager\0.exe", TempDirectory);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_ReparsePointInPath_ReturnsFalse()
        {
            // Arrange
            string realDir = Path.Combine(TempDirectory, "real_app_dir");
            string linkDir = Path.Combine(TempDirectory, "link_app_dir");
            Directory.CreateDirectory(realDir);

            try
            {
                Directory.CreateSymbolicLink(linkDir, realDir);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Assert.Skip("Symlink creation unavailable on this runner");
            }

            string targetFile = Path.Combine(linkDir, "Servy.Manager.exe");

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(targetFile, linkDir);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSafelyContainedWithinAppDirectory_FileSymlink_ReturnsFalse()
        {
            // Arrange
            string targetFile = Path.Combine(TempDirectory, "target.exe");
            string linkFile = Path.Combine(TempDirectory, "Servy.Manager.exe");
            File.WriteAllText(targetFile, "placeholder");

            try
            {
                File.CreateSymbolicLink(linkFile, targetFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Assert.Skip("Symlink creation unavailable on this runner");
            }

            // Act
            bool result = PathSecurityGuard.IsSafelyContainedWithinAppDirectory(linkFile, TempDirectory);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Directory ACL Hardening Guards

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsDirectoryAclHardened_NullOrWhitespace_ReturnsFalse(string? path)
        {
            // Act
            bool result = PathSecurityGuard.IsDirectoryAclHardened(path!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsDirectoryAclHardened_NonExistentDirectory_ReturnsTrue()
        {
            // Arrange
            string nonExistent = Path.Combine(TempDirectory, $"missing_dir_{Guid.NewGuid():N}");

            // Act
            bool result = PathSecurityGuard.IsDirectoryAclHardened(nonExistent);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsDirectoryAclHardened_UsersReadExecuteOnly_ReturnsTrue()
        {
            // Arrange
            string testDir = Path.Combine(TempDirectory, "acl_read_execute_dir");
            Directory.CreateDirectory(testDir);
            SetBuiltinUsersAccessRule(testDir, FileSystemRights.ReadAndExecute);

            // Act
            bool result = PathSecurityGuard.IsDirectoryAclHardened(testDir);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsDirectoryAclHardened_UsersModify_ReturnsFalse()
        {
            // Arrange
            string testDir = Path.Combine(TempDirectory, "acl_modify_dir");
            Directory.CreateDirectory(testDir);
            SetBuiltinUsersAccessRule(testDir, FileSystemRights.Modify);

            // Act
            bool result = PathSecurityGuard.IsDirectoryAclHardened(testDir);

            // Assert
            Assert.False(result);
        }

        private static void SetBuiltinUsersAccessRule(string directoryPath, FileSystemRights rights)
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            security.AddAccessRule(new FileSystemAccessRule(usersSid, rights, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
            new DirectoryInfo(directoryPath).SetAccessControl(security);
        }

        #endregion
    }
}
