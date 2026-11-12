using Servy.Core.ServiceDependencies;
using System;
using Xunit;

namespace Servy.Core.UnitTests.ServiceDependencies
{
    public class ServiceDependenciesParserTests
    {
        #region No Dependencies & Fallback Validation Pathways

        [Fact]
        public void NoDependencies_IsDoubleNullTerminator()
        {
            // Assert
            Assert.Equal("\0\0", ServiceDependenciesParser.NoDependencies);
        }

        [Theory]
        [InlineData(null)]             // ParseWithRequired_NullInput_ReturnsOnlyRequiredDependency
        [InlineData("")]               // ParseWithRequired_EmptyString_ReturnsOnlyRequiredDependency
        [InlineData("   \r\n  ")]      // ParseWithRequired_WhitespaceOnly_ReturnsOnlyRequiredDependency
        [InlineData(";;\n\n;;")]       // ParseWithRequired_AllEmptyEntries_ReturnsOnlyRequiredDependency
        [InlineData("; ;\t;")]         // every token empties only after the trim, so Tokenize's .Length > 0 filter is what empties the list
        public void ParseWithRequired_InvalidOrEmptyInput_ReturnsOnlyRequiredDependency(string input)
        {
            // Arrange & Act
            var result = ServiceDependenciesParser.ParseWithRequired(input, "Servy");

            // Assert
            Assert.Equal("Servy\0\0", result);
        }

        // Tokenize is public and shared by the validator and the parser. Validate re-checks
        // IsNullOrWhiteSpace and returns before calling it, but ParseWithRequired passes its input
        // straight through, so this guard is what turns a null or blank dependency list into an
        // empty sequence there. This pins that empty-sequence contract with a direct call.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   \r\n  ")]
        public void Tokenize_NullOrWhitespaceInput_ReturnsEmptySequence(string input)
        {
            // Arrange & Act
            var result = ServiceDependenciesParser.Tokenize(input);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region String Tokenization & Null-Separation Formatting Tests

        [Theory]
        [InlineData("ServiceA", "ServiceA\0Servy\0\0")]                                     // ParseWithRequired_SingleName_ReturnsNameWithDoubleNullTermination
        [InlineData("   ServiceA   ", "ServiceA\0Servy\0\0")]                               // ParseWithRequired_SingleNameWithExtraSpaces_ReturnsTrimmed
        [InlineData("ServiceA;ServiceB;ServiceC", "ServiceA\0ServiceB\0ServiceC\0Servy\0\0")] // ParseWithRequired_MultipleNamesSeparatedBySemicolon_ReturnsNullSeparatedString
        [InlineData("ServiceA\r\nServiceB\nServiceC", "ServiceA\0ServiceB\0ServiceC\0Servy\0\0")] // ParseWithRequired_MultipleNamesSeparatedByNewlines_ReturnsNullSeparatedString
        [InlineData(" ServiceA ;\n ServiceB  ;\r\nServiceC ", "ServiceA\0ServiceB\0ServiceC\0Servy\0\0")] // ParseWithRequired_MixedSeparatorsAndExtraSpaces_ReturnsTrimmedAndNullSeparatedString
        [InlineData("ServiceA;;\n\nServiceB;", "ServiceA\0ServiceB\0Servy\0\0")]            // ParseWithRequired_EmptyEntriesBetweenSeparators_AreIgnored
        [InlineData("ServiceA;ServiceB;", "ServiceA\0ServiceB\0Servy\0\0")]                 // ParseWithRequired_TrailingSeparator_StillEndsWithDoubleNull
        [InlineData("ServiceA\rServiceB", "ServiceA\0ServiceB\0Servy\0\0")]                 // ParseWithRequired_MultipleNamesSeparatedByBareCarriageReturn_ReturnsNullSeparatedString
        [InlineData("ServiceA;ServiceA;ServiceB", "ServiceA\0ServiceB\0Servy\0\0")]         // exact duplicate is removed
        [InlineData("ServiceA;servicea;ServiceB", "ServiceA\0ServiceB\0Servy\0\0")]         // duplicate differing only in case is removed (OrdinalIgnoreCase)
        // A token that survives RemoveEmptyEntries but trims to empty. Without Tokenize's
        // .Length > 0 filter it joins to an embedded "\0\0", which the SCM reads as the list
        // terminator, so every dependency after it is silently dropped.
        [InlineData("ServiceA; ;ServiceB", "ServiceA\0ServiceB\0Servy\0\0")]
        [InlineData("ServiceA;\t;ServiceB", "ServiceA\0ServiceB\0Servy\0\0")]
        public void ParseWithRequired_ValidInputs_NormalizesAndNullSeparates(string input, string expected)
        {
            // Arrange & Act
            var result = ServiceDependenciesParser.ParseWithRequired(input, "Servy");

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region ParseWithRequired

        [Theory]
        [InlineData(null, "Servy\0\0")]
        [InlineData("", "Servy\0\0")]
        [InlineData("  ;\n", "Servy\0\0")]
        [InlineData("ServiceA;ServiceB", "ServiceA\0ServiceB\0Servy\0\0")]
        [InlineData("ServiceA;Servy", "ServiceA\0Servy\0\0")]          // already named: not added twice
        [InlineData("SERVY;ServiceA", "SERVY\0ServiceA\0\0")]          // case-insensitive, the configured spelling kept
        public void ParseWithRequired_AlwaysNamesTheRequiredDependencyOnce(string input, string expected)
        {
            // Arrange, Act & Assert
            Assert.Equal(expected, ServiceDependenciesParser.ParseWithRequired(input, "Servy"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseWithRequired_BlankRequiredDependency_Throws(string required)
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => ServiceDependenciesParser.ParseWithRequired("ServiceA", required));
        }

        #endregion
    }
}
