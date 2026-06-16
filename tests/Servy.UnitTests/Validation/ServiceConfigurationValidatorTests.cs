using Moq;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Validation;
using Servy.UI.Services;
using Servy.Validation;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Servy.UnitTests.Validation
{
    public class ServiceConfigurationValidatorTests
    {
        private readonly Mock<IMessageBoxService> _mockMessageBox;
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly ServiceValidationRules _validationRules;
        private readonly ServiceConfigurationValidator _validator;

        public ServiceConfigurationValidatorTests()
        {
            _mockMessageBox = new Mock<IMessageBoxService>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _validationRules = new ServiceValidationRules(_mockProcessHelper.Object);
            _validator = new ServiceConfigurationValidator(_mockMessageBox.Object, _validationRules);
        }

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceConfigurationValidator(null, _validationRules));
            Assert.Throws<ArgumentNullException>(() => new ServiceConfigurationValidator(_mockMessageBox.Object, null));
        }

        [Fact]
        public async Task Validate_NullDto_ReturnsFalse()
        {
            var result = await _validator.ValidateAsync(null);
            Assert.False(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Validate_ValidationFails_ShowsErrorAndReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto(); // Assume this creates an invalid state
                                        // This relies on the actual logic within ServiceValidationRules.Validate

            // Act
            var result = await _validator.ValidateAsync(dto);

            // Assert
            Assert.False(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Validate_ValidationPasses_ReturnsTrue()
        {
            // Arrange: Provide a DTO that passes validation rules
            var dto = new ServiceDto { Name = "ValidService", ExecutablePath = @"C:\ValidService.exe" };

            _mockProcessHelper.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);

            // Act
            var result = await _validator.ValidateAsync(dto);

            // Assert
            Assert.True(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}