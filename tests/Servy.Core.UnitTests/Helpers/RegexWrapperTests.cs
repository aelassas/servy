using System.Text.RegularExpressions;
using RegexWrapperImpl = Servy.Core.RegexWrapper.RegexWrapper;

namespace Servy.Core.UnitTests.Helpers
{
    public class RegexWrapperTests
    {
        [Fact]
        public void Constructor_NullRegex_ThrowsArgumentNullException()
        {
            // Arrange
            Regex regex = null!;

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new RegexWrapperImpl(regex));

            // Assert
            Assert.Equal("regex", ex.ParamName);
        }

        [Fact]
        public void MatchValues_ReturnsEveryMatchInOrder()
        {
            // Arrange
            var sut = new RegexWrapperImpl(new Regex("%[^%]+%"));

            // Act
            var values = sut.MatchValues("a %ONE% b %TWO% c").ToList();

            // Assert
            Assert.Equal(new[] { "%ONE%", "%TWO%" }, values);
        }

        [Fact]
        public void MatchValues_NullInput_ThrowsOnlyWhenEnumerated()
        {
            // Arrange
            var sut = new RegexWrapperImpl(new Regex("%[^%]+%"));

            // Act
            var values = sut.MatchValues(null!);

            // Assert
            Assert.Throws<ArgumentNullException>(() => values.ToList());
        }
    }
}
