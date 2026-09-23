using Servy.Testing;
using System.IO;

namespace Servy.Core.UnitTests.TestInfrastructure
{
    /// <summary>
    /// Tests for <see cref="TempFile"/>'s disposal, the second of the two call sites that used to
    /// hand-roll the retry-delete loop now owned by <see cref="RetryDelete"/>. Only this class can
    /// reach the single-file delete, so these cases report on that former copy alone.
    /// </summary>
    public class TempFileTests
    {
        [Fact]
        public void Dispose_FileIsNotLocked_DeletesTheTemporaryFile()
        {
            // Arrange
            var tempFile = new TempFile(".probe").Write("content");
            var path = tempFile.Path;

            // Act
            tempFile.Dispose();

            // Assert
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void Dispose_FileIsLockedByAnotherHandle_SwallowsTheLockAndLeavesTheFileInPlace()
        {
            // Arrange
            var tempFile = new TempFile(".probe").Write("content");

            try
            {
                using (new FileStream(tempFile.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Act
                    var thrown = Record.Exception(() => tempFile.Dispose());

                    // Assert
                    Assert.Null(thrown);
                    Assert.True(File.Exists(tempFile.Path));
                }
            }
            finally
            {
                if (File.Exists(tempFile.Path))
                {
                    File.Delete(tempFile.Path);
                }
            }
        }

        [Fact]
        public void Dispose_FileWasNeverWritten_ReturnsWithoutThrowing()
        {
            // Arrange
            var tempFile = new TempFile(".probe");

            // Act
            var thrown = Record.Exception(() => tempFile.Dispose());

            // Assert
            Assert.Null(thrown);
            Assert.False(File.Exists(tempFile.Path));
        }
    }
}
