using Servy.Core.Enums;
using Servy.Service.StreamWriters;
using System;
using System.IO;
using Xunit;

namespace Servy.Service.UnitTests.StreamWriters
{
    public class RotatingStreamWriterAdapterTests : IDisposable
    {
        private readonly string _tempPath;

        public RotatingStreamWriterAdapterTests()
        {
            // Create a unique temporary file path for every test to avoid file access conflicts
            _tempPath = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid()}.log");
        }

        private RotatingStreamWriterAdapter CreateAdapter()
        {
            return new RotatingStreamWriterAdapter(
                _tempPath,
                enableSizeRotation: false,
                rotationSizeInBytes: 1024,
                enableDateRotation: false,
                dateRotationType: DateRotationType.Daily,
                maxRotations: 5,
                useLocalTimeForRotation: false);
        }

        #region Disposal & Guard Clause Tests

        [Fact]
        public void Dispose_IsIdempotent()
        {
            // Arrange
            var adapter = CreateAdapter();

            // Act
            adapter.Dispose();

            // Assert: Second dispose should not throw
            var ex = Record.Exception(() => adapter.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void WriteLine_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var adapter = CreateAdapter();
            adapter.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => adapter.WriteLine("Test line"));
        }

        [Fact]
        public void Write_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var adapter = CreateAdapter();
            adapter.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => adapter.Write("Test text"));
        }

        #endregion

        #region Functional Delegation Tests

        [Fact]
        public void WriteLine_WritesToStreamSuccessfully()
        {
            // Arrange
            var adapter = CreateAdapter();
            string testLine = "Log message content";

            // Act
            adapter.WriteLine(testLine);
            adapter.Dispose(); // Ensure flush

            // Assert
            Assert.True(File.Exists(_tempPath));
            string content = File.ReadAllText(_tempPath);
            Assert.Contains(testLine, content);
        }

        [Fact]
        public void Write_WritesToStreamSuccessfully()
        {
            // Arrange
            var adapter = CreateAdapter();
            string testText = "Partial text";

            // Act
            adapter.Write(testText);
            adapter.Dispose();

            // Assert
            Assert.True(File.Exists(_tempPath));
            string content = File.ReadAllText(_tempPath);
            Assert.Equal(testText, content);
        }

        #endregion

        #region Teardown

        public void Dispose()
        {
            // Clean up the temporary file created by the RotatingStreamWriter
            if (File.Exists(_tempPath))
            {
                try { File.Delete(_tempPath); } catch { /* Ignore cleanup errors */ }
            }
        }

        #endregion
    }
}