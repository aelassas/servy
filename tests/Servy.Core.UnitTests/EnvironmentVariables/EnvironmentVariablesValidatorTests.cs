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
            // Arrange
            string input = "";

            // Act
            bool result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_OnlyWhitespaceInput_ReturnsTrue()
        {
            // Arrange
            string input = "   \r\n\t   ";

            // Act
            bool result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_SingleValidVariable_ReturnsTrue()
        {
            // Arrange
            string input = "KEY=VALUE";

            // Act
            bool result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedBySemicolon_ReturnsTrue()
        {
            // Arrange
            string input = "KEY1=VAL1;KEY2=VAL2";

            // Act
            bool result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedByNewLines_ReturnsTrue()
        {
            // Arrange
            var input = "KEY1=VAL1\r\nKEY2=VAL2\nKEY3=VAL3";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_MultipleVariablesMixedDelimiters_ReturnsTrue()
        {
            // Arrange
            var input = "KEY1=VAL1;KEY2=VAL2\r\nKEY3=VAL3\nKEY4=VAL4";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        #region Parameterized Escaped Delimiter Evaluation Tests

        [Theory]
        [InlineData(@"KEY\=PART=VALUE")]  // Escaped Equals In Key
        [InlineData(@"KEY\;PART=VALUE")]  // Escaped Semicolon In Key
        [InlineData(@"KEY\\PART=VALUE")]  // Escaped Backslash In Key
        [InlineData(@"KEY=VALUE\=PART")]  // Escaped Equals In Value
        [InlineData(@"KEY=VALUE\;PART")]  // Escaped Semicolon In Value
        [InlineData(@"KEY=VALUE\\PART")]  // Escaped Backslash In Value
        [InlineData(@"KEY1=VAL\=UE")]     // Scenario: The first '=' unescaped is counted, others escaped by backslash
        [InlineData(@"KEY\\=VAL")]        // Scenario: Mix of escaped backslashes and delimiters: "KEY\\" (escaped backslash) + "=" (unescaped separator) + "VAL"
        public void Validate_EscapedDelimiterInKeyOrValue_ReturnsTrue(string input)
        {
            // Arrange & Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        #endregion

        [Fact]
        public void Validate_VariableMissingEquals_ReturnsFalse()
        {
            // Arrange
            string input = "NOVALUE";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableMissingEquals));
        }

        [Fact]
        public void Validate_VariableWithEmptyKey_ReturnsFalse()
        {
            // Arrange
            string input = "=VALUE";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableKeyEmpty));
        }

        [Fact]
        public void Validate_IgnoresEmptySegments()
        {
            // Arrange
            string input = "KEY1=VAL1;;KEY2=VAL2;";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
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
            // Arrange
            var delims = new[] { '=', ';' };

            // Act & Assert 1. delimiter at index 0 -> j < 0
            var result = InvokeSplit("=a", delims);
            Assert.Equal(new[] { string.Empty, "a" }, result);

            // Act & Assert 2. delimiter preceded by non-backslash
            result = InvokeSplit("a=b", delims);
            Assert.Equal(new[] { "a", "b" }, result);

            // Act & Assert 3. delimiter not in delimiters list
            result = InvokeSplit("a:b", delims);
            Assert.Single(result);
            Assert.Equal("a:b", result[0]);

            // Act & Assert 4. single backslash before delimiter (odd -> escaped)
            result = InvokeSplit(@"a\=b", delims);
            Assert.Single(result);
            Assert.Equal(@"a\=b", result[0]);

            // Act & Assert 5. multiple backslashes (odd -> escaped, loop runs multiple times)
            result = InvokeSplit(@"a\\\=b", delims);
            Assert.Single(result);
            Assert.Equal(@"a\\\=b", result[0]);

            // Act & Assert 6. even backslashes -> unescaped delimiter
            result = InvokeSplit(@"a\\=b", delims);
            Assert.Equal(new[] { @"a\\", "b" }, result);

            // Act & Assert 7. multiple delimiters, mixed escaped and unescaped
            result = InvokeSplit(@"a=b\;c;d", delims);
            Assert.Equal(new[] { "a", @"b\;c", "d" }, result);
        }

        [Fact]
        public void Validate_VariableWithMultipleUnescapedEquals_ReturnsTrue()
        {
            // Arrange
            // Scenario: Connection Strings and Base64 often have multiple '='.
            // Validator should allow this as long as the first '=' provides a valid key.
            var input = "CONN=Server=localhost;Database=Test;TOKEN=SGVsbG8==;";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_VariableWithNoUnescapedEquals_ReturnsFalse()
        {
            // Arrange
            // Scenario: All equals signs are escaped, so there is no key/value separator.
            var input = @"KEY\=VALUE;KEY2\=VALUE2";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableMissingEquals));
        }

        [Fact]
        public void Validate_VariableWithWhitespaceKey_ReturnsFalse()
        {
            // Arrange
            // Scenario: Key consists only of whitespace before the first '='.
            var input = "   =VALUE";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
            Assert.False(result);
            Assert.Contains(error, e => e.Contains(Strings.Msg_EnvironmentVariableKeyEmpty));
        }

        [Fact]
        public void Validate_Base64Value_ReturnsTrue()
        {
            // Arrange
            // Scenario: Base64 padding uses '=' which shouldn't require escaping in the value field.
            var input = "VAR=SGVsbG8gd29ybGQ=";

            // Act
            var result = EnvironmentVariablesValidator.Validate(input, out List<string> error);

            // Assert
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

        #region Key Formatting and Security Robustness Rule Tests

        [Fact]
        public void Validate_VariableWithEscapedNewlineInKey_ReturnsFalse()
        {
            // Arrange
            // Simulates an escaped newline sequence inside the key segment that tokenizes as a single block
            // but resolves to containing a newline byte upon unescaping step.
            string input = "KEY_START\\\nKEY_END=Value";

            // Act
            bool isValid = EnvironmentVariablesValidator.Validate(input, out List<string> errorMessages);

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(errorMessages);

            // Assert against the unescaped key version ("KEY_START\nKEY_END") matching validator execution
            Assert.Equal(string.Format(Strings.Msg_EnvironmentVariableForbiddenNewline, "KEY_START\nKEY_END"), errorMessages[0]);
        }

        [Fact]
        public void Validate_VariableWithNullTerminatorInKey_ReturnsFalse()
        {
            // Arrange
            // Injection boundary validation trap ensuring malicious payload sequences containing Win32 
            // structural string terminators are successfully caught during runtime verification.
            string input = "KEY\0ATTACK=Value";

            // Act
            bool isValid = EnvironmentVariablesValidator.Validate(input, out List<string> errorMessages);

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(errorMessages);
            Assert.Equal(errorMessages[0], string.Format(Strings.Msg_EnvironmentVariableKeyInvalidChars, "KEY\0ATTACK"));
        }

        #endregion
    }
}