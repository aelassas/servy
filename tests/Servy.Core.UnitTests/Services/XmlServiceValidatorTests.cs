using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Services;
using Servy.Core.Validators;
using System.Xml.Serialization;

namespace Servy.Core.UnitTests.Services
{
    public class XmlServiceValidatorTests
    {
        private readonly XmlServiceValidator _validator;
        private readonly Mock<IProcessHelper> _processHelperMock;

        public XmlServiceValidatorTests()
        {
            _processHelperMock = new Mock<IProcessHelper>();
            _validator = new XmlServiceValidator(_processHelperMock.Object, new ServiceValidationRules(_processHelperMock.Object));
        }

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
            var result = _validator.TryValidate(null!, out var error);
            Assert.False(result);
            Assert.Equal("XML cannot be empty.", error);

            result = _validator.TryValidate("   ", out error);
            Assert.False(result);
            Assert.Equal("XML cannot be empty.", error);
        }

        [Fact]
        public void TryValidate_InvalidXml_ReturnsFalse()
        {
            // Missing closing tag
            var result = _validator.TryValidate("<ServiceDto><Name>Test</Name>", out var error);
            Assert.False(result);
            Assert.StartsWith("XML structure error:", error);
        }

        [Fact]
        public void TryValidate_XmlNotMatchingServiceDto_ReturnsFalse()
        {
            // Valid XML, but doesn't map to ServiceDto properties
            var xml = "<NotServiceDto><Foo>bar</Foo></NotServiceDto>";
            var result = _validator.TryValidate(xml, out var error);
            Assert.False(result);
            Assert.Contains("XML structure error", error);
        }

        [Fact]
        public void TryValidate_DeserializedDtoIsNull_ReturnsFalse()
        {
            var xml = "<ServiceDto xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />";
            var result = _validator.TryValidate(xml, out var error);
            Assert.False(result);
            Assert.Equal("Failed to deserialize XML.", error);
        }

        [Fact]
        public void TryValidate_DomainValidationFailure_ReturnsFalse()
        {
            // Name length is a Domain check via ServiceValidator.ValidateDto
            var dto = new ServiceDto
            {
                Name = new string('A', AppConfig.MaxServiceNameLength + 1),
                ExecutablePath = "C:\\path\\to\\exe"
            };
            var xml = Serialize(dto);

            var result = _validator.TryValidate(xml, out var error);

            Assert.False(result);
            Assert.Contains("exceeds", error);
        }

        [Theory]
        [InlineData(-1)]
        public void TryValidate_InvalidTimeout_ReturnsFalse(int invalidTimeout)
        {
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\path\\to\\exe",
                StartTimeout = invalidTimeout
            };
            var xml = Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            var result = _validator.TryValidate(xml, out var error);

            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_InvalidStartTimeout, AppConfig.MinStartTimeout, AppConfig.MaxStartTimeout), error);
        }

        [Fact]
        public void TryValidate_InvalidExecutablePath_ReturnsFalse()
        {
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "INVALID_PATH_CHAR_<>|"
            };
            var xml = Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(false);

            var result = _validator.TryValidate(xml, out var error);

            Assert.False(result);
            Assert.Equal(Strings.Msg_InvalidPath, error);
        }

        [Fact]
        public void TryValidate_ValidXml_ReturnsTrue()
        {
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe",
                StopTimeout = 30000
            };
            var xml = Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            var result = _validator.TryValidate(xml, out var error);

            Assert.True(result);
            Assert.Null(error);
        }
    }
}