using Servy.Infrastructure.Data;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class UnicodeNoCaseCollationTests
    {
        private readonly UnicodeNoCaseCollation _collation = new UnicodeNoCaseCollation();

        [Fact]
        public void Compare_BothNull_ReturnsZero()
        {
            // Arrange & Act
            int result = _collation.Compare(null, null);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_FirstNull_ReturnsNegative()
        {
            // Arrange & Act
            int result = _collation.Compare(null, "a");

            // Assert
            Assert.True(result < 0);
        }

        [Fact]
        public void Compare_SecondNull_ReturnsPositive()
        {
            // Arrange & Act
            int result = _collation.Compare("a", null);

            // Assert
            Assert.True(result > 0);
        }

        [Fact]
        public void Compare_DifferentCasing_TreatedAsEqual()
        {
            // Arrange & Act
            int result = _collation.Compare("A", "a");

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_AccentedCasing_TreatedAsEqual()
        {
            // Arrange
            // The doc comment's own example ('ä' matches 'Ä') - the specific behavior that
            // distinguishes this collation from SQLite's built-in, ASCII-only NOCASE.

            // Act
            int result = _collation.Compare("ä", "Ä");

            // Assert
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData("a", "b")]
        [InlineData("A", "b")]
        [InlineData("a", "B")]
        [InlineData("ab", "abc")]
        public void Compare_DifferentStrings_OrdersConsistentlyAndAntisymmetrically(string lower, string higher)
        {
            // Arrange & Act
            int forward = _collation.Compare(lower, higher);
            int backward = _collation.Compare(higher, lower);

            // Assert
            Assert.True(forward < 0);
            Assert.True(backward > 0);
        }

        [Fact]
        public void Compare_PunctuationVersusLetter_UsesOrdinalFoldingNotLinguisticOrder()
        {
            // Arrange
            // OrdinalIgnoreCase folds to upper case, so '_' (0x5F) sorts AFTER 'a' (folded to 'A', 0x41).
            // A culture-sensitive comparison puts punctuation BEFORE letters, so this pair separates the two.

            // Act
            int result = _collation.Compare("_", "a");

            // Assert
            Assert.True(result > 0);
        }
    }
}
