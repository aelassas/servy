using Newtonsoft.Json.Linq;
using Servy.Testing;
using Servy.UI.Localization;
using System.Globalization;

namespace Servy.UI.UnitTests.Localization
{
    public class StartupCultureTests
    {
        private static CultureInfo Chinese => CultureInfo.GetCultureInfo("zh-CN");

        #region ReadLanguage

        [Fact]
        public void ReadLanguage_MissingFile_ReturnsSystem()
        {
            using (var file = new TempFile(".json"))
            {
                Assert.Equal(AppLanguage.System, StartupCulture.ReadLanguage(file.Path));
            }
        }

        [Fact]
        public void ReadLanguage_BlankPath_ReturnsSystem()
        {
            Assert.Equal(AppLanguage.System, StartupCulture.ReadLanguage("   "));
        }

        [Fact]
        public void ReadLanguage_MalformedJson_ReturnsSystem()
        {
            using (var file = new TempFile(".json").Write("{ not json"))
            {
                Assert.Equal(AppLanguage.System, StartupCulture.ReadLanguage(file.Path));
            }
        }

        [Theory]
        [InlineData("{\"Language\":\"English\"}", AppLanguage.English)]
        [InlineData("{\"Language\":\"ChineseSimplified\"}", AppLanguage.ChineseSimplified)]
        [InlineData("{\"Language\":\"System\"}", AppLanguage.System)]
        [InlineData("{\"language\":\"ChineseSimplified\"}", AppLanguage.ChineseSimplified)]
        [InlineData("{\"Language\":\"nonsense\"}", AppLanguage.System)]
        [InlineData("{\"Other\":\"English\"}", AppLanguage.System)]
        public void ReadLanguage_ReadsBothSpellingsAndFallsBackOnUnknownValues(string content, AppLanguage expected)
        {
            using (var file = new TempFile(".json").Write(content))
            {
                Assert.Equal(expected, StartupCulture.ReadLanguage(file.Path));
            }
        }

        #endregion

        #region ApplyFromSettings

        [Fact]
        public void ApplyFromSettings_ChineseSetting_AppliesChineseRegardlessOfSystem()
        {
            using (var file = new TempFile(".json").Write("{\"Language\":\"ChineseSimplified\"}"))
            using (new TemporaryCultureScope(CultureInfo.InvariantCulture))
            {
                var language = StartupCulture.ApplyFromSettings(file.Path, CultureInfo.GetCultureInfo("en-US"));

                Assert.Equal(AppLanguage.ChineseSimplified, language);
                Assert.Equal("zh-CN", CultureInfo.CurrentUICulture.Name);
            }
        }

        [Fact]
        public void ApplyFromSettings_SystemSetting_FollowsTheSystemCulture()
        {
            using (var file = new TempFile(".json").Write("{\"Language\":\"System\"}"))
            using (new TemporaryCultureScope(CultureInfo.InvariantCulture))
            {
                StartupCulture.ApplyFromSettings(file.Path, Chinese);

                Assert.Equal("zh-CN", CultureInfo.CurrentUICulture.Name);
            }
        }

        [Fact]
        public void ApplyFromSettings_EnglishSetting_OverridesAChineseSystem()
        {
            using (var file = new TempFile(".json").Write("{\"Language\":\"English\"}"))
            using (new TemporaryCultureScope(CultureInfo.InvariantCulture))
            {
                StartupCulture.ApplyFromSettings(file.Path, Chinese);

                Assert.Equal(string.Empty, CultureInfo.CurrentUICulture.Name);
            }
        }

        [Fact]
        public void ApplyFromSettings_MissingFile_FollowsTheSystemCultureAndNeverThrows()
        {
            using (var file = new TempFile(".json"))
            using (new TemporaryCultureScope(CultureInfo.InvariantCulture))
            {
                Assert.Equal(AppLanguage.System, StartupCulture.ApplyFromSettings(file.Path, Chinese));
                Assert.Equal("zh-CN", CultureInfo.CurrentUICulture.Name);
            }
        }

        #endregion

        #region WriteLanguage

        [Fact]
        public void WriteLanguage_CreatesTheFileWithTheLanguageAndTheRestartFlag()
        {
            using (var file = new TempFile(".json"))
            {
                StartupCulture.WriteLanguage(file.Path, StartupCulture.LanguageSettingKey, AppLanguage.ChineseSimplified);

                var root = JObject.Parse(File.ReadAllText(file.Path));
                Assert.Equal("ChineseSimplified", root[StartupCulture.LanguageSettingKey]?.ToString());
                Assert.True(root[StartupCulture.LanguageChangedSettingKey]?.Value<bool>());
                Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(file.Path));
            }
        }

        [Fact]
        public void WriteLanguage_KeepsTheOtherSettingsInTheFile()
        {
            using (var file = new TempFile(".json").Write("{\"LogLevel\":\"DEBUG\",\"Language\":\"English\"}"))
            {
                StartupCulture.WriteLanguage(file.Path, StartupCulture.LanguageSettingKey, AppLanguage.System);

                var root = JObject.Parse(File.ReadAllText(file.Path));
                Assert.Equal("DEBUG", root["LogLevel"]?.ToString());
                Assert.Equal("System", root[StartupCulture.LanguageSettingKey]?.ToString());
            }
        }

        [Fact]
        public void WriteLanguage_WritesUnderTheGivenKey()
        {
            using (var file = new TempFile(".json"))
            {
                StartupCulture.WriteLanguage(file.Path, "language", AppLanguage.English);

                var root = JObject.Parse(File.ReadAllText(file.Path));
                Assert.Equal("English", root["language"]?.ToString());
                Assert.Null(root[StartupCulture.LanguageSettingKey]);
                Assert.Equal(AppLanguage.English, StartupCulture.ReadLanguage(file.Path));
            }
        }

        #endregion
    }
}
