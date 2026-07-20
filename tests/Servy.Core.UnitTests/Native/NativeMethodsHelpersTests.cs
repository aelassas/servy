using Servy.Core.Config;
using Servy.Core.Native;
using System;
using System.ComponentModel;
using System.IO;
using System.Security;
using Xunit;

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
        public void ValidateCredentials_EmptyUsername_ThrowsArgumentException(string invalidUsername)
        {
            Assert.Throws<ArgumentException>(() => NativeMethodsHelpers.ValidateCredentials(invalidUsername, null));
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
        public void ValidateCredentials_ValidLocalAccount_BadPassword_ThrowsException()
        {
            // Arrange
            // ENVIRONMENT DEPENDENCY NOTE: Environment.UserName maps to the current executing identity. 
            // If the host is domain-joined or running under a built-in system service account, resolving this 
            // as a local machine account via '.\' can trigger translation or password-guard failures before 
            // LogonUser is reached. We capture any downstream structural failure context below.
            string currentUser = $".\\{Environment.UserName}";
            string badPassword = Guid.NewGuid().ToString(); // Guaranteed to be wrong

            // Act & Assert
            // Depending on host configuration (Workstation vs. Domain vs. SYSTEM), this call path will fail with 
            // UnauthorizedAccessException/Win32Exception (bad password), SecurityException (unmapped local name),
            // or ArgumentException (system identity rule violation).
            var exception = Assert.ThrowsAny<Exception>(() => NativeMethodsHelpers.ValidateCredentials(currentUser, badPassword));

            // ENVIRONMENT-RESILIENT RANGE CHECK: Broaden the accepted exception boundaries to prevent environmental false-positives
            Assert.True(
                exception is Win32Exception ||
                exception is UnauthorizedAccessException ||
                exception is SecurityException ||
                exception is ArgumentException,
                $"Unexpected identity translation exception caught for current environment profile: {exception.GetType().Name}");
        }

        #endregion

        #region AtomicSecureMove Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AtomicSecureMove_NullOrEmptySource_ThrowsArgumentException(string source)
        {
            Assert.Throws<ArgumentException>("source", () => NativeMethodsHelpers.AtomicSecureMove(source, "dest.txt"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AtomicSecureMove_NullOrEmptyDestination_ThrowsArgumentException(string destination)
        {
            Assert.Throws<ArgumentException>("destination", () => NativeMethodsHelpers.AtomicSecureMove("src.txt", destination));
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

        [Fact]
        public void AtomicSecureMove_DestinationExists_ReplacesAtomically()
        {
            string src = Path.Combine(_testDir, "src.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(src, "New Content");
            File.WriteAllText(dest, "Old Content");   // destination already exists

            NativeMethodsHelpers.AtomicSecureMove(src, dest);

            Assert.False(File.Exists(src));
            Assert.True(File.Exists(dest));
            Assert.Equal("New Content", File.ReadAllText(dest));
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
            // Generate a shared prefix block that spans exactly the 4096-byte prefix buffer window
            int prefixSize = AppConfig.FileIdentityPrefixBytes; // 4096 bytes
            byte[] sharedPrefix = new byte[prefixSize];
            for (int i = 0; i < prefixSize; i++)
            {
                sharedPrefix[i] = (byte)'A';
            }

            string file1Path = Path.Combine(_testDir, "file1.log");
            string file2Path = Path.Combine(_testDir, "file2.log");

            // File 1: Exactly the shared prefix (exactly 4096 bytes)
            File.WriteAllBytes(file1Path, sharedPrefix);

            // File 2: Same prefix, but larger file size (additional characters appended past the 4096-byte limit)
            using (var fs = new FileStream(file2Path, FileMode.Create))
            {
                fs.Write(sharedPrefix, 0, sharedPrefix.Length);

                // Append extra padding. Because this sits past the 4096-byte boundary, 
                // the content read by the digest buffer remains completely identical to File 1.
                byte[] extraPadding = new byte[1000];
                for (int i = 0; i < extraPadding.Length; i++)
                {
                    extraPadding[i] = (byte)'B';
                }
                fs.Write(extraPadding, 0, extraPadding.Length);
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

            // Under an identical 4096-byte prefix read, this assertion is guaranteed to fail 
            // if the native length-domain separator logic is ever modified or removed.
            Assert.NotEqual(digest1, digest2);
        }

        [Fact]
        public void GetFileIdentity_ClosedStream_HandlesGracefully()
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