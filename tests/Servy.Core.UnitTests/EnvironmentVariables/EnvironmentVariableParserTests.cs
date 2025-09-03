using Servy.Core.EnvironmentVariables;
using System;
using Xunit;

namespace Servy.Core.UnitTests.EnvironmentVariables
{
    public class EnvironmentVariableParserTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Parse_EmptyOrNullInput_ReturnsEmptyList(string input)
        {
            var result = EnvironmentVariableParser.Parse(input);
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
            var input = "KEY1=VALUE1;KEY2=VALUE2";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal(2, result.Count);
            Assert.Equal("KEY1", result[0].Name);
            Assert.Equal("VALUE1", result[0].Value);
            Assert.Equal("KEY2", result[1].Name);
            Assert.Equal("VALUE2", result[1].Value);
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

        [Theory]
        [InlineData("KEY=VAL\\UE", "VAL\\UE")]
        [InlineData("KEY=VAL\\\\UE", "VAL\\\\UE")]
        [InlineData("KEY=VAL\\\\\\UE", "VAL\\\\\\UE")]
        [InlineData("KEY=VAL\\\\\\\\UE", "VAL\\\\\\\\UE")]
        public void Parse_SupportsMultipleBackslashes(string input, string expectedValue)
        {
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal(expectedValue, result[0].Value);
        }

        [Theory]
        [InlineData("KEY=VAL\\XUE", "VAL\\XUE")] // unknown escape
        [InlineData("KEY=VAL\\\\XUE", "VAL\\\\XUE")] // double backslash before non special char
        public void Parse_UnknownOrLiteralBackslashes_Preserved(string input, string expectedValue)
        {
            var result = EnvironmentVariableParser.Parse(input);
            Assert.Single(result);
            Assert.Equal(expectedValue, result[0].Value);
        }

        [Theory]
        [InlineData("KEY=VALUE\\", "VALUE\\")] // trailing double backslash at end
        [InlineData("KEY=VALUE\\\\", "VALUE\\")] // trailing double backslash at end
        [InlineData("KEY=VALUE\\\\=", "VALUE\\=")] // double backslash before =
        [InlineData("KEY=VALUE\\\\\\;", "VALUE\\\\;")] // triple backslash before ;
        public void Parse_TrailingOrPreDelimiterBackslashes_ParsedCorrectly(string input, string expectedValue)
        {
            var result = EnvironmentVariableParser.Parse(input);
            Assert.Single(result);
            Assert.Equal(expectedValue, result[0].Value);
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
        public void Parse_MultipleVariablesWithMixedEscapes()
        {
            var input = "K\\=EY1=VAL\\1;KEY2=VAL\\=2;KEY3=VAL\\\\=3";
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Equal(3, result.Count);
            Assert.Equal("K=EY1", result[0].Name);
            Assert.Equal("VAL\\1", result[0].Value);

            Assert.Equal("KEY2", result[1].Name);
            Assert.Equal("VAL=2", result[1].Value);

            Assert.Equal("KEY3", result[2].Name);
            Assert.Equal("VAL\\=3", result[2].Value);
        }

    }
}
