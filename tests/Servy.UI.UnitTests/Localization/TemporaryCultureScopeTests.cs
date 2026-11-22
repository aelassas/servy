using Servy.UI.Localization;
using System.Globalization;

namespace Servy.UI.UnitTests.Localization
{
    public class TemporaryCultureScopeTests
    {
        #region Apply

        [Fact]
        public void Apply_SetsTheCurrentAndDefaultCultures()
        {
            // Arrange
            var culture = CultureInfo.GetCultureInfo("zh-CN");

            using (new TemporaryCultureScope(CultureInfo.InvariantCulture))
            {
                // Act
                TemporaryCultureScope.Apply(culture);

                // Assert
                Assert.Equal(culture, CultureInfo.CurrentUICulture);
                Assert.Equal(culture, CultureInfo.CurrentCulture);
                Assert.Equal(culture, CultureInfo.DefaultThreadCurrentUICulture);
                Assert.Equal(culture, CultureInfo.DefaultThreadCurrentCulture);
            }
        }

        [Fact]
        public void Constructor_CultureName_AppliesTheNormalizedCulture()
        {
            using (new TemporaryCultureScope("zh"))
            {
                Assert.Equal("zh-CN", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("zh-CN", CultureInfo.CurrentCulture.Name);
            }
        }

        [Fact]
        public void Constructor_UnsupportedCultureName_AppliesTheInvariantCulture()
        {
            using (new TemporaryCultureScope("fr-FR"))
            {
                Assert.Equal(string.Empty, CultureInfo.CurrentUICulture.Name);
            }
        }

        [Fact]
        public void Dispose_RestoresThePreviousCultures()
        {
            // Arrange
            var originalUi = CultureInfo.CurrentUICulture;
            var original = CultureInfo.CurrentCulture;
            var originalDefaultUi = CultureInfo.DefaultThreadCurrentUICulture;
            var originalDefault = CultureInfo.DefaultThreadCurrentCulture;

            try
            {
                // Act
                using (new TemporaryCultureScope(CultureInfo.GetCultureInfo("zh-CN")))
                {
                }

                // Assert
                Assert.Equal(originalUi, CultureInfo.CurrentUICulture);
                Assert.Equal(original, CultureInfo.CurrentCulture);
                Assert.Equal(originalDefaultUi, CultureInfo.DefaultThreadCurrentUICulture);
                Assert.Equal(originalDefault, CultureInfo.DefaultThreadCurrentCulture);
            }
            finally
            {
                CultureInfo.CurrentUICulture = originalUi;
                CultureInfo.CurrentCulture = original;
                CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUi;
                CultureInfo.DefaultThreadCurrentCulture = originalDefault;
            }
        }

        #endregion

        #region Validation

        [Fact]
        public void Constructor_NullCulture_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TemporaryCultureScope((CultureInfo)null!));
        }

        #endregion
    }
}
