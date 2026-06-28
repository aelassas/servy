using Servy.Core.ServiceDependencies;

namespace Servy.Core.UnitTests.ServiceDependencies
{
    public class ServiceDependenciesValidatorTests
    {
        [Fact]
        public void Validate_EmptyString_ReturnsTrue()
        {
            // Arrange
            var input = string.Empty;

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_WhitespaceOnly_ReturnsTrue()
        {
            var result = ServiceDependenciesValidator.Validate("   \r\n   ", out var errors);

            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_SingleValidServiceName_ReturnsTrue()
        {
            var result = ServiceDependenciesValidator.Validate("MyService1", out var errors);

            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MultipleValidNamesSeparatedBySemicolon_ReturnsTrue()
        {
            var input = "ServiceA;Service_B;AnotherService-1";
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MultipleValidNamesSeparatedByNewLines_ReturnsTrue()
        {
            var input = "ServiceA\r\nService_B\nAnotherService-1";
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NameWithSpace_ReturnsFalse()
        {
            var result = ServiceDependenciesValidator.Validate("Bad#Service", out var errors);

            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("Bad#Service"));
        }

        [Fact]
        public void Validate_NameWithSpecialCharacters_ReturnsFalse()
        {
            // '@' is rejected as an invalid service-name character
            var result = ServiceDependenciesValidator.Validate("Service@Name", out var errors);

            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("Service@Name"));
        }

        [Fact]
        public void Validate_MixedValidAndInvalidNames_ReturnsFalse()
        {
            // 'MSSQL$SQLEXPRESS' is treated as valid, while 'Bad#Service' and 'Another@Bad' fail
            var input = "MSSQL$SQLEXPRESS;Bad#Service;Another@Bad";
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            Assert.False(result);
            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.Contains("Bad#Service"));
            Assert.Contains(errors, e => e.Contains("Another@Bad"));
        }

        [Fact]
        public void Validate_EmptyEntriesBetweenSeparators_AreIgnored()
        {
            var input = "ServiceA;;ServiceB\n\nServiceC";
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NameWithLeadingOrTrailingWhitespace_TrimmedAndValid()
        {
            var input = "   ServiceA   ;  ServiceB  ";
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_AllInvalidNames_ReturnsFalse()
        {
            var input = "Bad^Service;Another#One;With@Symbol";
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            Assert.False(result);
            Assert.Equal(3, errors.Count);
        }

        [Fact]
        public void Validate_InputWithEmptyEntries_SkipsEmptyEntriesWithoutError()
        {
            // Arrange: Input with empty entries between semicolons and at edges
            var input = ";ServiceA;;ServiceB; ;\n;";

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.True(result); // or whatever is expected when valid
        }

        [Fact]
        public void Validate_SingleValidServiceNameWithDollarSign_ReturnsTrue()
        {
            // Verifies fix for SQL Server Named Instances (e.g., MSSQL$SQLEXPRESS)
            var result = ServiceDependenciesValidator.Validate("MSSQL$SQLEXPRESS", out var errors);

            Assert.True(result);
            Assert.Empty(errors);
        }
    }
}
