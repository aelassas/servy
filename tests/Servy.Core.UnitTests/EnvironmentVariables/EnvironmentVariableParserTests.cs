using Servy.Core.EnvironmentVariables;

namespace Servy.Core.UnitTests.EnvironmentVariables
{
    public class EnvironmentVariableParserTests
    {
        [Fact]
        public void Parse_EmptyString_ReturnsEmptyList()
        {
            // Arrange & Act
            var result = EnvironmentVariableParser.Parse("");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_SingleVariable_ParsesCorrectly()
        {
            // Arrange
            var input = "KEY=VALUE";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VALUE", result[0].Value);
        }

        [Fact]
        public void Parse_MultipleVariablesSeparatedBySemicolon_ParsesCorrectly()
        {
            // Arrange
            var input = "KEY1=VALUE1;KEY2=VALUE2;KEY3=\"VALUE3\";KEY4= \"VALUE4\" ;KEY5=  VALUE5 ; KEY6 = \" VALUE6 \"";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Equal(6, result.Count);
            Assert.Equal("KEY1", result[0].Name);
            Assert.Equal("VALUE1", result[0].Value);
            Assert.Equal("KEY2", result[1].Name);
            Assert.Equal("VALUE2", result[1].Value);
            Assert.Equal("KEY3", result[2].Name);
            Assert.Equal("VALUE3", result[2].Value);
            Assert.Equal("KEY4", result[3].Name);
            Assert.Equal("VALUE4", result[3].Value);
            Assert.Equal("KEY5", result[4].Name);
            Assert.Equal("VALUE5", result[4].Value);
            Assert.Equal("KEY6", result[5].Name);
            Assert.Equal(" VALUE6 ", result[5].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedSemicolonInValue()
        {
            // Arrange
            var input = "KEY1=VALUE\\;WITHSEMICOLON;KEY2=OK";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Equal("VALUE;WITHSEMICOLON", result[0].Value);
            Assert.Equal("OK", result[1].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedEqualsInKey()
        {
            // Arrange
            var input = "K\\=EY=VAL";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("K=EY", result[0].Name);
            Assert.Equal("VAL", result[0].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedEqualsInValue()
        {
            // Arrange
            var input = "KEY=VAL\\=UE";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VAL=UE", result[0].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedDoubleQuotesInValue()
        {
            // Arrange
            var input = "KEY=VAL\\\"UE";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VAL\"UE", result[0].Value);

            // Arrange (Nested Variant)
            input = "KEY=\"\\\"VAL\\\"UE\\\"\"";

            // Act
            result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("\"VAL\"UE\"", result[0].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedBackslash()
        {
            // Arrange
            var input = "KEY=VAL\\\\UE";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VAL\\UE", result[0].Value);
        }

        [Fact]
        public void Parse_UnknownEscapeSequence_PreservesBackslash()
        {
            // Arrange
            var input = "KEY=VAL\\XUE";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("VAL\\XUE", result[0].Value);
        }

        [Fact]
        public void Parse_TrailingBackslash_PreservesBackslash()
        {
            // Arrange
            var input = "KEY=VALUE\\";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("VALUE\\", result[0].Value);
        }

        [Fact]
        public void Parse_EmptyKey_ThrowsFormatException()
        {
            // Arrange
            var input = "=VALUE";

            // Act & Assert
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));
            Assert.Contains("Environment variable key cannot be empty", ex.Message);
        }

        [Theory]
        [InlineData(@"KEY\=NOEQUAL")]
        [InlineData(@"KEY\\\=NOEQUAL")]
        public void Parse_NoUnescapedEquals_ThrowsFormatException(string input)
        {
            // Arrange & Act
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));

            // Assert
            Assert.Contains("no unescaped '='", ex.Message);
        }

        [Theory]
        [InlineData(@"KEY\\=NOEQUAL")]
        [InlineData(@"KEY\\\\=NOEQUAL")]
        public void Parse_EscapeBackslash(string input)
        {
            // Arrange & Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("NOEQUAL", result[0].Value);
        }

        [Fact]
        public void Parse_IgnoresEmptySegments()
        {
            // Arrange
            var input = "KEY1=VAL1;;KEY2=VAL2;";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("KEY1", result[0].Name);
            Assert.Equal("KEY2", result[1].Name);
        }

        [Theory]
        [InlineData("KEY=\"hello\"", "hello")]           // Standard structural quotes
        [InlineData("KEY= \"hello\" ", "hello")]         // Structural quotes with surrounding whitespace
        [InlineData("KEY='hello'", "'hello'")]           // Single quotes are NOT structural; preserved literally
        [InlineData("KEY=\"hello", "\"hello")]           // Unmatched quotes are preserved literally
        public void Parse_StructuralQuotes_Behavior(string input, string expectedValue)
        {
            // Arrange & Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Equal(expectedValue, result[0].Value);
        }

        [Theory]
        [InlineData("KEY=\\\"hello\\\"", "\"hello\"")]
        [InlineData("KEY=\\\"\\\"", "\"\"")]
        [InlineData("KEY= \"hello\" ", "hello")]
        [InlineData("KEY=\"\\\"hello\\\"\"", "\"hello\"")]
        public void Parse_LiteralQuotes_PreservedWhenEscaped(string input, string expectedValue)
        {
            // Arrange & Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Equal(expectedValue, result[0].Value);
        }

        [Fact]
        public void Parse_NestedQuotes_PreservedWhenOuterAreStructural()
        {
            // Arrange
            var input = "KEY=\"\\\"hello\\\"\"";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Single(result);
            Assert.Equal("\"hello\"", result[0].Value);
        }

        [Fact]
        public void Parse_HandlesComplexEscapingWithQuotes()
        {
            // Arrange
            var input = "KEY=\"Value\\;WithSemicolon\";KEY2=NEXT";

            // Act
            var result = EnvironmentVariableParser.Parse(input);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Value;WithSemicolon", result[0].Value);
            Assert.Equal("NEXT", result[1].Value);
        }

        [Theory]
        [InlineData("KEY=Line1\\\nLine2", "KEY")]
        [InlineData("KEY=Line1\\\rLine2", "KEY")]
        [InlineData("KEY=Line1\\\r\\\nLine2", "KEY")]
        public void Parse_ValueContainsForbiddenNewline_ThrowsFormatException(string input, string expectedKeyInMessage)
        {
            // Arrange & Act
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));

            // Assert
            Assert.Contains($"Environment variable '{expectedKeyInMessage}' contains a forbidden newline character", ex.Message);
            Assert.Contains("Multi-line values are not supported", ex.Message);
        }

        [Theory]
        [InlineData("KEY=Line1\nLine2", "Line2")]
        [InlineData("KEY=Line1\rLine2", "Line2")]
        [InlineData("KEY=Line1\r\nLine2", "Line2")]
        public void Parse_UnquotedRawNewline_ThrowsStructuralFormatException(string input, string expectedFragmentInMessage)
        {
            // Arrange & Act
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));

            // Assert
            Assert.Contains($"no unescaped '='", ex.Message);
            Assert.Contains(expectedFragmentInMessage, ex.Message);
        }
    }
}