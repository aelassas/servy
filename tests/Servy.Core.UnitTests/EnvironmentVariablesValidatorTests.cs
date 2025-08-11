using Servy.Core.EnvironmentVariables;

namespace Servy.Core.UnitTests
{
    public class EnvironmentVariablesValidatorTests
    {
        [Fact]
        public void Validate_EmptyInput_ReturnsTrue()
        {
            string error;
            bool result = EnvironmentVariablesValidator.Validate("", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_OnlyWhitespaceInput_ReturnsTrue()
        {
            string error;
            bool result = EnvironmentVariablesValidator.Validate("   \r\n\t  ", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_SingleValidVariable_ReturnsTrue()
        {
            string error;
            bool result = EnvironmentVariablesValidator.Validate("KEY=VALUE", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedBySemicolon_ReturnsTrue()
        {
            string error;
            bool result = EnvironmentVariablesValidator.Validate("KEY1=VAL1;KEY2=VAL2", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_MultipleVariablesSeparatedByNewLines_ReturnsTrue()
        {
            string error;
            string input = "KEY1=VAL1\r\nKEY2=VAL2\nKEY3=VAL3";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_MultipleVariablesMixedDelimiters_ReturnsTrue()
        {
            string error;
            string input = "KEY1=VAL1;KEY2=VAL2\r\nKEY3=VAL3\nKEY4=VAL4";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedEqualsInKey_ReturnsTrue()
        {
            string error;
            string input = @"KEY\=PART=VALUE";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedSemicolonInKey_ReturnsTrue()
        {
            string error;
            string input = @"KEY\;PART=VALUE";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedBackslashInKey_ReturnsTrue()
        {
            string error;
            string input = @"KEY\\PART=VALUE";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedEqualsInValue_ReturnsTrue()
        {
            string error;
            string input = @"KEY=VALUE\=PART";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedSemicolonInValue_ReturnsTrue()
        {
            string error;
            string input = @"KEY=VALUE\;PART";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithEscapedBackslashInValue_ReturnsTrue()
        {
            string error;
            string input = @"KEY=VALUE\\PART";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableMissingEquals_ReturnsFalse()
        {
            string error;
            bool result = EnvironmentVariablesValidator.Validate("NOVALUE", out error);
            Assert.False(result);
            Assert.Contains("exactly one unescaped '='", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_VariableWithEmptyKey_ReturnsFalse()
        {
            string error;
            bool result = EnvironmentVariablesValidator.Validate("=VALUE", out error);
            Assert.False(result);
            Assert.Contains("key cannot be empty", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_IgnoresEmptySegments()
        {
            string error;
            bool result = EnvironmentVariablesValidator.Validate("KEY1=VAL1;;KEY2=VAL2;", out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithMultipleEqualsButOnlyOneUnescaped_ReturnsTrue()
        {
            string error;
            // The first '=' unescaped is counted, others escaped by backslash
            string input = @"KEY1=VAL\=UE";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.True(result);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void Validate_VariableWithMultipleUnescapedEquals_ReturnsFalse()
        {
            string error;
            string input = "KEY1=VAL=UE";
            bool result = EnvironmentVariablesValidator.Validate(input, out error);
            Assert.False(result);
            Assert.Contains("exactly one unescaped '='", error, StringComparison.OrdinalIgnoreCase);
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

    }
}
