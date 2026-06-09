using Moq;
using Servy.Core.DTOs;
using Servy.Core.Validation;
using Servy.Manager.Config;
using Servy.Manager.Validation;
using Servy.UI.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.Validation
{
    public class ServiceConfigurationValidatorTests
    {
        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IServiceValidationRules> _validationRulesMock;
        private readonly ServiceConfigurationValidator _validator;

        public ServiceConfigurationValidatorTests()
        {
            _messageBoxServiceMock = new Mock<IMessageBoxService>();
            _validationRulesMock = new Mock<IServiceValidationRules>();

            _validator = new ServiceConfigurationValidator(
                _messageBoxServiceMock.Object,
                _validationRulesMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullMessageBoxService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceConfigurationValidator(null, _validationRulesMock.Object));
        }

        [Fact]
        public void Constructor_NullValidationRules_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceConfigurationValidator(_messageBoxServiceMock.Object, null));
        }

        #endregion

        #region Validate Tests

        [Fact]
        public async Task Validate_WhenConfigurationIsValid_ReturnsTrueAndDoesNotShowMessage()
        {
            // Arrange
            var dto = new ServiceDto();
            var validResult = new ValidationResult { };

            _validationRulesMock
                .Setup(r => r.Validate(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(validResult);

            // Act
            var result = await _validator.ValidateAsync(dto);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m =>
                m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Validate_WhenConfigurationIsInvalid_ShowsErrorAndReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto();
            string expectedError = "Configuration error detected.";
            var invalidResult = new ValidationResult
            {
            };
            invalidResult.Errors.Add(expectedError);
            invalidResult.Errors.Add("Second error that should be ignored");

            _validationRulesMock
                .Setup(r => r.Validate(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(invalidResult);

            // Act
            var result = await _validator.ValidateAsync(dto);

            // Assert
            Assert.False(result);

            // Verify message box was shown with the FIRST error only
            _messageBoxServiceMock.Verify(m =>
                m.ShowErrorAsync(expectedError, AppConfig.Caption), Times.Once);
        }

        #endregion
    }
}