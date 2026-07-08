using Servy.Core.Native;
using System.ComponentModel;
using System.Security;

namespace Servy.Core.UnitTests.Native
{
    public class NativeMethodsHelpersTests : IDisposable
    {
        private readonly string _testDir;

        public NativeMethodsHelpersTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ServyNativeTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                try
                {
                    Directory.Delete(_testDir, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        #region ValidateCredentials Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateCredentials_EmptyUsername_ThrowsArgumentException(string? invalidUsername)
        {
            Assert.Throws<ArgumentException>(() => NativeMethodsHelpers.ValidateCredentials(invalidUsername!, null));
        }

        [Theory]
        [InlineData("Everyone")]
        [InlineData("Anonymous Logon")]
        [InlineData("NT AUTHORITY\\Interactive")]
        public void ValidateCredentials_ForbiddenGroup_ThrowsArgumentException(string forbiddenGroup)
        {
            var ex = Assert.Throws<ArgumentException>(() => NativeMethodsHelpers.ValidateCredentials(forbiddenGroup, null));
            Assert.Contains("group or logon context", ex.Message);
        }

        [Theory]
        [InlineData("LocalSystem")]
        [InlineData("NT AUTHORITY\\NetworkService")]
        [InlineData(".\\LocalService")]
        [InlineData("NT SERVICE\\MyCustomService")]
        [InlineData("IIS APPPOOL\\DefaultAppPool")]
        public void ValidateCredentials_BuiltInAccount_WithPassword_ThrowsArgumentException(string builtInAccount)
        {
            var ex = Assert.Throws<ArgumentException>(() => NativeMethodsHelpers.ValidateCredentials(builtInAccount, "SomePassword"));
            Assert.Contains("password cannot be provided", ex.Message);
        }

        [Theory]
        [InlineData("LocalSystem")]
        [InlineData("NT AUTHORITY\\NetworkService")]
        [InlineData(".\\LocalService")]
        [InlineData("NT SERVICE\\MyCustomService")]
        [InlineData("IIS APPPOOL\\DefaultAppPool")]
        public void ValidateCredentials_BuiltInOrVirtualAccount_WithoutPassword_Succeeds(string account)
        {
            // Act & Assert - Should not throw
            var ex = Record.Exception(() => NativeMethodsHelpers.ValidateCredentials(account, null));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData("JustAUser")]
        [InlineData("DOMAIN\\")]
        [InlineData(".\\")]
        public void ValidateCredentials_InvalidFormat_ThrowsArgumentException(string badFormat)
        {
            var ex = Assert.Throws<ArgumentException>(() => NativeMethodsHelpers.ValidateCredentials(badFormat, null));
            Assert.Contains("Username format is invalid", ex.Message);
        }

        [Fact]
        public void ValidateCredentials_UnmappedAccount_ThrowsSecurityException()
        {
            // Arrange
            string fakeUser = $".\\FakeUser_{Guid.NewGuid():N}";

            // Act & Assert
            var ex = Assert.Throws<SecurityException>(() => NativeMethodsHelpers.ValidateCredentials(fakeUser, null));
            Assert.Contains("could not be resolved", ex.Message);
        }

        [Fact]
        public void ValidateCredentials_ValidLocalAccount_BadPassword_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            // Environment.UserName maps to the current executing user, guaranteeing it exists and passes translation.
            string currentUser = $".\\{Environment.UserName}";
            string badPassword = Guid.NewGuid().ToString(); // Guaranteed to be wrong

            // Act & Assert
            // When LogonUser is hit with a bad password, it throws UnauthorizedAccessException
            var exception = Assert.ThrowsAny<Exception>(() => NativeMethodsHelpers.ValidateCredentials(currentUser, badPassword));

            Assert.True(exception is System.ComponentModel.Win32Exception || exception is UnauthorizedAccessException,
                $"Expected Win32Exception or UnauthorizedAccessException, but caught: {exception.GetType().Name}");
        }

        #endregion

        #region AtomicSecureMove Tests

        [Fact]
        public void AtomicSecureMove_NullOrEmptySource_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>("source", () => NativeMethodsHelpers.AtomicSecureMove("", "dest.txt"));
        }

        [Fact]
        public void AtomicSecureMove_NullOrEmptyDestination_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>("destination", () => NativeMethodsHelpers.AtomicSecureMove("src.txt", ""));
        }

        [Fact]
        public void AtomicSecureMove_SourceDoesNotExist_ThrowsWin32Exception()
        {
            // Arrange
            string src = Path.Combine(_testDir, "doesnotexist.txt");
            string dest = Path.Combine(_testDir, "dest.txt");

            // Act & Assert
            var ex = Assert.Throws<Win32Exception>(() => NativeMethodsHelpers.AtomicSecureMove(src, dest));
            Assert.Contains("Failed to atomically replace", ex.Message);
        }

        [Fact]
        public void AtomicSecureMove_ValidFiles_Succeeds()
        {
            // Arrange
            string src = Path.Combine(_testDir, "src.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(src, "Hello World");

            // Act
            NativeMethodsHelpers.AtomicSecureMove(src, dest);

            // Assert
            Assert.False(File.Exists(src));
            Assert.True(File.Exists(dest));
            Assert.Equal("Hello World", File.ReadAllText(dest));
        }

        #endregion

        #region GetFileIdentity Tests

        [Fact]
        public void GetFileIdentity_EmptyFile_ReturnsEmptyPrefixDigest()
        {
            // Arrange
            string filePath = Path.Combine(_testDir, "empty.log");
            File.WriteAllBytes(filePath, Array.Empty<byte>());

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // Act
                var identity = NativeMethodsHelpers.GetFileIdentity(fs);

                // Assert
                Assert.True(identity.IsValidHandleInfo);
                Assert.Equal(string.Empty, identity.PrefixDigest);
            }
        }

        [Fact]
        public void GetFileIdentity_FileWithContent_ReturnsValidDigest()
        {
            // Arrange
            string filePath = Path.Combine(_testDir, "content.log");
            File.WriteAllText(filePath, "Testing log prefix hashing.");

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // Act
                var identity = NativeMethodsHelpers.GetFileIdentity(fs);

                // Assert
                Assert.True(identity.IsValidHandleInfo);
                Assert.False(string.IsNullOrEmpty(identity.PrefixDigest));

                // Digest should be a hex string
                Assert.Matches("^[a-f0-9]+$", identity.PrefixDigest);
            }
        }

        [Fact]
        public void GetFileIdentity_DifferentFileLengthsWithSamePrefix_ProduceDifferentDigests()
        {
            // Arrange
            string prefix = "STATIC_HEADER_CONTENT_BLOCK";

            string file1Path = Path.Combine(_testDir, "file1.log");
            string file2Path = Path.Combine(_testDir, "file2.log");

            // File 1: Just the prefix
            File.WriteAllText(file1Path, prefix);

            // File 2: Same prefix, but larger file size
            using (var fs = new FileStream(file2Path, FileMode.Create))
            using (var writer = new StreamWriter(fs))
            {
                writer.Write(prefix);
                writer.Write(new string('A', 5000)); // Push size past prefix limit
            }

            string digest1, digest2;

            // Act
            using (var fs1 = new FileStream(file1Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                digest1 = NativeMethodsHelpers.GetFileIdentity(fs1).PrefixDigest;
            }

            using (var fs2 = new FileStream(file2Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                digest2 = NativeMethodsHelpers.GetFileIdentity(fs2).PrefixDigest;
            }

            // Assert
            Assert.False(string.IsNullOrEmpty(digest1));
            Assert.False(string.IsNullOrEmpty(digest2));
            Assert.NotEqual(digest1, digest2); // Length domain separator ensures they are different
        }

        [Fact]
        public void GetFileIdentity_ClosedStream_LogsAndHandlesGracefully()
        {
            // Arrange
            string filePath = Path.Combine(_testDir, "closed.log");
            File.WriteAllText(filePath, "Content");

            FileStream closedStream;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                closedStream = fs;
            } // fs is now disposed and handles are closed

            // Act
            // Passing a closed stream will force SafeFileHandle to throw ObjectDisposedException
            // and fs.CanSeek to return false. The method should catch these safely.
            var identity = NativeMethodsHelpers.GetFileIdentity(closedStream);

            // Assert
            Assert.False(identity.IsValidHandleInfo); // Kernel32 probe failed
            Assert.Null(identity.PrefixDigest); // Prefix probe skipped (CanSeek was false)
        }

        #endregion
    }
}