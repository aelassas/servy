using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Services;
using Servy.Core.UnitTests.Helpers;
using Servy.Core.Validation;

namespace Servy.Core.UnitTests.Services
{
    public class XmlServiceValidatorTests
    {
        private readonly XmlServiceValidator _validator;
        private readonly Mock<IProcessHelper> _processHelperMock;

        public XmlServiceValidatorTests()
        {
            _processHelperMock = new Mock<IProcessHelper>();
            _validator = new XmlServiceValidator(new ServiceValidationRules(_processHelperMock.Object));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryValidate_NullOrWhitespaceXml_ReturnsFalse(string? xml)
        {
            // Arrange
            var expectedError = string.Format(Strings.Msg_ImportInputEmptyOrWhitespace, "XML");

            // Act
            var result = _validator.TryValidate(null, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedError, error);

            // Act
            result = _validator.TryValidate(xml, out error);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedError, error);
        }

        [Fact]
        public void TryValidate_InvalidXml_ReturnsFalse()
        {
            // Arrange
            // Missing closing tag
            var invalidXml = "<ServiceDto><Name>Test</Name>";
            var expectedPrefix = string.Format(Strings.Msg_ImportInvalidStructure, "XML", string.Empty).TrimEnd();

            // Act
            var result = _validator.TryValidate(invalidXml, out var error);

            // Assert
            Assert.False(result);
            Assert.StartsWith(expectedPrefix, error);
        }

        [Fact]
        public void TryValidate_XmlNotMatchingServiceDto_ReturnsFalse()
        {
            // Arrange
            // Valid XML, but doesn't map to ServiceDto properties
            var xml = "<NotServiceDto><Foo>bar</Foo></NotServiceDto>";
            var expectedPrefix = string.Format(Strings.Msg_ImportStructureError, "XML", string.Empty).TrimEnd();

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Contains(expectedPrefix, error);
        }

        [Fact]
        public void TryValidate_DeserializedDtoIsNull_ReturnsFalse()
        {
            // Arrange
            var xml = "<ServiceDto xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />";
            var expectedError = string.Format(Strings.Msg_ImportEmptyDefinition, "XML");

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedError, error);
        }

        [Fact]
        public void TryValidate_DomainValidationFailure_ReturnsFalse()
        {
            // Arrange
            // Name length is a Domain check via ServiceValidator.ValidateDto
            var dto = new ServiceDto
            {
                Name = new string('A', AppConfig.MaxServiceNameLength + 1),
                ExecutablePath = "C:\\path\\to\\exe"
            };
            var xml = ServiceDtoXml.Serialize(dto);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Contains(string.Format(Strings.Msg_ServiceNameLengthReached, AppConfig.MaxServiceNameLength), error);
        }

        [Theory]
        [InlineData(-1)]
        public void TryValidate_InvalidTimeout_ReturnsFalse(int invalidTimeout)
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\path\\to\\exe",
                StartTimeout = invalidTimeout
            };
            var xml = ServiceDtoXml.Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_InvalidStartTimeout, AppConfig.MinStartTimeout, AppConfig.MaxStartTimeout), error);
        }

        [Fact]
        public void TryValidate_InvalidExecutablePath_ReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "INVALID_PATH_CHAR_<>|"
            };
            var xml = ServiceDtoXml.Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(false);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(Strings.Msg_InvalidPath, error);
        }

        [Fact]
        public void TryValidate_ValidXml_ReturnsTrue()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe",
                StopTimeout = 30000
            };
            var xml = ServiceDtoXml.Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.True(result);
            Assert.Null(error);
        }
    }
}