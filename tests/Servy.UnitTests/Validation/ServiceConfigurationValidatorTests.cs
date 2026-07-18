using Moq;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Validation;
using Servy.UI.Services;
using Servy.Validation;

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
            Assert.Throws<ArgumentNullException>(() => new ServiceConfigurationValidator(null!, _validationRules));
            Assert.Throws<ArgumentNullException>(() => new ServiceConfigurationValidator(_mockMessageBox.Object, null!));
        }

        [Fact]
        public async Task Validate_NullDto_ReturnsFalse()
        {
            var result = await _validator.ValidateAsync(null, cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Validate_ValidationFails_ShowsErrorAndReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto { Name = "", ExecutablePath = @"C:\Service.exe", RunAsLocalSystem = true };

            // Act
            var result = await _validator.ValidateAsync(dto, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);

            _mockMessageBox.Verify(m => m.ShowErrorAsync(
                It.Is<string>(s => s != null && s.IndexOf(Strings.Msg_ServiceNameRequired, StringComparison.OrdinalIgnoreCase) >= 0),
                It.IsAny<string>()
            ), Times.Once);
        }

        [Fact]
        public async Task Validate_ValidationPasses_ReturnsTrue()
        {
            // Arrange: Provide a DTO that passes validation rules
            var dto = new ServiceDto { Name = "ValidService", ExecutablePath = @"C:\ValidService.exe", RunAsLocalSystem = true };

            _mockProcessHelper.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);

            // Act
            var result = await _validator.ValidateAsync(dto, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

            _mockProcessHelper.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);

            // Act: Pass a confirmPassword parameter that explicitly mismatches the target DTO secret string
            var result = await _validator.ValidateAsync(dto, confirmPassword: "DifferentPassword", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ValidateAsync_WithExplicitWrapperExePath_EvaluatesRulesAndReturnsTrue()
        {
            // Arrange
            var dto = new ServiceDto { Name = "ValidService", ExecutablePath = @"C:\ValidService.exe", RunAsLocalSystem = true };
            string wrapperExePath = @"C:\Servy\Servy.Service.exe";

            _mockProcessHelper.Setup(p => p.ValidatePath(dto.ExecutablePath, true)).Returns(true);
            _mockProcessHelper.Setup(p => p.ValidatePath(wrapperExePath, true)).Returns(true);

            // Act: Exercise forwarding boundary loops by explicitly passing both wrapper paths and identical password structures
            var result = await _validator.ValidateAsync(dto, wrapperExePath: wrapperExePath, confirmPassword: null, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            _mockMessageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}