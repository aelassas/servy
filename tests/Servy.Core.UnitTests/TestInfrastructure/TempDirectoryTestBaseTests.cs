using Servy.Testing;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.TestInfrastructure
{
    /// <summary>
    /// Tests for <see cref="TempDirectoryTestBase"/>'s disposal, the first of the two call sites
    /// that used to hand-roll the retry-delete loop now owned by <see cref="RetryDelete"/>.
    /// Only this base class can reach the recursive directory delete, so these cases report on
    /// that former copy alone.
    /// </summary>
    public class TempDirectoryTestBaseTests
    {
        /// <summary>
        /// Concrete stand-in for the abstract base, exposing the protected directory path.
        /// It is deliberately not a test class of its own.
        /// </summary>
        private sealed class Probe : TempDirectoryTestBase
        {
            public string Directory => TempDirectory;
        }

        [Fact]
        public void Dispose_DirectoryIsNotLocked_DeletesTheTemporaryDirectory()
        {
            // Arrange
            var probe = new Probe();
            File.WriteAllText(Path.Combine(probe.Directory, "payload.txt"), "content");

            // Act
            probe.Dispose();

            // Assert
            Assert.False(Directory.Exists(probe.Directory));
        }

        [Fact]
        public void Dispose_DirectoryHoldsALockedFile_SwallowsTheLockAndLeavesTheDirectoryInPlace()
        {
            // Arrange
            var probe = new Probe();
            var lockedFile = Path.Combine(probe.Directory, "locked.bin");

            try
            {
                using (new FileStream(lockedFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    // Act
                    var thrown = Record.Exception(() => probe.Dispose());

                    // Assert
                    Assert.Null(thrown);
                    Assert.True(Directory.Exists(probe.Directory));
                }
            }
            finally
            {
                if (Directory.Exists(probe.Directory))
                {
                    Directory.Delete(probe.Directory, recursive: true);
                }
            }
        }

        [Fact]
        public void Dispose_DirectoryAlreadyRemoved_ReturnsWithoutThrowing()
        {
            // Arrange
            var probe = new Probe();
            Directory.Delete(probe.Directory, recursive: true);

            // Act
            var thrown = Record.Exception(() => probe.Dispose());

            // Assert
            Assert.Null(thrown);
        }
    }
}
