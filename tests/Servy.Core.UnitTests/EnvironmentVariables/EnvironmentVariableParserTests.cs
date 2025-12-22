using Servy.Core.EnvironmentVariables;
using System;
using System.Reflection;
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
        public void Parse_EscapeBackSpalsh(string input)
        {
            var result = EnvironmentVariableParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("NOEQUAL", result[0].Value);
        }

        [Fact]
        public void Parse_IgnoresEmptySegments()
        {
            var input = "KEY1=\\VAL1;;KEY2=VAL2;";
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

        private static string[] InvokeSplit(string input, char delimiter)
        {
            var method = typeof(EnvironmentVariableParser)
                .GetMethod(
                    "SplitByUnescapedDelimiter",
                    BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            return (string[])method.Invoke(null, new object[] { input, delimiter });
        }

        [Fact]
        public void SplitByUnescapedDelimiter_AllBranchesCovered()
        {
            // no delimiter
            var result = InvokeSplit("abc", '=');
            Assert.Single(result);
            Assert.Equal("abc", result[0]);

            // unescaped delimiter
            result = InvokeSplit("a=b", '=');
            Assert.Equal(new[] { "a", "b" }, result);

            // escaped delimiter (odd backslashes)
            result = InvokeSplit(@"a\=b", '=');
            Assert.Single(result);
            Assert.Equal(@"a\=b", result[0]);

            // even backslashes -> delimiter is unescaped
            result = InvokeSplit(@"a\\=b", '=');
            Assert.Equal(new[] { @"a\\", "b" }, result);

            // odd backslashes again
            result = InvokeSplit(@"a\\\=b", '=');
            Assert.Single(result);
            Assert.Equal(@"a\\\=b", result[0]);

            // multiple unescaped delimiters
            result = InvokeSplit("a=b=c", '=');
            Assert.Equal(new[] { "a", "b", "c" }, result);

            // trailing delimiter
            result = InvokeSplit("a=", '=');
            Assert.Equal(new[] { "a", string.Empty }, result);
        }

        [Fact]
        public void SplitByUnescapedDelimiter_CoversAllWhileBranchConditions()
        {
            // 1. j < 0 (delimiter at index 0)
            var result = InvokeSplit("=a", '=');
            Assert.Equal(new[] { string.Empty, "a" }, result);

            // 2. j >= 0 but previous char is NOT backslash
            result = InvokeSplit("a=b", '=');
            Assert.Equal(new[] { "a", "b" }, result);

            // 3. loop executes once (single backslash)
            result = InvokeSplit(@"a\=b", '=');
            Assert.Single(result);
            Assert.Equal(@"a\=b", result[0]);

            // 4. loop executes multiple times (multiple backslashes)
            result = InvokeSplit(@"a\\\=b", '=');
            Assert.Single(result);
            Assert.Equal(@"a\\\=b", result[0]);

            // sanity: even backslashes -> split
            result = InvokeSplit(@"a\\=b", '=');
            Assert.Equal(new[] { @"a\\", "b" }, result);
        }

    }
}