using Moq;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Validation;
using Servy.UI.Services;
using Servy.UI.Validation;

namespace Servy.UI.UnitTests.Validation
{
    /// <summary>
    /// Tests for the shared <see cref="ServiceConfigurationValidator"/>. Both applications use this
    /// one implementation; the caption is the only per-application value and is injected, so the
    /// tests live with the class rather than being mirrored in each application's test project.
    /// </summary>
    public class ServiceConfigurationValidatorTests
    {
        private const string Caption = "Test caption";

        private readonly Mock<IMessageBoxService> _messageBoxServiceMock;
        private readonly Mock<IServiceValidationRules> _validationRulesMock;
        private readonly ServiceConfigurationValidator _validator;

        private readonly Mock<IProcessHelper> _processHelperMock;
        private readonly ServiceValidationRules _realValidationRules;
        private readonly ServiceConfigurationValidator _realRulesValidator;

        public ServiceConfigurationValidatorTests()
        {
            _messageBoxServiceMock = new Mock<IMessageBoxService>();
            _validationRulesMock = new Mock<IServiceValidationRules>();
            _validator = new ServiceConfigurationValidator(
                _messageBoxServiceMock.Object,
                _validationRulesMock.Object,
                Caption);

            _processHelperMock = new Mock<IProcessHelper>();
            _realValidationRules = new ServiceValidationRules(_processHelperMock.Object);
            _realRulesValidator = new ServiceConfigurationValidator(
                _messageBoxServiceMock.Object,
                _realValidationRules,
                Caption);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullMessageBoxService_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ServiceConfigurationValidator(null!, _validationRulesMock.Object, Caption));

            Assert.Equal("messageBoxService", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullValidationRules_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ServiceConfigurationValidator(_messageBoxServiceMock.Object, null!, Caption));

            Assert.Equal("serviceValidationRules", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullCaption_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new ServiceConfigurationValidator(_messageBoxServiceMock.Object, _validationRulesMock.Object, null!));

            Assert.Equal("caption", ex.ParamName);
        }

        #endregion

        #region Caption Tests

        [Fact]
        public async Task ValidateAsync_ValidationFails_ShowsErrorWithInjectedCaption()
        {
            // Arrange
            // The caption is the only per-application value, so a distinctive one proves the
            // forwarding more directly than either application's own constant did.
            const string injectedCaption = "Injected caption";
            var rules = new Mock<IServiceValidationRules>();
            var result = new ValidationResult();
            result.Errors.Add("error");
            rules.Setup(r => r.Validate(It.IsAny<ServiceDto?>(), null, null, false)).Returns(result);
            var messageBox = new Mock<IMessageBoxService>();
            var validator = new ServiceConfigurationValidator(messageBox.Object, rules.Object, injectedCaption);

            // Act
            var isValid = await validator.ValidateAsync(new ServiceDto(), cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(isValid);
            messageBox.Verify(m => m.ShowErrorAsync("error", injectedCaption), Times.Once);
        }

        #endregion

        #region ValidateAsync Tests (mocked rules engine)

        [Fact]
        public async Task ValidateAsync_CancelledToken_ThrowsOperationCanceledExceptionBeforeValidating()
        {
            // Arrange
            var dto = new ServiceDto();

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    _validator.ValidateAsync(dto, cancellationToken: cts.Token));

                // The guard exists to skip the rules engine's credential stage, so pin that it ran first:
                // without this, swapping the guard and the Validate call leaves the test green.
                _validationRulesMock.Verify(
                    r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
                    Times.Never);
                _messageBoxServiceMock.Verify(
                    m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ValidateAsync_WhenConfigurationIsValid_ReturnsTrueAndDoesNotShowMessage(bool importMode)
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
        public async Task ValidateAsync_WhenConfigurationIsInvalid_ShowsErrorAndReturnsFalse(bool importMode)
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
                m.ShowErrorAsync(expectedError, Caption), Times.Once);

            // Pin the fail-fast contract: guarantee no other error dialog is shown to the operator
            _messageBoxServiceMock.Verify(m =>
                m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region ValidateAsync Tests (real rules engine)

        [Fact]
        public async Task ValidateAsync_NullDto_ShowsErrorAndReturnsFalse()
        {
            // Arrange, Act
            var result = await _realRulesValidator.ValidateAsync(null, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(
                It.Is<string>(s => s != null && s.IndexOf(Strings.Msg_ValidationError, StringComparison.OrdinalIgnoreCase) >= 0),
                It.IsAny<string>()
            ), Times.Once);
        }

        [Fact]
        public async Task ValidateAsync_ValidationFails_ShowsErrorAndReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto { Name = "", ExecutablePath = @"C:\Service.exe", RunAsLocalSystem = true };

            // Act
            var result = await _realRulesValidator.ValidateAsync(dto, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);

            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(
                It.Is<string>(s => s != null && s.IndexOf(Strings.Msg_ServiceNameRequired, StringComparison.OrdinalIgnoreCase) >= 0),
                It.IsAny<string>()
            ), Times.Once);
        }

        [Fact]
        public async Task ValidateAsync_ValidationPasses_ReturnsTrue()
        {
            // Arrange: Provide a DTO that passes validation rules
            var dto = new ServiceDto { Name = "ValidService", ExecutablePath = @"C:\ValidService.exe", RunAsLocalSystem = true };

            _processHelperMock.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);

            // Act
            var result = await _realRulesValidator.ValidateAsync(dto, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ValidateAsync_PasswordMismatch_ShowsErrorAndReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "ValidService",
                ExecutablePath = @"C:\ValidService.exe",
                RunAsLocalSystem = false,
                Password = "Password123"
            };

            _processHelperMock.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);

            // Act: Pass a confirmPassword parameter that explicitly mismatches the target DTO secret string
            var result = await _realRulesValidator.ValidateAsync(dto, confirmPassword: "DifferentPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(
                It.Is<string>(s => s != null && s.IndexOf(Strings.Msg_PasswordsDontMatch, StringComparison.OrdinalIgnoreCase) >= 0),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ValidateAsync_WithExplicitWrapperExePath_EvaluatesRulesAndReturnsTrue()
        {
            // Arrange
            var dto = new ServiceDto { Name = "ValidService", ExecutablePath = @"C:\ValidService.exe", RunAsLocalSystem = true };
            string wrapperExePath = @"C:\Servy\Servy.Service.exe";

            _processHelperMock.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);
            _processHelperMock.Setup(p => p.ValidatePath(wrapperExePath, true)).Returns(true);

            // Act: pass an explicit wrapperExePath so the wrapper-path validation rule is evaluated
            var result = await _realRulesValidator.ValidateAsync(dto, wrapperExePath: wrapperExePath, confirmPassword: null, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ValidateAsync_WithInvalidWrapperExePath_ForwardsPathAndReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto { Name = "ValidService", ExecutablePath = @"C:\ValidService.exe", RunAsLocalSystem = true };
            string wrapperExePath = @"C:\Servy\Missing.exe";

            _processHelperMock.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);
            _processHelperMock.Setup(p => p.ValidatePath(wrapperExePath, true)).Returns(false);

            // Act: the wrapper-path rule can only fire if the argument is still forwarded
            var result = await _realRulesValidator.ValidateAsync(dto, wrapperExePath: wrapperExePath, confirmPassword: null, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(
                It.Is<string>(s => s != null && s.IndexOf(Strings.Msg_InvalidWrapperExePath, StringComparison.OrdinalIgnoreCase) >= 0),
                It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region ImportMode Tests

        [Fact]
        public async Task ValidateAsync_ImportModeTrue_SkipsCredentialValidation()
        {
            // Arrange: non-LocalSystem DTO with password mismatch that would fail credential validation if importMode was false
            var dto = new ServiceDto
            {
                Name = "ValidService",
                ExecutablePath = @"C:\ValidService.exe",
                RunAsLocalSystem = false,
                UserAccount = @".\nonexistent-user",
                Password = "Password123"
            };

            _processHelperMock.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);

            // Act: pass importMode: true along with a mismatching confirmPassword
            var result = await _realRulesValidator.ValidateAsync(
                dto,
                confirmPassword: "DifferentPassword",
                importMode: true,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ValidateAsync_ImportModeFalse_EnforcesCredentialValidation()
        {
            // Arrange: identical DTO setup with password mismatch
            var dto = new ServiceDto
            {
                Name = "ValidService",
                ExecutablePath = @"C:\ValidService.exe",
                RunAsLocalSystem = false,
                UserAccount = @".\nonexistent-user",
                Password = "Password123"
            };

            _processHelperMock.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);

            // Act: pass importMode: false with mismatching confirmPassword
            var result = await _realRulesValidator.ValidateAsync(
                dto,
                confirmPassword: "DifferentPassword",
                importMode: false,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _messageBoxServiceMock.Verify(m => m.ShowErrorAsync(
                It.Is<string>(s => s != null && s.IndexOf(Strings.Msg_PasswordsDontMatch, StringComparison.OrdinalIgnoreCase) >= 0),
                It.IsAny<string>()), Times.Once);
        }

        #endregion
    }
}
