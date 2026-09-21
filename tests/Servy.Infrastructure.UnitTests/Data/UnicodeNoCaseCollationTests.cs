using Servy.Infrastructure.Data;

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
    }
}
