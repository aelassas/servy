using Microsoft.Extensions.Configuration;
using Servy.Core.Helpers;
using System.Globalization;

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

        [Flags]
        public enum TestFlags
        {
            None = 0,
            A = 1,
            B = 2,
            Combined = 3
        }

        [Flags]
        public enum ULongFlagsEnum : ulong
        {
            None = 0UL,
            LowBit = 1UL,
            HighBit = 0x8000_0000_0000_0000UL // Exceeds long.MaxValue (9,223,372,036,854,775,807)
        }

        [Flags]
        public enum LongFlagsEnum : long
        {
            None = 0L,
            MemberOne = 1L,
            MemberTwo = 2L
        }

        public enum ByteBackedEnum : byte
        {
            None = 0,
            Member = 1
        }

        #region ParseInt Tests

        [Theory]
        [InlineData(null, 10, 10)]
        [InlineData("", 10, 10)]
        [InlineData("    ", 10, 10)]
        public void ParseInt_NullOrWhitespace_ReturnsDefault(string? input, int @default, int expected)
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
        public void ParseInt_PaddedInput_ReturnsParsedValue()
        {
            // Act
            var result = ConfigParser.ParseInt(" 42 ", 10);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void ParseInt_MalformedInput_ReturnsDefault()
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
        [InlineData("", true, true)]
        [InlineData("", false, false)]
        [InlineData("    ", false, false)]
        public void ParseBool_NullOrWhitespace_ReturnsDefault(string? input, bool @default, bool expected)
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
        [InlineData("2", false, false)]
        [InlineData("none", true, true)]
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

        [Theory]
        [InlineData(1, TestFlags.A)]
        [InlineData(3, TestFlags.Combined)]
        public void ParseEnum_Int_FlagsEnum_ValidValue_ReturnsParsedValue(int input, TestFlags expected)
        {
            // Act
            var result = ConfigParser.ParseEnum(input, TestFlags.None);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseEnum_Int_FlagsEnum_UnmappedBits_ReturnsDefault()
        {
            // Act
            var result = ConfigParser.ParseEnum(5, TestFlags.None);

            // Assert
            Assert.Equal(TestFlags.None, result);
        }

        [Fact]
        public void ParseEnum_Int_OverflowUnderlyingType_CatchesExceptionAndReturnsDefault()
        {
            // Act
            var result = ConfigParser.ParseEnum(999, ByteBackedEnum.None);

            // Assert
            Assert.Equal(ByteBackedEnum.None, result);
        }

        #endregion

        #region ParseEnum (String) Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void ParseEnum_String_NullOrWhitespace_ReturnsDefault(string? input)
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
        public void ParseEnum_String_ValidInput_ReturnsParsedValue(string input, TestStatus expected)
        {
            // Act
            var result = ConfigParser.ParseEnum(input, TestStatus.None);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("A", TestFlags.A)]
        [InlineData("A, B", TestFlags.Combined)]
        [InlineData("1", TestFlags.A)]
        [InlineData("3", TestFlags.Combined)]
        public void ParseEnum_String_FlagsEnum_ValidInput_ReturnsParsedValue(string input, TestFlags expected)
        {
            // Act
            var result = ConfigParser.ParseEnum(input, TestFlags.None);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("5")]
        [InlineData("A, Invalid")]
        public void ParseEnum_String_FlagsEnum_UnmappedInput_ReturnsDefault(string input)
        {
            // Act
            var result = ConfigParser.ParseEnum(input, TestFlags.None);

            // Assert
            Assert.Equal(TestFlags.None, result);
        }

        [Fact]
        public void ParseEnum_String_ULongFlagsEnum_ExceedingLongMax_UnmappedBits_ReturnsDefault()
        {
            // Arrange
            // 18446744073709551615 (0xFFFFFFFFFFFFFFFF) exceeds long.MaxValue and contains unmapped bits
            string unmappedULongString = "18446744073709551615";

            // Act
            var result = ConfigParser.ParseEnum(unmappedULongString, ULongFlagsEnum.None);

            // Assert
            Assert.Equal(ULongFlagsEnum.None, result);
        }

        [Fact]
        public void ParseEnum_String_ULongFlagsEnum_ExceedingLongMax_ValidValue_ReturnsParsedValue()
        {
            // Arrange
            // 9223372036854775808 (0x8000000000000000) exceeds long.MaxValue but is a valid defined member HighBit
            string validULongHighBitString = "9223372036854775808";

            // Act
            var result = ConfigParser.ParseEnum(validULongHighBitString, ULongFlagsEnum.None);

            // Assert
            Assert.Equal(ULongFlagsEnum.HighBit, result);
        }

        [Fact]
        public void ParseEnum_String_FlagsEnum_NegativeUnmappedInput_UnderNonInvariantCulture_ReturnsDefault()
        {
            // Arrange
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                // Culture where negative sign is U+2212 instead of standard ASCII '-'
                CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

                // Act - unmapped negative value "-5" for a long-backed flags enum
                var result = ConfigParser.ParseEnum("-5", LongFlagsEnum.None);

                // Assert
                Assert.Equal(LongFlagsEnum.None, result);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Theory]
        [InlineData("99")]
        [InlineData("999")]
        public void ParseEnum_String_UndefinedNumericInput_ReturnsDefault(string undefinedNumeric)
        {
            // Act
            // Numeric values parse successfully but fail Enum.IsDefined verification, falling through to the default
            var result = ConfigParser.ParseEnum(undefinedNumeric, TestStatus.None);

            // Assert
            Assert.Equal(TestStatus.None, result);
        }

        [Fact]
        public void ParseEnum_String_MalformedText_ReturnsDefault()
        {
            // Arrange
            string malformedInput = "Banana";

            // Act
            // Non-numeric arbitrary text cannot be parsed by Enum.TryParse, executing the true malformed branch
            var result = ConfigParser.ParseEnum(malformedInput, TestStatus.None);

            // Assert
            Assert.Equal(TestStatus.None, result);
        }

        [Theory]
        [InlineData("Active, Paused")]
        [InlineData("Active, Active")]
        [InlineData("Active, 2")]
        public void ParseEnum_String_NonFlagsEnum_CommaSeparatedInput_ReturnsDefault(string commaSeparatedInput)
        {
            // Arrange
            var defaultValue = TestStatus.None;

            // Act
            // Enum.TryParse bitwise ORs comma-separated enum names even on non-Flags enums;
            // ConfigParser must reject comma-separated values for non-Flags enums to prevent unintended member mapping.
            var result = ConfigParser.ParseEnum(commaSeparatedInput, defaultValue);

            // Assert
            Assert.Equal(defaultValue, result);
        }

        #endregion

        #region GetConfigInt Tests

        [Fact]
        public void GetConfigInt_MissingKey_ReturnsDefault()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            // Act
            var result = ConfigParser.GetConfigInt(config, "MissingKey", 10, 1, 100);

            // Assert
            Assert.Equal(10, result);
        }

        [Fact]
        public void GetConfigInt_ValidInRangeValue_ReturnsParsedValue()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "RestartTimeoutSeconds", "30" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act
            var result = ConfigParser.GetConfigInt(config, "RestartTimeoutSeconds", 10, 1, 60);

            // Assert
            Assert.Equal(30, result);
        }

        [Fact]
        public void GetConfigInt_MinBoundaryValue_ReturnsParsedValue()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "ConsoleMaxLines", "10" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act
            var result = ConfigParser.GetConfigInt(config, "ConsoleMaxLines", 100, 10, 10000);

            // Assert
            Assert.Equal(10, result);
        }

        [Fact]
        public void GetConfigInt_MaxBoundaryValue_ReturnsParsedValue()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "ConsoleMaxLines", "10000" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act
            var result = ConfigParser.GetConfigInt(config, "ConsoleMaxLines", 100, 10, 10000);

            // Assert
            Assert.Equal(10000, result);
        }

        [Fact]
        public void GetConfigInt_MalformedValue_ReturnsDefault()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "RefreshIntervalInSeconds", "not-a-number" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act
            var result = ConfigParser.GetConfigInt(config, "RefreshIntervalInSeconds", 5, 1, 60);

            // Assert
            Assert.Equal(5, result);
        }

        [Fact]
        public void GetConfigInt_BelowMin_ReturnsDefault()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "ConsoleMaxLines", "0" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act
            var result = ConfigParser.GetConfigInt(config, "ConsoleMaxLines", 100, 10, 10000);

            // Assert
            Assert.Equal(100, result);
        }

        [Fact]
        public void GetConfigInt_AboveMax_ReturnsDefault()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "MaxBulkOperationParallelism", "100" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act
            var result = ConfigParser.GetConfigInt(config, "MaxBulkOperationParallelism", 8, 1, 16);

            // Assert
            Assert.Equal(8, result);
        }

        #endregion
    }
}
