using Servy.Core.Enums;
using Servy.Service.StreamWriters;
using Servy.Testing;

namespace Servy.Service.UnitTests.StreamWriters
{
    public class StreamWriterFactoryTests
    {
        [Theory]
        [InlineData(true, false, true)]
        [InlineData(false, true, false)]
        public void Create_ReturnsInstanceOfRotatingStreamWriterAdapter(
            bool enableSizeRotation,
            bool enableDateRotation,
            bool useLocalTime)
        {
            // Arrange
            var factory = new StreamWriterFactory();
            string path = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid():N}.log");
            long rotationSizeInBytes = 1024;
            DateRotationType dateRotationType = DateRotationType.Daily;
            int maxRotations = 3;

            try
            {
                // Act
                using (var result = factory.Create(
                    path: path,
                    enableSizeRotation: enableSizeRotation,
                    rotationSizeInBytes: rotationSizeInBytes,
                    enableDateRotation: enableDateRotation,
                    dateRotationType: dateRotationType,
                    maxRotations: maxRotations,
                    useLocalTimeForRotation: useLocalTime))
                {
                    // Assert
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
