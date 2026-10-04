using Servy.Core.Security;

namespace Servy.Core.UnitTests.Security
{
    public class DecryptionFailureMarkerTests
    {
        [Fact]
        public void IsPresent_DescriptionStartsWithCorruptMarker_ReturnsTrue()
        {
            // Arrange
            var description = string.Format(DecryptionFailureMarker.CorruptFormat, "CryptographicException")
                + DecryptionFailureMarker.OriginalDescriptionSeparator + "My app";

            // Act
            var result = DecryptionFailureMarker.IsPresent(description);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsPresent_DescriptionStartsWithLegacyBlockedMarker_ReturnsTrue()
        {
            // Arrange
            var description = DecryptionFailureMarker.LegacyBlocked
                + DecryptionFailureMarker.OriginalDescriptionSeparator + "My app";

            // Act
            var result = DecryptionFailureMarker.IsPresent(description);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("My app")]
        [InlineData("My app " + DecryptionFailureMarker.LegacyBlocked + DecryptionFailureMarker.OriginalDescriptionSeparator + "text")]
        public void IsPresent_DescriptionWithoutLeadingMarker_ReturnsFalse(string description)
        {
            // Act
            var result = DecryptionFailureMarker.IsPresent(description);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsPresent_NullDescription_ReturnsFalse()
        {
            // Act
            var result = DecryptionFailureMarker.IsPresent(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsPresent_EmptyDescription_ReturnsFalse()
        {
            // Act
            var result = DecryptionFailureMarker.IsPresent(string.Empty);

            // Assert
            Assert.False(result);
        }
    }
}
