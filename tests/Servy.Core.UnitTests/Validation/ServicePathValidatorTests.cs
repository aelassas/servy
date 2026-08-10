using Servy.Core.DTOs;
using Servy.Core.Validation;
using System.Linq;
using Xunit;

namespace Servy.Core.UnitTests.Validation
{
    public class ServicePathValidatorTests
    {
        private class TestDto
        {
            [ServicePath("executable path", isFile: true, required: true)]
            public string ExecutablePath { get; set; }

            [ServicePath("startup directory", isFile: false)]
            public string StartupDirectory { get; set; }
        }

        #region FindFirstViolation Tests

        [Fact]
        public void FindFirstViolation_WhenTargetIsNull_ReturnsNull()
        {
            // Arrange
            TestDto dto = null;

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) => true);

            // Assert
            Assert.Null(violation);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void FindFirstViolation_WhenRequiredPropertyIsNullOrEmptyOrWhitespace_ReturnsMissingViolationAndSkipsPathValidation(string requiredPath)
        {
            // Arrange
            var dto = new TestDto { ExecutablePath = requiredPath };
            bool pathValidated = false;

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) =>
            {
                pathValidated = true;
                return true;
            });

            // Assert
            Assert.NotNull(violation);
            Assert.True(violation.IsMissing);
            Assert.Equal("executable path", violation.Attribute.Label);
            Assert.False(pathValidated); // Verifies validatePath is never invoked when string is empty or whitespace
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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void FindFirstViolation_WhenOptionalPathIsNullOrEmptyOrWhitespace_ReturnsNullAndSkipsPathValidation(string optionalPath)
        {
            // Arrange
            var dto = new TestDto { ExecutablePath = @"C:\valid\app.exe", StartupDirectory = optionalPath };
            bool optionalPathValidated = false;

            // Act
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) =>
            {
                if (path == optionalPath)
                {
                    optionalPathValidated = true;
                }
                return true;
            });

            // Assert
            Assert.Null(violation);
            Assert.False(optionalPathValidated); // Verifies validatePath is skipped for null/empty/whitespace optional paths
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
            TestDto dto = null;

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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void FindAllViolations_WhenOptionalPathIsNullOrEmptyOrWhitespace_ReturnsEmptyEnumerableAndSkipsPathValidation(string optionalPath)
        {
            // Arrange
            var dto = new TestDto { ExecutablePath = @"C:\valid\app.exe", StartupDirectory = optionalPath };
            bool optionalPathValidated = false;

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) =>
            {
                if (path == optionalPath)
                {
                    optionalPathValidated = true;
                }
                return true;
            });

            // Assert
            Assert.Empty(violations);
            Assert.False(optionalPathValidated); // Verifies validatePath is skipped for null/empty/whitespace optional paths
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
