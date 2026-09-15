using Servy.Manager.Converters;

namespace Servy.Manager.UnitTests.Converters
{
    /// <summary>
    /// Tests for <see cref="EnumLocalizedConverter{TEnum}"/> itself. Both concrete converters pass a
    /// <c>static readonly</c> dictionary literal to the base constructor, so its null-map guard cannot
    /// be reached through them and needs a test-only derived class.
    /// </summary>
    public class EnumLocalizedConverterTests
    {
        /// <summary>
        /// Minimal concrete converter used only to drive the base constructor.
        /// </summary>
        private sealed class TestEnumConverter : EnumLocalizedConverter<DayOfWeek>
        {
            public TestEnumConverter(Dictionary<DayOfWeek, Func<string>> map) : base(map)
            {
            }

            protected override string GetFallbackValue(object? value) => string.Empty;
        }

        [Fact]
        public void Constructor_NullMap_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>("map", () => new TestEnumConverter(null!));
        }
    }
}
