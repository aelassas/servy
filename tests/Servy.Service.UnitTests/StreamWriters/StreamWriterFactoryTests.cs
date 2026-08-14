using Servy.Core.Enums;
using Servy.Service.StreamWriters;
using Servy.Testing;
using System;
using System.IO;
using Xunit;

namespace Servy.Service.UnitTests.StreamWriters
{
    public class StreamWriterFactoryTests
    {
        [Fact]
        public void Create_ReturnsInstanceOfRotatingStreamWriterAdapter()
        {
            // Arrange
            var factory = new StreamWriterFactory();
            string path = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.log");
            bool enableSizeRotation = true;
            long rotationSizeInBytes = 1024;
            bool enableDateRotation = false;
            DateRotationType dateRotationType = DateRotationType.Daily;
            int maxRotations = 3;
            bool useLocalTime = true;

            try
            {
                // Act & Assert
                using (var result = factory.Create(
                    path,
                    enableSizeRotation,
                    rotationSizeInBytes,
                    enableDateRotation,
                    dateRotationType,
                    maxRotations,
                    useLocalTime))
                {
                    var adapter = Assert.IsType<RotatingStreamWriterAdapter>(result);
                    var inner = TestReflection.GetField<Core.IO.RotatingStreamWriter>(adapter, "_inner");
                    Assert.Equal(path, TestReflection.GetField<FileInfo>(inner, "_file").FullName);
                    Assert.Equal(enableSizeRotation, TestReflection.GetField<bool>(inner, "_enableSizeRotation"));
                    Assert.Equal(rotationSizeInBytes, TestReflection.GetField<long>(inner, "_rotationSizeInBytes"));
                    Assert.Equal(enableDateRotation, TestReflection.GetField<bool>(inner, "_enableDateRotation"));
                    Assert.Equal(dateRotationType, TestReflection.GetField<DateRotationType>(inner, "_dateRotationType"));
                    Assert.Equal(maxRotations, TestReflection.GetField<int>(inner, "_maxRotations"));
                    Assert.Equal(useLocalTime, TestReflection.GetField<bool>(inner, "_useLocalTimeForRotation"));
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { /* Ignore exceptions */ }
                }
            }
        }
    }
}
