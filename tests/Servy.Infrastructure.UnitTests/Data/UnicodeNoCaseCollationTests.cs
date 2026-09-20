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
            Assert.Equal(0, _collation.Compare(null, null));
        }

        [Fact]
        public void Compare_FirstNull_ReturnsNegative()
        {
            Assert.True(_collation.Compare(null, "a") < 0);
        }

        [Fact]
        public void Compare_SecondNull_ReturnsPositive()
        {
            Assert.True(_collation.Compare("a", null) > 0);
        }

        [Fact]
        public void Compare_DifferentCasing_TreatedAsEqual()
        {
            Assert.Equal(0, _collation.Compare("A", "a"));
        }
    }
}
