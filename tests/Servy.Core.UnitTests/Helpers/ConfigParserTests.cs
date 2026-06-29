using Servy.Core.Helpers;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ConfigParserTests
    {
        public enum TestStatus
        {
            None = 0,
            Active = 1,
            Paused = 2
        }

        #region ParseInt Tests

        [Theory]
        [InlineData(null, 10, 10)]
        [InlineData("", 10, 10)]
        [InlineData("   ", 10, 10)]
        public void ParseInt_NullOrWhitespace_ReturnsDefaultWithoutLogging(string input, int @default, int expected)
        {
            // Act
            var result = ConfigParser.ParseInt(input, @default);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseInt_ValidInteger_ReturnsParsedValue()
        {
            // Act
            var result = ConfigParser.ParseInt("42", 10);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void ParseInt_MalformedInput_ReturnsDefaultAndLogsWarning()
        {
            // Act
            var result = ConfigParser.ParseInt("not-a-number", 10);

            // Assert
            Assert.Equal(10, result);
        }

        #endregion

        #region ParseBool Tests

        [Theory]
        [InlineData(null, true, true)]
        [InlineData("   ", false, false)]
        public void ParseBool_NullOrWhitespace_ReturnsDefault(string input, bool @default, bool expected)
        {
            // Act
            var result = ConfigParser.ParseBool(input, @default);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("  TRUE  ", true)] // Verifies trimming behavior
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("FALSE", false)]
        public void ParseBool_ValidStandardInput_ReturnsParsedValue(string input, bool expected)
        {
            // Act
            var result = ConfigParser.ParseBool(input, !expected);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("1", true)]
        [InlineData("yes", true)]
        [InlineData("Yes", true)]
        [InlineData("YES", true)]
        [InlineData("y", true)]
        [InlineData("Y", true)]
        [InlineData("on", true)]
        [InlineData("On", true)]
        [InlineData("ON", true)]
        [InlineData("0", false)]
        [InlineData("no", false)]
        [InlineData("No", false)]
        [InlineData("NO", false)]
        [InlineData("n", false)]
        [InlineData("N", false)]
        [InlineData("off", false)]
        [InlineData("Off", false)]
        [InlineData("OFF", false)]
        [InlineData("  yes  ", true)] // Verifies trimming on alias variants
        public void ParseBool_ValidSemanticAliases_ReturnsParsedValue(string input, bool expected)
        {
            // Act
            var result = ConfigParser.ParseBool(input, !expected);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Maybe", true, true)]
        [InlineData("Maybe", false, false)]
        [InlineData("2", true, true)]
        [InlineData("none", false, false)]
        public void ParseBool_InvalidInput_ReturnsDefault(string input, bool @default, bool expected)
        {
            // Act
            var result = ConfigParser.ParseBool(input, @default);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region ParseEnum (Numeric) Tests

        [Fact]
        public void ParseEnum_Int_Null_ReturnsDefault()
        {
            // Act
            var result = ConfigParser.ParseEnum((int?)null, TestStatus.Paused);

            // Assert
            Assert.Equal(TestStatus.Paused, result);
        }

        [Fact]
        public void ParseEnum_Int_DefinedValue_ReturnsEnumMember()
        {
            // Act
            var result = ConfigParser.ParseEnum(1, TestStatus.None);

            // Assert
            Assert.Equal(TestStatus.Active, result);
        }

        [Fact]
        public void ParseEnum_Int_UndefinedValue_ReturnsDefault()
        {
            // Act
            var result = ConfigParser.ParseEnum(999, TestStatus.None);

            // Assert
            Assert.Equal(TestStatus.None, result);
        }

        #endregion

        #region ParseEnum (String) Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void ParseEnum_String_NullOrWhitespace_ReturnsDefault(string input)
        {
            // Act
            var result = ConfigParser.ParseEnum(input, TestStatus.Paused);

            // Assert
            Assert.Equal(TestStatus.Paused, result);
        }

        [Theory]
        [InlineData("Active", TestStatus.Active)]
        [InlineData("active", TestStatus.Active)] // Case-insensitive check
        [InlineData("2", TestStatus.Paused)]      // Numeric string check
        [InlineData("99", TestStatus.None)]       // Numeric string out of range
        public void ParseEnum_String_ValidInput_ReturnsParsedValue(string input, TestStatus expected)
        {
            // Act
            var result = ConfigParser.ParseEnum(input, TestStatus.None);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseEnum_String_InvalidNumericInput_ReturnsDefault()
        {
            // Act
            var result = ConfigParser.ParseEnum("99", TestStatus.None);

            // Assert
            Assert.Equal(TestStatus.None, result);
        }

        [Fact]
        public void ParseEnum_String_MalformedOrUndefined_ReturnsDefault()
        {
            // Act
            var result = ConfigParser.ParseEnum("999", TestStatus.None);

            // Assert
            Assert.Equal(TestStatus.None, result);
        }

        #endregion
    }
}