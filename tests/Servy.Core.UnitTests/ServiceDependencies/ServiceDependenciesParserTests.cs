using Servy.Core.ServiceDependencies;

namespace Servy.Core.UnitTests.ServiceDependencies
{
    public class ServiceDependenciesParserTests
    {
        #region No Dependencies & Fallback Validation Pathways

        [Theory]
        [InlineData(null)]             // Parse_NullInput_ReturnsNoDependencies
        [InlineData("")]               // Parse_EmptyString_ReturnsNoDependencies
        [InlineData("   \r\n  ")]      // Parse_WhitespaceOnly_ReturnsNoDependencies
        [InlineData(";;\n\n;;")]       // Parse_AllEmptyEntries_ReturnsNoDependencies
        public void Parse_InvalidOrEmptyInput_ReturnsNoDependenciesSentinel(string? input)
        {
            // Arrange & Act
            var result = ServiceDependenciesParser.Parse(input);

            // Assert
            Assert.Equal(ServiceDependenciesParser.NoDependencies, result);
        }

        #endregion

        #region String Tokenization & Null-Separation Formatting Tests

        [Theory]
        [InlineData("ServiceA", "ServiceA\0\0")]                                     // Parse_SingleName_ReturnsNameWithDoubleNullTermination
        [InlineData("   ServiceA   ", "ServiceA\0\0")]                               // Parse_SingleNameWithExtraSpaces_ReturnsTrimmed
        [InlineData("ServiceA;ServiceB;ServiceC", "ServiceA\0ServiceB\0ServiceC\0\0")] // Parse_MultipleNamesSeparatedBySemicolon_ReturnsNullSeparatedString
        [InlineData("ServiceA\r\nServiceB\nServiceC", "ServiceA\0ServiceB\0ServiceC\0\0")] // Parse_MultipleNamesSeparatedByNewlines_ReturnsNullSeparatedString
        [InlineData(" ServiceA ;\n ServiceB  ;\r\nServiceC ", "ServiceA\0ServiceB\0ServiceC\0\0")] // Parse_MixedSeparatorsAndExtraSpaces_ReturnsTrimmedAndNullSeparatedString
        [InlineData("ServiceA;;\n\nServiceB;", "ServiceA\0ServiceB\0\0")]             // Parse_EmptyEntriesBetweenSeparators_AreIgnored
        [InlineData("ServiceA;ServiceB;", "ServiceA\0ServiceB\0\0")]                 // Parse_TrailingSeparator_StillEndsWithDoubleNull
        [InlineData("ServiceA\rServiceB", "ServiceA\0ServiceB\0\0")]                 // Parse_MultipleNamesSeparatedByBareCarriageReturn_ReturnsNullSeparatedString
        public void Parse_ValidInputs_NormalizesAndNullSeparates(string input, string expected)
        {
            // Arrange & Act
            var result = ServiceDependenciesParser.Parse(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}