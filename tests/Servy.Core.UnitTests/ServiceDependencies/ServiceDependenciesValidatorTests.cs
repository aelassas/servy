using Servy.Core.ServiceDependencies;

namespace Servy.Core.UnitTests.ServiceDependencies
{
    public class ServiceDependenciesValidatorTests
    {
        #region Valid Input Verification Pathways

        [Theory]
        [InlineData("")]                                         // Validate_EmptyString_ReturnsTrue
        [InlineData("   \r\n   ")]                               // Validate_WhitespaceOnly_ReturnsTrue
        [InlineData("MyService1")]                               // Validate_SingleValidServiceName_ReturnsTrue
        [InlineData("ServiceA;Service_B;AnotherService-1")]      // Validate_MultipleValidNamesSeparatedBySemicolon_ReturnsTrue
        [InlineData("ServiceA\r\nService_B\nAnotherService-1")]  // Validate_MultipleValidNamesSeparatedByNewLines_ReturnsTrue
        [InlineData("ServiceA;;ServiceB\n\nServiceC")]           // Validate_EmptyEntriesBetweenSeparators_AreIgnored
        [InlineData("   ServiceA   ;  ServiceB  ")]              // Validate_NameWithLeadingOrTrailingWhitespace_TrimmedAndValid
        [InlineData(";ServiceA;;ServiceB; ;\n;")]                // Validate_InputWithEmptyEntries_SkipsEmptyEntriesWithoutError
        [InlineData("MSSQL$SQLEXPRESS")]                         // Validate_SingleValidServiceNameWithDollarSign_ReturnsTrue (SQL Server Named Instances)
        public void Validate_ValidInput_ReturnsTrueWithNoErrors(string input)
        {
            // Arrange & Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.True(result);
            Assert.Empty(errors);
        }

        #endregion

        #region Invalid Input Constraint Validation Tests

        [Fact]
        public void Validate_NameWithInvalidSpecialCharacter_ReturnsFalse()
        {
            // Arrange & Act
            // Tests that a name containing an invalid special character like '#' is rejected
            var result = ServiceDependenciesValidator.Validate("Bad#Service", out var errors);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("Bad#Service"));
        }

        [Fact]
        public void Validate_NameWithSpecialCharacters_ReturnsFalse()
        {
            // Arrange
            // '@' is rejected as an invalid service-name character

            // Act
            var result = ServiceDependenciesValidator.Validate("Service@Name", out var errors);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("Service@Name"));
        }

        [Fact]
        public void Validate_MixedValidAndInvalidNames_ReturnsFalse()
        {
            // Arrange
            // 'MSSQL$SQLEXPRESS' is treated as valid, while 'Bad#Service' and 'Another@Bad' fail
            var input = "MSSQL$SQLEXPRESS;Bad#Service;Another@Bad";

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.False(result);
            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.Contains("Bad#Service"));
            Assert.Contains(errors, e => e.Contains("Another@Bad"));
        }

        [Fact]
        public void Validate_AllInvalidNames_ReturnsFalse()
        {
            // Arrange
            var input = "Bad^Service;Another#One;With@Symbol";

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.False(result);
            Assert.Equal(3, errors.Count);
            Assert.Contains(errors, e => e.Contains("Bad^Service"));
            Assert.Contains(errors, e => e.Contains("Another#One"));
            Assert.Contains(errors, e => e.Contains("With@Symbol"));
        }

        #endregion
    }
}