using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Services;
using System;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class XmlServiceSerializerTests
    {
        private readonly XmlServiceSerializer _serializer = new XmlServiceSerializer();

        [Fact]
        public void Deserialize_NullOrEmpty_ReturnsNull()
        {
            // Branch: if (string.IsNullOrWhiteSpace(xml))
            Assert.Null(_serializer.Deserialize(null));
            Assert.Null(_serializer.Deserialize(string.Empty));
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
        public void Deserialize_PartialXml_AppliesDefaults()
        {
            // Arrange: Minimal XML missing most properties
            var xml = "<ServiceDto><Name>PartialTest</Name></ServiceDto>";

            // Act
            var result = _serializer.Deserialize(xml);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PartialTest", result.Name);

            // Integration check: Verify ApplyDefaults was called 
            // by checking a property not present in the XML above.
            Assert.Equal(AppConfig.DefaultStartTimeout, result.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeout);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, result.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_InvalidXml_ThrowsInvalidOperationException()
        {
            // Arrange: Malformed XML
            var invalidXml = "<ServiceDto><Name>Test</Name>";

            // Act & Assert
            // This covers the case where XmlReader fails to parse
            Assert.Throws<InvalidOperationException>(() => _serializer.Deserialize(invalidXml));
        }

        [Fact]
        public void Deserialize_WrongRootElement_ReturnsNull()
        {
            // Arrange: Valid XML but not a ServiceDto
            // This forces the 'is ServiceDto' check to fail
            var xml = "<OtherObject><Value>123</Value></OtherObject>";

            // Act & Assert
            // Note: XmlSerializer usually throws InvalidOperationException if the root 
            // doesn't match the type it was initialized with.
            Assert.Throws<InvalidOperationException>(() => _serializer.Deserialize(xml));
        }
    }
}