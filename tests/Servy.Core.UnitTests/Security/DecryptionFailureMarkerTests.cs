using Servy.Core.DTOs;
using Servy.Core.Security;
using Xunit;

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

        [Theory]
        [InlineData("CryptographicException")]
        [InlineData("FormatException")]
        public void Strip_DescriptionStartsWithCorruptMarker_ReturnsTheOriginalDescription(string rootCause)
        {
            // Arrange
            var description = string.Format(DecryptionFailureMarker.CorruptFormat, rootCause)
                + DecryptionFailureMarker.OriginalDescriptionSeparator + "My app";

            // Act
            var result = DecryptionFailureMarker.Strip(description);

            // Assert
            Assert.Equal("My app", result);
        }

        [Fact]
        public void Strip_DescriptionStartsWithLegacyBlockedMarker_ReturnsTheOriginalDescription()
        {
            // Arrange
            var description = DecryptionFailureMarker.LegacyBlocked
                + DecryptionFailureMarker.OriginalDescriptionSeparator + "My app";

            // Act
            var result = DecryptionFailureMarker.Strip(description);

            // Assert
            Assert.Equal("My app", result);
        }

        [Fact]
        public void Strip_StackedMarkers_RemovesEveryLeadingMarker()
        {
            // Arrange: the #5186 shape, a marker persisted in front of a marker
            var description = DecryptionFailureMarker.LegacyBlocked
                + DecryptionFailureMarker.OriginalDescriptionSeparator
                + string.Format(DecryptionFailureMarker.CorruptFormat, "CryptographicException")
                + DecryptionFailureMarker.OriginalDescriptionSeparator + "My app";

            // Act
            var result = DecryptionFailureMarker.Strip(description);

            // Assert
            Assert.Equal("My app", result);
        }

        [Theory]
        [InlineData("My app")]
        [InlineData("My app " + DecryptionFailureMarker.LegacyBlocked + DecryptionFailureMarker.OriginalDescriptionSeparator + "text")]
        [InlineData("")]
        public void Strip_DescriptionWithoutLeadingMarker_ReturnsItUnchanged(string description)
        {
            // Act
            var result = DecryptionFailureMarker.Strip(description);

            // Assert
            Assert.Equal(description, result);
        }

        [Fact]
        public void HasDecryptionFailure_NullService_ReturnsFalse()
        {
            // Act
            var result = DecryptionFailureMarker.HasDecryptionFailure(null);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(false, true, false)]
        public void HasDecryptionFailure_DecidesFromTheFlagAndNeverFromTheDescription(bool flag, bool markerInDescription, bool expected)
        {
            // Arrange: a v9.5 row can carry the marker text while its secrets decrypt cleanly (#7348)
            var service = new ServiceDto
            {
                Name = "svc",
                DecryptionFailed = flag,
                Description = markerInDescription
                    ? DecryptionFailureMarker.LegacyBlocked + DecryptionFailureMarker.OriginalDescriptionSeparator + "My app"
                    : "My app",
            };

            // Act
            var result = DecryptionFailureMarker.HasDecryptionFailure(service);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
