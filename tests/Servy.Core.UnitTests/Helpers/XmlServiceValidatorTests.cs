using Servy.Core.DTOs;
using Servy.Core.Helpers;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class XmlServiceValidatorTests
    {
        private string Serialize(ServiceDto dto)
        {
            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var sw = new StringWriter())
            {
                serializer.Serialize(sw, dto);
                return sw.ToString();
            }
        }

        [Fact]
        public void TryValidate_NullOrEmptyXml_ReturnsFalse()
        {
            var result = XmlServiceValidator.TryValidate(null, out var error);
            Assert.False(result);
            Assert.Equal("XML cannot be empty.", error);

            result = XmlServiceValidator.TryValidate("   ", out error);
            Assert.False(result);
            Assert.Equal("XML cannot be empty.", error);
        }

        [Fact]
        public void TryValidate_InvalidXml_ReturnsFalse()
        {
            var result = XmlServiceValidator.TryValidate("<ServiceDto><Name>Test</Name>", out var error);
            Assert.False(result);
            Assert.StartsWith("Invalid XML format:", error);
        }

        [Fact]
        public void TryValidate_XmlNotMatchingServiceDto_ReturnsFalse()
        {
            string xml = "<NotServiceDto><Foo>bar</Foo></NotServiceDto>";
            var result = XmlServiceValidator.TryValidate(xml, out var error);
            Assert.False(result);
            Assert.StartsWith("XML does not match ServiceDto format:", error);
        }

        [Fact]
        public void TryValidate_DeserializedDtoIsNull_ReturnsFalse()
        {
            string xml = "<ServiceDto xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />";
            var result = XmlServiceValidator.TryValidate(xml, out var error);
            Assert.False(result);
            Assert.Equal("Failed to deserialize XML to ServiceDto.", error);
        }

        [Fact]
        public void TryValidate_MissingName_ReturnsFalse()
        {
            var dto = new ServiceDto
            {
                Name = "",
                ExecutablePath = "C:\\path\\to\\exe"
            };
            var xml = Serialize(dto);

            var result = XmlServiceValidator.TryValidate(xml, out var error);
            Assert.False(result);
            Assert.Equal("Service name is required.", error);
        }

        [Fact]
        public void TryValidate_MissingExecutablePath_ReturnsFalse()
        {
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = ""
            };
            var xml = Serialize(dto);

            var result = XmlServiceValidator.TryValidate(xml, out var error);
            Assert.False(result);
            Assert.Equal("Executable path is required.", error);
        }

        [Fact]
        public void TryValidate_ValidXml_ReturnsTrue()
        {
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "C:\\path\\to\\exe"
            };
            var xml = Serialize(dto);

            var result = XmlServiceValidator.TryValidate(xml, out var error);
            Assert.True(result);
            Assert.Null(error);
        }
    }
}
