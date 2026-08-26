using Servy.Core.Helpers;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ReservedNamesTests
    {
        #region ReservedDeviceNames Collection Tests

        [Fact]
        public void ReservedDeviceNames_ContainsExpectedCount()
        {
            // Assert
            // 4 base (CON, PRN, AUX, NUL) + 2 handles (CONIN$, CONOUT$) + 12 COM (1-9, ¹, ², ³) + 12 LPT (1-9, ¹, ², ³) = 30
            Assert.Equal(30, ReservedNames.ReservedDeviceNames.Count);
        }

        [Fact]
        public void ReservedDeviceNames_IsCaseInsensitive()
        {
            // Assert
            Assert.Contains("con", ReservedNames.ReservedDeviceNames);
            Assert.Contains("CON", ReservedNames.ReservedDeviceNames);
            Assert.Contains("cOm1", ReservedNames.ReservedDeviceNames);
            Assert.Contains("com¹", ReservedNames.ReservedDeviceNames);
        }

        #endregion

        #region IsReservedDeviceName Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IsReservedDeviceName_NullOrEmpty_ReturnsFalse(string segment)
        {
            // Act
            bool result = ReservedNames.IsReservedDeviceName(segment);

            // Assert
            Assert.False(result);
        }

        [Theory]
        // Base reserved device names
        [InlineData("CON")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("CONIN$")]
        [InlineData("CONOUT$")]
        // COM ports
        [InlineData("COM1")]
        [InlineData("COM2")]
        [InlineData("COM3")]
        [InlineData("COM4")]
        [InlineData("COM5")]
        [InlineData("COM6")]
        [InlineData("COM7")]
        [InlineData("COM8")]
        [InlineData("COM9")]
        [InlineData("COM¹")]
        [InlineData("COM²")]
        [InlineData("COM³")]
        // LPT ports
        [InlineData("LPT1")]
        [InlineData("LPT2")]
        [InlineData("LPT3")]
        [InlineData("LPT4")]
        [InlineData("LPT5")]
        [InlineData("LPT6")]
        [InlineData("LPT7")]
        [InlineData("LPT8")]
        [InlineData("LPT9")]
        [InlineData("LPT¹")]
        [InlineData("LPT²")]
        [InlineData("LPT³")]
        public void IsReservedDeviceName_ValidReservedNames_ReturnsTrue(string segment)
        {
            // Act
            bool result = ReservedNames.IsReservedDeviceName(segment);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("con")]
        [InlineData("prn")]
        [InlineData("aux")]
        [InlineData("nul")]
        [InlineData("conin$")]
        [InlineData("conout$")]
        [InlineData("com1")]
        [InlineData("lpt1")]
        [InlineData("com¹")]
        [InlineData("lpt¹")]
        public void IsReservedDeviceName_CaseInsensitiveInputs_ReturnsTrue(string segment)
        {
            // Act
            bool result = ReservedNames.IsReservedDeviceName(segment);

            // Assert
            Assert.True(result);
        }

        [Theory]
        // Trailing spaces
        [InlineData("CON ")]
        [InlineData("COM1   ")]
        // Trailing periods
        [InlineData("NUL.")]
        [InlineData("AUX...")]
        // Trailing tabs
        [InlineData("PRN\t")]
        // Mixed trailing spaces, periods, and tabs
        [InlineData("CON. . \t.")]
        [InlineData("COM1.\t ")]
        [InlineData("LPT¹..  \t")]
        public void IsReservedDeviceName_TrailingSpacesPeriodsOrTabs_StripsAndReturnsTrue(string segment)
        {
            // Act
            bool result = ReservedNames.IsReservedDeviceName(segment);

            // Assert
            Assert.True(result);
        }

        [Theory]
        // Leading whitespace or characters (Win32 does not strip leading characters/whitespace)
        [InlineData(" CON")]
        [InlineData("\tPRN")]
        [InlineData(".NUL")]
        // Embedded characters
        [InlineData("CON1")]
        [InlineData("COM0")]
        [InlineData("COM10")]
        [InlineData("LPT0")]
        [InlineData("LPT10")]
        [InlineData("COM4_LOG")]
        // Standard safe filenames
        [InlineData("Console")]
        [InlineData("Auxiliary")]
        [InlineData("NullFile")]
        [InlineData("Service.log")]
        [InlineData("AppConfig.json")]
        public void IsReservedDeviceName_UnreservedNames_ReturnsFalse(string segment)
        {
            // Act
            bool result = ReservedNames.IsReservedDeviceName(segment);

            // Assert
            Assert.False(result);
        }

        #endregion
    }
}
