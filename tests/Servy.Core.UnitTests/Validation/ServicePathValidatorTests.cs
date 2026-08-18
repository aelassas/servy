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

        [Fact]
        public void FindFirstViolation_WhenMultiplePropertiesAreInvalid_ReturnsFirstPropertyInDeclarationOrder()
        {
            // Arrange: Provide a DTO with multiple simultaneously invalid paths.
            // ExecutablePath is declared before StartupDirectory in TestDto metadata.
            var dto = new TestDto
            {
                ExecutablePath = @"C:\invalid\app.exe",
                StartupDirectory = @"C:\invalid\dir"
            };

            // Act: Evaluate violations when both path checks return false.
            var violation = ServicePathValidator.FindFirstViolation(dto, (path, isFile) => false);

            // Assert: Verify that the violation for the first-declared property (ExecutablePath) is returned.
            Assert.NotNull(violation);
            Assert.Equal(nameof(TestDto.ExecutablePath), violation.Property.Name);
            Assert.Equal("executable path", violation.Attribute.Label);
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

        [Fact]
        public void FindAllViolations_WhenMultiplePropertiesAreInvalid_ReturnsAllViolationsInDeclarationOrder()
        {
            // Arrange
            var dto = new TestDto
            {
                ExecutablePath = null, // Missing required path (Violation 1)
                StartupDirectory = @"C:\invalid\dir" // Invalid path (Violation 2)
            };

            // Act
            var violations = ServicePathValidator.FindAllViolations(dto, (path, isFile) => false).ToList();

            // Assert: Verify that violations are yielded strictly in property MetadataToken declaration order
            Assert.Equal(2, violations.Count);

            Assert.Equal(nameof(TestDto.ExecutablePath), violations[0].Property.Name);
            Assert.True(violations[0].IsMissing);
            Assert.Equal("executable path", violations[0].Attribute.Label);

            Assert.Equal(nameof(TestDto.StartupDirectory), violations[1].Property.Name);
            Assert.False(violations[1].IsMissing);
            Assert.Equal(@"C:\invalid\dir", violations[1].Value);
            Assert.Equal("startup directory", violations[1].Attribute.Label);
        }

        #endregion
    }
}
