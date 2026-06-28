using Servy.Core.EnvironmentVariables;
using System;
using Xunit;

namespace Servy.Core.UnitTests.EnvironmentVariables
{
    public class EnvironmentVariableParserTests
    {
        [Fact]
        public void Parse_EmptyString_ReturnsEmptyList()
        {
            var result = EnvironmentVariableParser.Parse("");
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_SingleVariable_ParsesCorrectly()
        {
            var input = "KEY=VALUE";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VALUE", result[0].Value);
        }

        [Fact]
        public void Parse_MultipleVariablesSeparatedBySemicolon_ParsesCorrectly()
        {
            var input = "KEY1=VALUE1;KEY2=VALUE2;KEY3=\"VALUE3\";KEY4= \"VALUE4\" ;KEY5=  VALUE5 ; KEY6 = \" VALUE6 \"";
            var result = EnvironmentVariableParser.Parse(input);

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
            var input = "KEY1=VALUE\\;WITHSEMICOLON;KEY2=OK";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal("VALUE;WITHSEMICOLON", result[0].Value);
            Assert.Equal("OK", result[1].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedEqualsInKey()
        {
            var input = "K\\=EY=VAL";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("K=EY", result[0].Name);
            Assert.Equal("VAL", result[0].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedEqualsInValue()
        {
            var input = "KEY=VAL\\=UE";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VAL=UE", result[0].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedDoubleQuotesInValue()
        {
            var input = "KEY=VAL\\\"UE";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VAL\"UE", result[0].Value);

            input = "KEY=\"\\\"VAL\\\"UE\\\"\"";
            result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("\"VAL\"UE\"", result[0].Value);
        }

        [Fact]
        public void Parse_SupportsEscapedBackslash()
        {
            var input = "KEY=VAL\\\\UE";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal("VAL\\UE", result[0].Value);
        }

        [Fact]
        public void Parse_UnknownEscapeSequence_PreservesBackslash()
        {
            var input = "KEY=VAL\\XUE";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("VAL\\XUE", result[0].Value);
        }

        [Fact]
        public void Parse_TrailingBackslash_PreservesBackslash()
        {
            var input = "KEY=VALUE\\";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("VALUE\\", result[0].Value);
        }

        [Fact]
        public void Parse_EmptyKey_ThrowsFormatException()
        {
            var input = "=VALUE";
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));
            Assert.Contains("Environment variable key cannot be empty", ex.Message);
        }

        [Theory]
        [InlineData(@"KEY\=NOEQUAL")]
        [InlineData(@"KEY\\\=NOEQUAL")]
        public void Parse_NoUnescapedEquals_ThrowsFormatException(string input)
        {
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));
            Assert.Contains("no unescaped '='", ex.Message);
        }

        [Theory]
        [InlineData(@"KEY\\=NOEQUAL")]
        [InlineData(@"KEY\\\\=NOEQUAL")]
        public void Parse_EscapeBackslash(string input)
        {
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("NOEQUAL", result[0].Value);
        }

        [Fact]
        public void Parse_IgnoresEmptySegments()
        {
            var input = "KEY1=VAL1;;KEY2=VAL2;";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal(2, result.Count);
            Assert.Equal("KEY1", result[0].Name);
            Assert.Equal("KEY2", result[1].Name);
        }

        [Fact]
        public void Parse_ThrowsFormatException_WhenEqualsIsEscaped()
        {
            var input = @"KEY\\\=VALUE"; // 3 backslashes -> escaped =

            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));
            Assert.Contains("no unescaped '='", ex.Message);
        }

        [Theory]
        [InlineData("KEY=VALUE", "VALUE")]
        [InlineData("KEY=\"VALUE\"", "VALUE")]
        [InlineData("KEY=\"VALUE\\\\\"", "VALUE\\")]
        [InlineData("KEY=\"VALUE\\=\"", "VALUE=")]
        [InlineData("KEY=\"VALUE\\;\"", "VALUE;")]
        [InlineData("KEY=\"VALUE\\\"\"", "VALUE\"")]
        [InlineData("KEY=\"VALUE\\=A\"", "VALUE=A")]
        [InlineData("KEY=\"VALUE\\;A\"", "VALUE;A")]
        [InlineData("KEY=\"VALUE\\\"A\"", "VALUE\"A")]
        [InlineData("KEY=\"VALUE\\\\A\"", "VALUE\\A")]
        [InlineData("KEY=VALUE\\=\\;\\\"\\\\A", "VALUE=;\"\\A")]
        [InlineData("KEY=\"VALUE\\=\\;\\\"\\\\A\"", "VALUE=;\"\\A")]
        [InlineData("KEY=\"VALUE\\=\\;\\\"\\\\\\\"\"", "VALUE=;\"\\\"")]
        public void Parse_Miscellaneous(string input, string value)
        {
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("KEY", result[0].Name);
            Assert.Equal(value, result[0].Value);
        }

        [Fact]
        public void Parse_MultipleVariablesSeparatedByNewline_ParsesCorrectly()
        {
            var input = "KEY1=VALUE1\nKEY2=VALUE2\nKEY3=\"VALUE3\"";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal(3, result.Count);
            Assert.Equal("KEY1", result[0].Name);
            Assert.Equal("KEY2", result[1].Name);
            Assert.Equal("KEY3", result[2].Name);
        }

        [Fact]
        public void Parse_MultipleVariablesSeparatedByWindowsNewline_ParsesCorrectly()
        {
            var input = "KEY1=VALUE1\r\nKEY2=VALUE2\r\nKEY3=VALUE3";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal(3, result.Count);
            Assert.Equal("KEY1", result[0].Name);
            Assert.Equal("KEY2", result[1].Name);
            Assert.Equal("KEY3", result[2].Name);
        }

        [Fact]
        public void Parse_MixedDelimiters_ParsesCorrectly()
        {
            // Mix of ;, \n, and \r\n
            var input = "KEY1=VAL1;KEY2=VAL2\nKEY3=VAL3\r\nKEY4=VAL4";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal(4, result.Count);
            Assert.Equal("VAL1", result[0].Value);
            Assert.Equal("VAL2", result[1].Value);
            Assert.Equal("VAL3", result[2].Value);
            Assert.Equal("VAL4", result[3].Value);
        }

        // ====================================================================
        // UPDATED REFLECTION TESTS (Targeting SplitByUnescapedDelimiters)
        // ====================================================================

        private static string[] InvokeSplit(string input, char[] delimiters)
        {
            return EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);
        }

        [Fact]
        public void SplitByUnescapedDelimiters_AllBranchesCovered()
        {
            char[] delims = new[] { '=' };

            // no delimiter
            var result = InvokeSplit("abc", delims);
            Assert.Single(result);
            Assert.Equal("abc", result[0]);

            // unescaped delimiter
            result = InvokeSplit("a=b", delims);
            Assert.Equal(new[] { "a", "b" }, result);

            // escaped delimiter (odd backslashes)
            result = InvokeSplit(@"a\=b", delims);
            Assert.Single(result);
            Assert.Equal(@"a\=b", result[0]);

            // even backslashes -> delimiter is unescaped
            result = InvokeSplit(@"a\\=b", delims);
            Assert.Equal(new[] { @"a\\", "b" }, result);

            // multiple unescaped delimiters
            result = InvokeSplit("a=b=c", delims);
            Assert.Equal(new[] { "a", "b", "c" }, result);

            // trailing delimiter
            result = InvokeSplit("a=", delims);
            Assert.Equal(new[] { "a", string.Empty }, result);
        }

        [Fact]
        public void SplitByUnescapedDelimiters_CoversAllWhileBranchConditions()
        {
            char[] delims = new[] { '=' };

            // 1. j < 0 (delimiter at index 0)
            var result = InvokeSplit("=a", delims);
            Assert.Equal(new[] { string.Empty, "a" }, result);

            // 2. j >= 0 but previous char is NOT backslash
            result = InvokeSplit("a=b", delims);
            Assert.Equal(new[] { "a", "b" }, result);

            // 3. loop executes once (single backslash)
            result = InvokeSplit(@"a\=b", delims);
            Assert.Single(result);
            Assert.Equal(@"a\=b", result[0]);

            // 4. loop executes multiple times (multiple backslashes)
            result = InvokeSplit(@"a\\\=b", delims);
            Assert.Single(result);
            Assert.Equal(@"a\\\=b", result[0]);
        }

        [Theory]
        [InlineData("KEY=\"hello\"", "hello")]           // Standard structural quotes
        [InlineData("KEY= \"hello\" ", "hello")]         // Structural quotes with surrounding whitespace
        [InlineData("KEY='hello'", "'hello'")]           // Single quotes are NOT structural; preserved literally
        [InlineData("KEY=\"hello", "\"hello")]           // Unmatched quotes are preserved literally
        public void Parse_StructuralQuotes_Behavior(string input, string expectedValue)
        {
            var result = EnvironmentVariableParser.Parse(input);
            Assert.Equal(expectedValue, result[0].Value);
        }

        [Theory]
        // 1. Literal quotes: Bypasses stripping because it starts with a backslash.
        // Parser Input: KEY=\"hello\" -> Result: "hello"
        [InlineData("KEY=\\\"hello\\\"", "\"hello\"")]

        // 2. Multiple literal quotes: Bypasses stripping.
        // Parser Input: KEY=\"\"\"\" -> Result: ""
        [InlineData("KEY=\\\"\\\"", "\"\"")]

        // 3. Structural quotes: Stripping is triggered because it starts/ends with ".
        // Parser Input: KEY= "hello" -> Result: hello
        [InlineData("KEY= \"hello\" ", "hello")]

        // 4. Nested quotes: Outer are stripped, inner are unescaped.
        // Parser Input: KEY="\"hello\"" -> Result: "hello"
        [InlineData("KEY=\"\\\"hello\\\"\"", "\"hello\"")]
        public void Parse_LiteralQuotes_PreservedWhenEscaped(string input, string expectedValue)
        {
            var result = EnvironmentVariableParser.Parse(input);
            Assert.Equal(expectedValue, result[0].Value);
        }

        [Fact]
        public void Parse_NestedQuotes_PreservedWhenOuterAreStructural()
        {
            // Input: KEY="\"hello\""
            // 1. Trim: KEY="\"hello\""
            // 2. Strip structural quotes: \"hello\"
            // 3. Unescape: "hello"
            var input = "KEY=\"\\\"hello\\\"\"";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("\"hello\"", result[0].Value);
        }

        [Fact]
        public void Parse_HandlesComplexEscapingWithQuotes()
        {
            // Verifies that escaped delimiters inside structural quotes work correctly
            var input = "KEY=\"Value\\;WithSemicolon\";KEY2=NEXT";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal(2, result.Count);
            Assert.Equal("Value;WithSemicolon", result[0].Value);
            Assert.Equal("NEXT", result[1].Value);
        }

        [Theory]
        // Preceding the raw newline control bytes with a backslash keeps the record unified 
        // during the tokenizer phase. Then, Unescape strips the backslash, leaving the raw control byte
        // inside 'value' so the parser's forbidden-newline check is triggered.
        [InlineData("KEY=Line1\\\nLine2", "KEY")]
        [InlineData("KEY=Line1\\\rLine2", "KEY")]
        [InlineData("KEY=Line1\\\r\\\nLine2", "KEY")]
        public void Parse_ValueContainsForbiddenNewline_ThrowsFormatException(string input, string expectedKeyInMessage)
        {
            // Act & Assert
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));

            // Verifies that the custom multi-line check was successfully triggered
            Assert.Contains($"Environment variable '{expectedKeyInMessage}' contains a forbidden newline character", ex.Message);
            Assert.Contains("Multi-line values are not supported", ex.Message);
        }

        [Theory]
        // Raw unquoted, unescaped control bytes cause structural splitting errors, 
        // confirming that multi-line formatting fails early at the boundary layer.
        [InlineData("KEY=Line1\nLine2", "Line2")]
        [InlineData("KEY=Line1\rLine2", "Line2")]
        [InlineData("KEY=Line1\r\nLine2", "Line2")]
        public void Parse_UnquotedRawNewline_ThrowsStructuralFormatException(string input, string expectedFragmentInMessage)
        {
            // Act & Assert
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));

            // Verifies that the tokenizer split the record, causing a structural missing '=' failure
            Assert.Contains($"no unescaped '='", ex.Message);
            Assert.Contains(expectedFragmentInMessage, ex.Message);
        }

        [Fact]
        public void Parse_EscapedRawNewlineControlBytes_ThrowsFormatExceptionAfterUnescaping()
        {
            // Arrange: Replicates an escaped raw control newline byte character block.
            // When EscapedTokenizer.Unescape processes '\' followed by a true '\n' byte,
            // it strips the backslash and appends the raw '\n' byte directly to the value stream.
            string input = "KEY=Line1\\\nLine2";

            // Act & Assert
            var ex = Assert.Throws<FormatException>(() => EnvironmentVariableParser.Parse(input));
            Assert.Contains("Environment variable 'KEY' contains a forbidden newline character", ex.Message);
        }
    }
}