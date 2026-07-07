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

        [Fact]
        public void Validate_VariableWithMultipleUnescapedEquals_ReturnsTrue()
        {
            // Arrange
            // Semicolons are backslash-escaped so the validator handles the connection string 
            // as a single environment variable context block under the 'CONN' key, matching the test name intent.
            var input = "CONN=Server=localhost\\;Database=Test\\;TOKEN=SGVsbG8==;";

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
            string rawInput = @"MULTILINE_KEY=line1\nline2";

            // Act
            string formatted = StringHelper.FormatEnvironmentVariables(rawInput);

            // Assert
            Assert.Contains(@"MULTILINE_KEY=line1\\nline2", formatted);

            List<string> error;
            bool isValid = EnvironmentVariablesValidator.Validate(formatted, out error);
            Assert.True(isValid, $"Validator rejected formatted output with errors count: {error.Count}");
        }

        [Fact]
        public void Validate_UnescapedNewlineWithinSegment_FailsValidation()
        {
            // Arrange
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
            string input = "KEY_START\\\nKEY_END=Value";

            // Act
            bool isValid = EnvironmentVariablesValidator.Validate(input, out List<string> errorMessages);

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(errorMessages);
            Assert.Equal(string.Format(Strings.Msg_EnvironmentVariableForbiddenNewline, "KEY_START\nKEY_END"), errorMessages[0]);
        }

        [Fact]
        public void Validate_VariableWithNullTerminatorInKey_ReturnsFalse()
        {
            // Arrange
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