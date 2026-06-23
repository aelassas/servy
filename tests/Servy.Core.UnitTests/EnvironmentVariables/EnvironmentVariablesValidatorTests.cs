using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using System.Collections.Generic;
using Xunit;

namespace Servy.Core.UnitTests.EnvironmentVariables
{
    public class EnvironmentVariablesValidatorTests
    {
        [Fact]
        public void Validate_EmptyInput_ReturnsTrue()
        {
            List<string> error;
            var result = EnvironmentVariablesValidator.Validate("", out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_OnlyWhitespaceInput_ReturnsTrue()
        {
            List<string> error;
            var result = EnvironmentVariablesValidator.Validate("   \r\n\t   ", out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_SingleValidVariable_ReturnsTrue()
        {
            List<string> error;
            var result = EnvironmentVariablesValidator.Validate("KEY=VALUE", out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedBySemicolon_ReturnsTrue()
        {
            List<string> error;
            var result = EnvironmentVariablesValidator.Validate("KEY1=VAL1;KEY2=VAL2", out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedByNewLines_ReturnsTrue()
        {
            List<string> error;
            var input = "KEY1=VAL1\r\nKEY2=VAL2\nKEY3=VAL3";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_MultipleVariablesMixedDelimiters_ReturnsTrue()
        {
            List<string> error;
            var input = "KEY1=VAL1;KEY2=VAL2\r\nKEY3=VAL3\nKEY4=VAL4";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithEscapedEqualsInKey_ReturnsTrue()
        {
            List<string> error;
            var input = @"KEY\=PART=VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithEscapedSemicolonInKey_ReturnsTrue()
        {
            List<string> error;
            var input = @"KEY\;PART=VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithEscapedBackslashInKey_ReturnsTrue()
        {
            List<string> error;
            var input = @"KEY\\PART=VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithEscapedEqualsInValue_ReturnsTrue()
        {
            List<string> error;
            var input = @"KEY=VALUE\=PART";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithEscapedSemicolonInValue_ReturnsTrue()
        {
            List<string> error;
            var input = @"KEY=VALUE\;PART";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithEscapedBackslashInValue_ReturnsTrue()
        {
            List<string> error;
            var input = @"KEY=VALUE\\PART";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableMissingEquals_ReturnsFalse()
        {
            List<string> error;
            var result = EnvironmentVariablesValidator.Validate("NOVALUE", out error);
            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableMissingEquals));
        }

        [Fact]
        public void Validate_VariableWithEmptyKey_ReturnsFalse()
        {
            List<string> error;
            var result = EnvironmentVariablesValidator.Validate("=VALUE", out error);
            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableKeyEmpty));
        }

        [Fact]
        public void Validate_IgnoresEmptySegments()
        {
            List<string> error;
            var result = EnvironmentVariablesValidator.Validate("KEY1=VAL1;;KEY2=VAL2;", out error);
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithMultipleEqualsButOnlyOneUnescaped_ReturnsTrue()
        {
            List<string> error;
            // The first '=' unescaped is counted, others escaped by backslash
            var input = @"KEY1=VAL\=UE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Empty(error);
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
            List<string> error;
            var input = "CONN=Server=localhost;Database=Test;TOKEN=SGVsbG8==;";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithNoUnescapedEquals_ReturnsFalse()
        {
            // Scenario: All equals signs are escaped, so there is no key/value separator.
            List<string> error;
            var input = @"KEY\=VALUE;KEY2\=VALUE2";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableMissingEquals));
        }

        [Fact]
        public void Validate_VariableWithWhitespaceKey_ReturnsFalse()
        {
            // Scenario: Key consists only of whitespace before the first '='.
            List<string> error;
            var input = "   =VALUE";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableKeyEmpty));
        }

        [Fact]
        public void Validate_Base64Value_ReturnsTrue()
        {
            // Scenario: Base64 padding uses '=' which shouldn't require escaping in the value field.
            List<string> error;
            var input = "VAR=SGVsbG8gd29ybGQ=";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_ComplexEscapingSequence_ReturnsTrue()
        {
            // Scenario: Mix of escaped backslashes and delimiters.
            // "KEY\\" (escaped backslash) + "=" (unescaped separator) + "VAL"
            List<string> error;
            var input = @"KEY\\=VAL";
            var result = EnvironmentVariablesValidator.Validate(input, out error);

            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void FormatEnvironmentVariables_WithLiteralNewlines_EscapesCarriageReturnAndLineFeed()
        {
            // Arrange
            // Input string simulating paired variables with multi-line values
            string rawInput = @"MULTILINE_KEY=line1\nline2";

            // Act
            string formatted = StringHelper.FormatEnvironmentVariables(rawInput);

            // Assert
            // The serialization must explicitly present the escaped sequences back out to prevent line truncation
            Assert.Contains(@"MULTILINE_KEY=line1\\nline2", formatted);

            List<string> error;
            bool isValid = EnvironmentVariablesValidator.Validate(formatted, out error);
            Assert.True(isValid, $"Validator rejected formatted output with errors count: {error.Count}");
        }

        [Fact]
        public void EnvironmentVariablesValidator_UnescapedNewlineWithinSegment_FailsValidation()
        {
            // Arrange
            // Malformed configuration state mimicking an unescaped raw break mid-value
            string corruptedInput = "KEY=line1\nline2_with_no_equals";

            // Act
            List<string> errorMessages;
            bool isValid = EnvironmentVariablesValidator.Validate(corruptedInput, out errorMessages);

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(errorMessages);
        }
    }
}