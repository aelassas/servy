using Servy.Core.Resources;
using System.Globalization;
using Xunit;

namespace Servy.Core.UnitTests.Localization
{
    public class ResourceLanguageTests
    {
        private const string English = "An unexpected error occurred.";

        private static string ReadUnderChineseUiCulture()
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                return Strings.Msg_UnexpectedError;
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        [Fact]
        public void UseEnglish_ChineseUiCulture_KeepsTheCoreStringsInEnglishUntilFollowUiCultureIsCalled()
        {
            try
            {
                // Arrange: without the pin the translated satellite assembly answers under a Chinese UI culture
                ResourceLanguage.FollowUiCulture();
                var translated = ReadUnderChineseUiCulture();

                // Act
                ResourceLanguage.UseEnglish();
                var pinned = ReadUnderChineseUiCulture();
                ResourceLanguage.FollowUiCulture();
                var released = ReadUnderChineseUiCulture();

                // Assert
                Assert.NotEqual(English, translated);
                Assert.Equal(English, pinned);
                Assert.Equal(translated, released);
            }
            finally
            {
                ResourceLanguage.FollowUiCulture();
            }
        }
    }
}
