using Servy.Core.DTOs;
using Servy.Core.Validation;

namespace Servy.Core.UnitTests.Validation
{
    public class ServicePathValidatorTests
    {
        private class TestDto
        {
            [ServicePath("executable path", isFile: true, required: true)]
            public string? ExecutablePath { get; set; }

            [ServicePath("startup directory", isFile: false)]
            public string? StartupDirectory { get; set; }
        }

        #region FindFirstViolation Tests

        [Fact]
        public void FindFirstViolation_WhenTargetIsNull_ReturnsNull()
        {
            // Arrange
            TestDto? dto = null;

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) => true);

            // Assert
            Assert.Null(violation);
        }

        [Fact]
        public void FindFirstViolation_WhenRequiredPropertyIsNull_ReturnsMissingViolation()
        {
            // Arrange
            var dto = new TestDto { ExecutablePath = null };

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) => true);

            // Assert
            Assert.NotNull(violation);
            Assert.True(violation.IsMissing);
            Assert.Equal("executable path", violation.Attribute.Label);
        }

        [Fact]
        public void FindFirstViolation_WhenPathIsInvalid_ReturnsInvalidViolation()
        {
            // Arrange
            var dto = new TestDto { ExecutablePath = @"C:\invalid\app.exe" };

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) => false);

            // Assert
            Assert.NotNull(violation);
            Assert.False(violation.IsMissing);
            Assert.Equal(@"C:\invalid\app.exe", violation.Value);
        }

        [Fact]
        public void FindFirstViolation_WhenOptionalPathIsEmpty_ReturnsNull()
        {
            // Arrange
            var dto = new TestDto { ExecutablePath = @"C:\valid\app.exe", StartupDirectory = null };

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) => true);

            // Assert
            Assert.Null(violation);
        }

        [Fact]
        public void FindFirstViolation_WhenStartupDirectoryIsInvalid_PassesIsFileFalseToValidatorAndReturnsViolation()
        {
            // Arrange
            var dto = new TestDto
            {
                ExecutablePath = @"C:\valid\app.exe",
                StartupDirectory = @"C:\invalid\dir"
            };

            bool? receivedIsFile = null;

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) =>
            {
                if (path == dto.StartupDirectory)
                {
                    receivedIsFile = isFile;
                    return false; // Force directory path to fail validation
                }
                return true;
            });

            // Assert
            Assert.NotNull(violation);
            Assert.False(violation.IsMissing);
            Assert.Equal(@"C:\invalid\dir", violation.Value);
            Assert.Equal("startup directory", violation.Attribute.Label);
            Assert.False(receivedIsFile); // Verifies isFile = false was evaluated for StartupDirectory
        }

        [Fact]
        public void FindFirstViolation_WhenStartupDirectoryIsValid_ReturnsNull()
        {
            // Arrange
            var dto = new TestDto
            {
                ExecutablePath = @"C:\valid\app.exe",
                StartupDirectory = @"C:\valid\dir"
            };

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) => true);

            // Assert
            Assert.Null(violation);
        }

        #endregion

        #region FindAllViolations Tests

        [Fact]
        public void FindAllViolations_WhenTargetIsNull_ReturnsEmptyEnumerable()
        {
            // Arrange
            TestDto? dto = null;

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) => true);

            // Assert
            Assert.Empty(violations);
        }

        [Fact]
        public void FindAllViolations_WhenAllPathsAreValid_ReturnsEmptyEnumerable()
        {
            // Arrange
            var dto = new TestDto
            {
                ExecutablePath = @"C:\valid\app.exe",
                StartupDirectory = @"C:\valid\dir"
            };

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) => true);

            // Assert
            Assert.Empty(violations);
        }

        [Fact]
        public void FindAllViolations_WhenOptionalPathIsEmpty_ReturnsEmptyEnumerable()
        {
            // Arrange
            var dto = new TestDto { ExecutablePath = @"C:\valid\app.exe", StartupDirectory = null };

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) => true);

            // Assert
            Assert.Empty(violations);
        }

        [Fact]
        public void FindAllViolations_WhenStartupDirectoryIsInvalid_PassesIsFileFalseToValidatorAndReturnsViolation()
        {
            // Arrange
            var dto = new TestDto
            {
                ExecutablePath = @"C:\valid\app.exe",
                StartupDirectory = @"C:\invalid\dir"
            };

            bool? receivedIsFile = null;

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) =>
            {
                if (path == dto.StartupDirectory)
                {
                    receivedIsFile = isFile;
                    return false; // Force directory path to fail validation
                }
                return true;
            }).ToList();

            // Assert
            var violation = Assert.Single(violations);
            Assert.False(violation.IsMissing);
            Assert.Equal(@"C:\invalid\dir", violation.Value);
            Assert.Equal("startup directory", violation.Attribute.Label);
            Assert.False(receivedIsFile); // Verifies isFile = false was evaluated for StartupDirectory
        }

        [Fact]
        public void FindAllViolations_WhenMultiplePropertiesAreInvalid_ReturnsAllViolations()
        {
            // Arrange
            var dto = new TestDto
            {
                ExecutablePath = null, // Missing required path (Violation 1)
                StartupDirectory = @"C:\invalid\dir" // Invalid path (Violation 2)
            };

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) => false).ToList();

            // Assert
            Assert.Equal(2, violations.Count);

            var firstViolation = violations.FirstOrDefault(v => v.Property.Name == nameof(TestDto.ExecutablePath));
            Assert.NotNull(firstViolation);
            Assert.True(firstViolation.IsMissing);
            Assert.Equal("executable path", firstViolation.Attribute.Label);

            var secondViolation = violations.FirstOrDefault(v => v.Property.Name == nameof(TestDto.StartupDirectory));
            Assert.NotNull(secondViolation);
            Assert.False(secondViolation.IsMissing);
            Assert.Equal(@"C:\invalid\dir", secondViolation.Value);
            Assert.Equal("startup directory", secondViolation.Attribute.Label);
        }

        [Fact]
        public void FindAllViolations_WhenOnlyOnePropertyIsInvalid_ReturnsSingleViolation()
        {
            // Arrange
            var dto = new TestDto
            {
                ExecutablePath = @"C:\valid\app.exe",
                StartupDirectory = @"C:\invalid\dir"
            };

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) => path != @"C:\invalid\dir").ToList();

            // Assert
            var violation = Assert.Single(violations);
            Assert.Equal("startup directory", violation.Attribute.Label);
            Assert.Equal(@"C:\invalid\dir", violation.Value);
            Assert.False(violation.IsMissing);
        }

        #endregion
    }
}