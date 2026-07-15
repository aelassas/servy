using Servy.Core.Enums;
using Servy.Service.StreamWriters;

namespace Servy.Service.UnitTests.StreamWriters
{
    public class StreamWriterFactoryTests
    {
        [Fact]
        public void Create_ReturnsInstanceOfRotatingStreamWriterAdapter()
        {
            // Arrange
            var factory = new StreamWriterFactory();
            string path = "test.log";
            bool enableSizeRotation = true;
            long rotationSizeInBytes = 1024;
            bool enableDateRotation = false;
            DateRotationType dateRotationType = DateRotationType.Daily;
            int maxRotations = 3;
            bool useLocalTime = true;

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
                Assert.IsType<RotatingStreamWriterAdapter>(result);
            }
        }
    }
}