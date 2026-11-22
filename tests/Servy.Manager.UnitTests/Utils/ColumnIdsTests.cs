using Servy.Manager.Resources;
using Servy.Manager.Utils;
using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    public class ColumnIdsTests
    {
        private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

        [Theory]
        [InlineData("Column_Name")]
        [InlineData("Column_DisplayName")]
        [InlineData("Column_Status")]
        [InlineData("Column_StartupType")]
        [InlineData("Column_Time")]
        [InlineData("Column_Message")]
        public void FromHeader_TranslatedHeader_ReturnsTheEnglishHeader(string resourceKey)
        {
            // Arrange
            var english = Strings.ResourceManager.GetString(resourceKey, CultureInfo.InvariantCulture);
            var translated = Strings.ResourceManager.GetString(resourceKey, Chinese);

            // Act
            var id = ColumnIds.FromHeader(translated, Strings.ResourceManager, Chinese);

            // Assert
            Assert.NotNull(english);
            Assert.NotEqual(english, translated);
            Assert.Equal(english, id);
        }

        [Fact]
        public void FromHeader_EnglishHeader_ReturnsItUnchangedSoSettingsSavedBeforeTranslationsKeepMatching()
        {
            // Arrange
            var english = Strings.ResourceManager.GetString("Column_DisplayName", CultureInfo.InvariantCulture);

            // Act
            var id = ColumnIds.FromHeader(english, Strings.ResourceManager, English);

            // Assert
            Assert.Equal("Display Name", id);
        }

        [Fact]
        public void FromHeader_EveryColumnResource_MapsBackToADistinctEnglishHeader()
        {
            // Arrange
            var keys = typeof(Strings).GetProperties()
                .Select(p => p.Name)
                .Where(n => n.StartsWith("Column_", StringComparison.Ordinal))
                .ToList();

            // Act
            var ids = keys
                .Select(k => ColumnIds.FromHeader(Strings.ResourceManager.GetString(k, Chinese), Strings.ResourceManager, Chinese))
                .ToList();

            // Assert
            Assert.NotEmpty(keys);
            Assert.Equal(keys.Select(k => Strings.ResourceManager.GetString(k, CultureInfo.InvariantCulture)), ids);
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Theory]
        [InlineData("Not a column")]
        [InlineData("")]
        [InlineData(null)]
        public void FromHeader_HeaderThatIsNoColumnResource_ReturnsItUnchanged(string header)
        {
            // Act
            var id = ColumnIds.FromHeader(header, Strings.ResourceManager, Chinese);

            // Assert
            Assert.Equal(header, id);
        }

        [Fact]
        public void For_NullColumn_ReturnsNull()
        {
            // Act
            var id = ColumnIds.For(null);

            // Assert
            Assert.Null(id);
        }
    }
}
