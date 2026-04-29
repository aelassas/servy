using Servy.Core.EnvironmentVariables;
using System.Reflection;

namespace Servy.Core.UnitTests.EnvironmentVariables
{
    public class EnvironmentVariablesValidatorTests
    {
        [Fact]
        public void Validate_EmptyInput_ReturnsTrue()
        {
            string error;
            var result = EnvironmentVariablesValidator.Validate("", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_OnlyWhitespaceInput_ReturnsTrue()
        {
            string error;
            var result = EnvironmentVariablesValidator.Validate("   \r\n\t  ", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_SingleValidVariable_ReturnsTrue()
        {
            string error;
            var result = EnvironmentVariablesValidator.Validate("KEY=VALUE", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedBySemicolon_ReturnsTrue()
        {
            string error;
            var result = EnvironmentVariablesValidator.Validate("KEY1=VAL1;KEY2=VAL2", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedByNewLines_ReturnsTrue()
        {
            string error;
            var input = "KEY1=VAL1\r\nKEY2=VAL2\nKEY3=VAL3";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_MultipleVariablesMixedDelimiters_ReturnsTrue()
        {
            string error;
            var input = "KEY1=VAL1;KEY2=VAL2\r\nKEY3=VAL3\nKEY4=VAL4";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedEqualsInKey_ReturnsTrue()
        {
            string error;
            var input = @"KEY\=PART=VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedSemicolonInKey_ReturnsTrue()
        {
            string error;
            var input = @"KEY\;PART=VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedBackslashInKey_ReturnsTrue()
        {
            string error;
            var input = @"KEY\\PART=VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedEqualsInValue_ReturnsTrue()
        {
            string error;
            var input = @"KEY=VALUE\=PART";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedSemicolonInValue_ReturnsTrue()
        {
            string error;
            var input = @"KEY=VALUE\;PART";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedBackslashInValue_ReturnsTrue()
        {
            string error;
            var input = @"KEY=VALUE\\PART";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableMissingEquals_ReturnsFalse()
        {
            string error;
            var result = EnvironmentVariablesValidator.Validate("NOVALUE", out error);
            Assert.False(result);
            Assert.Contains("Each variable must contain an unescaped '=' character to separate the key from the value.", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_VariableWithEmptyKey_ReturnsFalse()
        {
            string error;
            var result = EnvironmentVariablesValidator.Validate("=VALUE", out error);
            Assert.False(result);
            Assert.Contains("key cannot be empty", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_IgnoresEmptySegments()
        {
            string error;
            var result = EnvironmentVariablesValidator.Validate("KEY1=VAL1;;KEY2=VAL2;", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithMultipleEqualsButOnlyOneUnescaped_ReturnsTrue()
        {
            string error;
            // The first '=' unescaped is counted, others escaped by backslash
            var input = @"KEY1=VAL\=UE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void IndexOfUnescapedChar_ReturnsMinusOne_WhenCharNotFound()
        {
            // Arrange
            var input = @"KEY\=NOEQUAL"; // '=' is escaped, so no unescaped '=' present

            // Act
            var result = EnvironmentVariablesValidator.IndexOfUnescapedChar(input, '=');

            // Assert
            Assert.Equal(-1, result);
        }

        private static string[] InvokeSplit(string input, char[] delimiters)
        {
            return EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);
        }

        [Fact]
        public void SplitByUnescapedDelimiters_AllBranchesCovered()
        {
            var delims = new[] { '=', ';' };

            // 1. delimiter at index 0 -> j < 0
            var result = InvokeSplit("=a", delims);
            Assert.Equal(new[] { string.Empty, "a" }, result);

            // 2. delimiter preceded by non-backslash
            result = InvokeSplit("a=b", delims);
            Assert.Equal(new[] { "a", "b" }, result);

            // 3. delimiter not in delimiters list
            result = InvokeSplit("a:b", delims);
            Assert.Single(result);
            Assert.Equal("a:b", result[0]);

            // 4. single backslash before delimiter (odd -> escaped)
            result = InvokeSplit(@"a\=b", delims);
            Assert.Single(result);
            Assert.Equal(@"a\=b", result[0]);

            // 5. multiple backslashes (odd -> escaped, loop runs multiple times)
            result = InvokeSplit(@"a\\\=b", delims);
            Assert.Single(result);
            Assert.Equal(@"a\\\=b", result[0]);

            // 6. even backslashes -> unescaped delimiter
            result = InvokeSplit(@"a\\=b", delims);
            Assert.Equal(new[] { @"a\\", "b" }, result);

            // 7. multiple delimiters, mixed escaped and unescaped
            result = InvokeSplit(@"a=b\;c;d", delims);
            Assert.Equal(new[] { "a", @"b\;c", "d" }, result);
        }

        [Fact]
        public void Validate_VariableWithMultipleUnescapedEquals_ReturnsTrue()
        {
            // Scenario: Connection Strings and Base64 often have multiple '='.
            // Validator should allow this as long as the first '=' provides a valid key.
            string error;
            var input = "CONN=Server=localhost;Database=Test;TOKEN=SGVsbG8==;";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithNoUnescapedEquals_ReturnsFalse()
        {
            // Scenario: All equals signs are escaped, so there is no key/value separator.
            string error;
            var input = @"KEY\=VALUE;KEY2\=VALUE2";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.False(result);
            // Updated to match the refined error message
            Assert.Contains("must contain an unescaped '=' character", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_VariableWithWhitespaceKey_ReturnsFalse()
        {
            // Scenario: Key consists only of whitespace before the first '='.
            string error;
            var input = "   =VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.False(result);
            Assert.Contains("key cannot be empty", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_Base64Value_ReturnsTrue()
        {
            // Scenario: Base64 padding uses '=' which shouldn't require escaping in the value field.
            string error;
            var input = "VAR=SGVsbG8gd29ybGQ=";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_ComplexEscapingSequence_ReturnsTrue()
        {
            // Scenario: Mix of escaped backslashes and delimiters.
            // "KEY\\" (escaped backslash) + "=" (unescaped separator) + "VAL"
            string error;
            var input = @"KEY\\=VAL";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.True(result);
        }

    }
}
