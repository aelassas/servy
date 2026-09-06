using Servy.Core.Config;
using Servy.Core.Resources;
using Servy.Core.ServiceDependencies;

namespace Servy.Core.UnitTests.ServiceDependencies
{
    public class ServiceDependenciesValidatorTests
    {
        #region Valid Input Verification Pathways

        [Theory]
        [InlineData(null)]                                       // Validate's parameter is string?, and the sibling Parse theory covers null for the same guard
        [InlineData("")]                                         // Validate_EmptyString_ReturnsTrue
        [InlineData("   \r\n   ")]                               // Validate_WhitespaceOnly_ReturnsTrue
        [InlineData("MyService1")]                               // Validate_SingleValidServiceName_ReturnsTrue
        [InlineData("ServiceA;Service_B;AnotherService-1")]      // Validate_MultipleValidNamesSeparatedBySemicolon_ReturnsTrue
        [InlineData("ServiceA\r\nService_B\nAnotherService-1")]  // Validate_MultipleValidNamesSeparatedByNewLines_ReturnsTrue
        [InlineData("ServiceA;;ServiceB\n\nServiceC")]           // Validate_EmptyEntriesBetweenSeparators_AreIgnored
        [InlineData("   ServiceA   ;  ServiceB  ")]              // Validate_NameWithLeadingOrTrailingWhitespace_TrimmedAndValid
        [InlineData(";ServiceA;;ServiceB; ;\n;")]                // Validate_InputWithEmptyEntries_SkipsEmptyEntriesWithoutError
        [InlineData("MSSQL$SQLEXPRESS")]                         // Validate_SingleValidServiceNameWithDollarSign_ReturnsTrue (SQL Server Named Instances)
        [InlineData("clr_optimization_v4.0.30319_32")]           // period is allowed
        [InlineData("My Service;Another Service")]               // internal spaces are allowed (edge spaces are trimmed, internal ones are not)
        [InlineData("+TDI")]                                     // load-order group dependency prefix (+) is allowed
        [InlineData("+NetworkProvider;+Base;+PNP_TDI")]          // multiple load-order group dependencies
        [InlineData("ServiceA;servicea")]                        // a case-insensitive duplicate is collapsed by Tokenize, not reported as an error
        [InlineData("Servy-Café")]                               // non-ASCII unicode letters match creation rule
        [InlineData("My Service (v2)")]                          // parentheses match creation rule
        [InlineData("Backup,Sync")]                              // comma matches creation rule
        [InlineData("net_svc+1")]                                // plus matches creation rule
        [InlineData("데이터베이스")]                              // Korean characters match creation rule
        public void Validate_ValidInput_ReturnsTrueWithNoErrors(string? input)
        {
            // Arrange & Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NameExactlyAtMaximumLength_ReturnsTrue()
        {
            // Arrange
            var input = new string('A', AppConfig.MaxServiceNameLength);

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.True(result);
            Assert.Empty(errors);
        }

        #endregion

        #region Invalid Input Constraint Validation Tests

        [Theory]
        [InlineData(@"My\Service")]
        [InlineData("My/Service")]
        [InlineData("My:Service")]
        [InlineData("My*Service")]
        [InlineData("My?Service")]
        [InlineData("My\"Service")]
        [InlineData("My<Service")]
        [InlineData("My>Service")]
        [InlineData("My|Service")]
        [InlineData("+")]                                         // Sole '+' group prefix with empty name
        public void Validate_NameWithInvalidCharacter_ReturnsFalse(string invalidName)
        {
            // Arrange & Act
            var result = ServiceDependenciesValidator.Validate(invalidName, out var errors);

            // Assert
            Assert.False(result);
            Assert.Single(errors);
            Assert.Contains(errors, e => e.Contains(invalidName));
        }

        [Fact]
        public void Validate_NameExceedingMaximumLength_ReturnsFalse()
        {
            // Arrange
            var tooLongName = new string('A', AppConfig.MaxServiceNameLength + 1);
            var expectedError = string.Format(Strings.Msg_ServiceDependencyNameLengthReachedForName, tooLongName, AppConfig.MaxServiceNameLength);

            // Act
            var result = ServiceDependenciesValidator.Validate(tooLongName, out var errors);

            // Assert
            Assert.False(result);
            Assert.Single(errors);
            Assert.Contains(expectedError, errors);
        }

        [Fact]
        public void Validate_MixedValidAndInvalidNames_ReturnsFalse()
        {
            // Arrange
            // 'MSSQL$SQLEXPRESS' is treated as valid, while 'Bad<Service' and 'Another>Bad' fail
            var input = "MSSQL$SQLEXPRESS;Bad<Service;Another>Bad";

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.False(result);
            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.Contains("Bad<Service"));
            Assert.Contains(errors, e => e.Contains("Another>Bad"));
        }

        [Fact]
        public void Validate_MixedValidExceedingLengthAndInvalidNames_ReturnsFalse()
        {
            // Arrange
            var validName = "ValidService";
            var tooLongName = new string('B', AppConfig.MaxServiceNameLength + 1);
            var invalidCharName = "Bad<Service";
            var input = $"{validName};{tooLongName};{invalidCharName}";
            var expectedLengthError = string.Format(Strings.Msg_ServiceDependencyNameLengthReachedForName, tooLongName, AppConfig.MaxServiceNameLength);

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.False(result);
            Assert.Equal(2, errors.Count);
            Assert.Contains(expectedLengthError, errors);
            Assert.Contains(errors, e => e.Contains("Bad<Service"));
        }

        [Fact]
        public void Validate_DuplicateInvalidName_ReportsItOnce()
        {
            // Arrange
            // Regression guard for #4513: Validate must consume the de-duplicated token stream
            // from ServiceDependenciesParser.Tokenize, not report one error per occurrence.
            var input = "Bad<Service;bad<service;Bad<Service";

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.False(result);
            Assert.Single(errors);
            Assert.Contains(errors, e => e.Contains("Bad<Service"));
        }

        [Fact]
        public void Validate_AllInvalidNames_ReturnsFalse()
        {
            // Arrange
            var input = "Bad<Service;Another>One;With|Symbol";

            // Act
            var result = ServiceDependenciesValidator.Validate(input, out var errors);

            // Assert
            Assert.False(result);
            Assert.Equal(3, errors.Count);
            Assert.Contains(errors, e => e.Contains("Bad<Service"));
            Assert.Contains(errors, e => e.Contains("Another>One"));
            Assert.Contains(errors, e => e.Contains("With|Symbol"));
        }

        #endregion
    }
}
