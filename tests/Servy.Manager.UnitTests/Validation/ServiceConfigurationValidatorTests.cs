using Moq;
using Servy.Core.DTOs;
using Servy.Core.Validation;
using Servy.Manager.Config;
using Servy.Manager.Validation;
using Servy.UI.Services;

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
                new ServiceConfigurationValidator(null!, _validationRulesMock.Object));
        }

        [Fact]
        public void Constructor_NullValidationRules_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceConfigurationValidator(_messageBoxServiceMock.Object, null!));
        }

        #endregion

        #region Validate Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Validate_WhenConfigurationIsValid_ReturnsTrueAndDoesNotShowMessage(bool importMode)
        {
            // Arrange
            var dto = new ServiceDto();
            var validResult = new ValidationResult();

            _validationRulesMock
                .Setup(r => r.Validate(dto, null, null, importMode))
                .Returns(validResult);

            // Act
            var result = await _validator.ValidateAsync(dto, importMode: importMode, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _validationRulesMock.Verify(r => r.Validate(dto, null, null, importMode), Times.Once);
            _messageBoxServiceMock.Verify(m =>
                m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Validate_WhenConfigurationIsInvalid_ShowsErrorAndReturnsFalse(bool importMode)
        {
            // Arrange
            var dto = new ServiceDto();
            string expectedError = "Configuration error detected.";
            var invalidResult = new ValidationResult();
            invalidResult.Errors.Add(expectedError);
            invalidResult.Errors.Add("Second error that should be ignored");

            _validationRulesMock
                .Setup(r => r.Validate(dto, null, null, importMode))
                .Returns(invalidResult);

            // Act
            var result = await _validator.ValidateAsync(dto, importMode: importMode, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _validationRulesMock.Verify(r => r.Validate(dto, null, null, importMode), Times.Once);

            // Verify message box was shown with the FIRST error only
            _messageBoxServiceMock.Verify(m =>
                m.ShowErrorAsync(expectedError, UiAppConfig.Caption), Times.Once);

            // Pin the fail-fast contract: guarantee no other error dialog is shown to the operator
            _messageBoxServiceMock.Verify(m =>
                m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        #endregion
    }
}