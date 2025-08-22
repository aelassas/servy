using Servy.Core.DTOs;
using Servy.Core.Helpers;
using System;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class XmlServiceSerializerTests
    {
        private readonly XmlServiceSerializer _serializer = new XmlServiceSerializer();

        [Fact]
        public void Deserialize_NullOrEmpty_ReturnsNull()
        {
            // Null string
            Assert.Null(_serializer.Deserialize(null));

            // Empty string
            Assert.Null(_serializer.Deserialize(string.Empty));

            // Whitespace string
            Assert.Null(_serializer.Deserialize("   "));
        }

        [Fact]
        public void Deserialize_ValidXml_ReturnsServiceDto()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "TestService",
                Description = "Test description",
                ExecutablePath = @"C:\test.exe"
            };

            var xmlSerializer = new XmlSerializer(typeof(ServiceDto));
            string xml;
            using (var sw = new StringWriter())
            {
                xmlSerializer.Serialize(sw, dto);
                xml = sw.ToString();
            }

            // Act
            var result = _serializer.Deserialize(xml);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(dto.Name, result.Name);
            Assert.Equal(dto.Description, result.Description);
            Assert.Equal(dto.ExecutablePath, result.ExecutablePath);
        }

        [Fact]
        public void Deserialize_InvalidXml_ThrowsInvalidOperationException()
        {
            // Arrange
            var invalidXml = "<ServiceDto><Name>Test</Name>"; // Missing closing tags properly

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _serializer.Deserialize(invalidXml));
        }
    }
}
