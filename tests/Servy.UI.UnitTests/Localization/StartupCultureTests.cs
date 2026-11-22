using Moq;
using Newtonsoft.Json.Linq;
using Servy.Core.Logging;
using Servy.Testing;
using Servy.UI.Localization;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace Servy.UI.UnitTests.Localization
{
    // LogCapture re-initializes the process-wide static Logger, so every class that uses it runs in one collection
    [Collection("StaticLogger")]
    public class StartupCultureTests : IDisposable
    {
        private readonly string _directory;
        private readonly string _configPath;

        public StartupCultureTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ServyStartupCultureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _configPath = Path.Combine(_directory, "appsettings.desktop.json");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temporary directory
            }
        }

        private static CultureInfo Culture(string name)
        {
            return CultureInfo.GetCultureInfo(name);
        }

        #region ReadLanguage

        [Fact]
        public void ReadLanguage_MissingFile_ReturnsSystem()
        {
            // Act
            var language = StartupCulture.ReadLanguage(_configPath);

            // Assert
            Assert.Equal(AppLanguage.System, language);
        }

        [Theory]
        [InlineData("{\"Language\": \"zh-CN\"}", AppLanguage.ChineseSimplified)]
        [InlineData("{\"language\": \"en\"}", AppLanguage.English)]
        [InlineData("{\"LANGUAGE\": \"system\"}", AppLanguage.System)]
        [InlineData("{\"Language\": \"ChineseSimplified\"}", AppLanguage.ChineseSimplified)]
        [InlineData("{\"Language\": \"klingon\"}", AppLanguage.System)]
        [InlineData("{\"Language\": 7}", AppLanguage.System)]
        [InlineData("{\"LogLevel\": \"INFO\"}", AppLanguage.System)]
        [InlineData("{ this is not json", AppLanguage.System)]
        [InlineData("", AppLanguage.System)]
        public void ReadLanguage_FileContent_ReadsTheKeyInAnyCasingAndNeverThrows(string content, AppLanguage expected)
        {
            // Arrange
            File.WriteAllText(_configPath, content);

            // Act
            var language = StartupCulture.ReadLanguage(_configPath);

            // Assert
            Assert.Equal(expected, language);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ReadLanguage_NullOrBlankPath_ReturnsSystem(string? path)
        {
            // Act
            var language = StartupCulture.ReadLanguage(path!);

            // Assert
            Assert.Equal(AppLanguage.System, language);
        }

        #endregion

        #region WriteLanguage

        [Fact]
        public void WriteLanguage_ExistingFile_StoresTheLanguageKeepsTheOtherKeysAndLeavesNoStagingFile()
        {
            // Arrange
            File.WriteAllText(_configPath, "{\"LogLevel\": \"DEBUG\", \"LogRotationSizeMB\": 7}");

            // Act
            StartupCulture.WriteLanguage(_configPath, AppLanguage.ChineseSimplified);

            // Assert
            var json = JObject.Parse(File.ReadAllText(_configPath));
            Assert.Equal("zh-CN", json.Value<string>("Language"));
            Assert.Equal("DEBUG", json.Value<string>("LogLevel"));
            Assert.Equal(7, json.Value<int>("LogRotationSizeMB"));
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
            Assert.Equal(new[] { _configPath }, Directory.GetFiles(_directory));
        }

        [Fact]
        public void WriteLanguage_KeyStoredInAnotherCasing_ReplacesItInsteadOfAddingASecondKey()
        {
            // Arrange
            File.WriteAllText(_configPath, "{\"language\": \"zh-CN\"}");

            // Act
            StartupCulture.WriteLanguage(_configPath, AppLanguage.English);

            // Assert
            var json = JObject.Parse(File.ReadAllText(_configPath));
            var property = Assert.Single(json.Properties());
            Assert.Equal("language", property.Name);
            Assert.Equal("en", property.Value.ToString());
        }

        [Fact]
        public void WriteLanguage_MissingFile_CreatesAUtf8FileWithoutBomHoldingOnlyTheLanguage()
        {
            // Act
            StartupCulture.WriteLanguage(_configPath, AppLanguage.System);

            // Assert
            var bytes = File.ReadAllBytes(_configPath);
            Assert.NotEmpty(bytes);
            Assert.NotEqual(0xEF, bytes[0]);
            var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
            var property = Assert.Single(json.Properties());
            Assert.Equal("Language", property.Name);
            Assert.Equal("system", property.Value.ToString());
        }

        [Fact]
        public void WriteLanguage_MalformedFile_ThrowsAndLeavesTheFileUntouched()
        {
            // Arrange
            const string content = "{ this is not json";
            File.WriteAllText(_configPath, content);

            // Act
            var ex = Record.Exception(() => StartupCulture.WriteLanguage(_configPath, AppLanguage.English));

            // Assert
            Assert.IsAssignableFrom<Newtonsoft.Json.JsonException>(ex);
            Assert.Equal(content, File.ReadAllText(_configPath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WriteLanguage_NullOrBlankPath_ThrowsArgumentException(string? path)
        {
            // Act
            var ex = Assert.Throws<ArgumentException>(() => StartupCulture.WriteLanguage(path!, AppLanguage.English));

            // Assert
            Assert.Equal("configPath", ex.ParamName);
        }

        #endregion

        #region Settings files of both applications

        [Theory]
        [InlineData("appsettings.desktop.json", "appsettings.manager.json")]
        [InlineData("appsettings.manager.json", "appsettings.desktop.json")]
        [InlineData("APPSETTINGS.DESKTOP.JSON", "appsettings.manager.json")]
        public void GetSettingsFiles_KnownSettingsFile_ReturnsItFirstThenTheOtherApplicationsFile(string own, string other)
        {
            // Arrange
            var ownPath = Path.Combine(_directory, own);

            // Act
            var files = StartupCulture.GetSettingsFiles(ownPath);

            // Assert
            Assert.Equal(new[] { ownPath, Path.Combine(_directory, other) }, files);
        }

        [Fact]
        public void GetSettingsFiles_UnknownFileName_ReturnsOnlyThatFile()
        {
            // Arrange
            var path = Path.Combine(_directory, "custom.json");

            // Act
            var files = StartupCulture.GetSettingsFiles(path);

            // Assert
            Assert.Equal(new[] { path }, files);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetSettingsFiles_NullOrBlankPath_ReturnsNoFile(string? path)
        {
            // Act
            var files = StartupCulture.GetSettingsFiles(path!);

            // Assert
            Assert.Empty(files);
        }

        [Fact]
        public void WriteLanguageToAll_BothFilesWritable_StoresTheLanguageInBothAndKeepsTheirOtherKeys()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(_configPath, "{\"LogLevel\": \"DEBUG\"}");
            File.WriteAllText(managerPath, "{\"Language\": \"en\", \"RefreshIntervalInSeconds\": 7}");

            // Act
            var failures = StartupCulture.WriteLanguageToAll(_configPath, AppLanguage.ChineseSimplified);

            // Assert
            Assert.Empty(failures);
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(managerPath));
            Assert.Equal("DEBUG", JObject.Parse(File.ReadAllText(_configPath)).Value<string>("LogLevel"));
            Assert.Equal(7, JObject.Parse(File.ReadAllText(managerPath)).Value<int>("RefreshIntervalInSeconds"));
        }

        [Fact]
        public void WriteLanguageToAll_OtherFileMissing_CreatesItSoBothApplicationsAgree()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");

            // Act
            var failures = StartupCulture.WriteLanguageToAll(_configPath, AppLanguage.English);

            // Assert
            Assert.Empty(failures);
            Assert.Equal(AppLanguage.English, StartupCulture.ReadLanguage(_configPath));
            Assert.Equal(AppLanguage.English, StartupCulture.ReadLanguage(managerPath));
        }

        [Fact]
        public void WriteLanguageToAll_FromTheManagerWithTheDesktopFileMissing_CreatesTheDesktopFile()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            Assert.False(File.Exists(_configPath));

            // Act
            var failures = StartupCulture.WriteLanguageToAll(managerPath, AppLanguage.ChineseSimplified);

            // Assert
            Assert.Empty(failures);
            Assert.True(File.Exists(_configPath));
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(managerPath));
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
        }

        [Fact]
        public void WriteLanguageToAll_OtherFileUnwritable_WritesTheOwnFileAndReportsTheOtherByName()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(managerPath, "{ this is not json");

            // Act
            var failures = StartupCulture.WriteLanguageToAll(_configPath, AppLanguage.ChineseSimplified);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
            Assert.StartsWith("appsettings.manager.json: ", Assert.Single(failures));
            Assert.Equal("{ this is not json", File.ReadAllText(managerPath));
        }

        [Fact]
        public void WriteLanguageToAll_OwnFileUnwritable_ThrowsAndLeavesTheOtherFileAlone()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(_configPath, "{ this is not json");
            File.WriteAllText(managerPath, "{\"Language\": \"en\"}");

            // Act
            var ex = Record.Exception(() => StartupCulture.WriteLanguageToAll(_configPath, AppLanguage.ChineseSimplified));

            // Assert
            Assert.NotNull(ex);
            Assert.Equal(AppLanguage.English, StartupCulture.ReadLanguage(managerPath));
        }

        #endregion

        #region Command line

        [Theory]
        [InlineData(new[] { "--lang", "zh-CN" }, "zh-CN")]
        [InlineData(new[] { "--LANG", "en" }, "en")]
        [InlineData(new[] { "--lang=zh-CN" }, "zh-CN")]
        [InlineData(new[] { "--force-sr", "--lang", "en", "MyService" }, "en")]
        [InlineData(new[] { "false", "MyService", "--lang=system" }, "system")]
        [InlineData(new[] { "--lang=" }, "")]
        public void TryGetLanguageArg_OptionPresent_ReturnsItsValue(string[] args, string expected)
        {
            // Act
            var found = StartupCulture.TryGetLanguageArg(args, out var value);

            // Assert
            Assert.True(found);
            Assert.Equal(expected, value);
        }

        [Fact]
        public void TryGetLanguageArg_OptionIsTheLastArgument_ReturnsTrueWithoutAValue()
        {
            // Act
            var found = StartupCulture.TryGetLanguageArg(new[] { "MyService", "--lang" }, out var value);

            // Assert
            Assert.True(found);
            Assert.Null(value);
        }

        [Fact]
        public void TryGetLanguageArg_OptionAbsentOrNoArguments_ReturnsFalse()
        {
            // Act
            var absent = StartupCulture.TryGetLanguageArg(new[] { "--force-sr", "MyService", "--language", "en" }, out var absentValue);
            var none = StartupCulture.TryGetLanguageArg(null, out var noneValue);

            // Assert
            Assert.False(absent);
            Assert.Null(absentValue);
            Assert.False(none);
            Assert.Null(noneValue);
        }

        [Theory]
        [InlineData(new[] { "--lang", "zh-CN", "false", "MyService" }, new[] { "false", "MyService" })]
        [InlineData(new[] { "false", "--lang=en", "MyService" }, new[] { "false", "MyService" })]
        [InlineData(new[] { "MyService", "--LANG", "en" }, new[] { "MyService" })]
        [InlineData(new[] { "MyService", "--lang" }, new[] { "MyService" })]
        [InlineData(new[] { "--force-sr", "MyService" }, new[] { "--force-sr", "MyService" })]
        public void StripLanguageArg_Arguments_RemovesTheOptionAndItsValueAndKeepsTheOrder(string[] args, string[] expected)
        {
            // Act
            var remaining = StartupCulture.StripLanguageArg(args);

            // Assert
            Assert.Equal(expected, remaining);
        }

        [Fact]
        public void StripLanguageArg_NoArguments_ReturnsAnEmptyList()
        {
            // Act
            var remaining = StartupCulture.StripLanguageArg(null);

            // Assert
            Assert.Empty(remaining);
        }

        #endregion

        #region Apply

        [Fact]
        public void Apply_LanguageInSettingsAndNoOption_AppliesTheStoredLanguage()
        {
            // Arrange
            File.WriteAllText(_configPath, "{\"Language\": \"zh-CN\"}");
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "MyService" }, Culture("en-US"), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, result.Language);
            Assert.Equal("zh-CN", Assert.Single(applied).Name);
            Assert.Equal("zh-CN", result.Culture?.Name);
            Assert.Null(result.RejectedArgument);
            Assert.Null(result.SyncError);
            Assert.Same(result, StartupCulture.Last);
        }

        [Fact]
        public void Apply_OptionDiffersFromSettings_OptionWinsAndIsWrittenToTheSettingsFile()
        {
            // Arrange
            File.WriteAllText(_configPath, "{\"Language\": \"zh-CN\", \"LogLevel\": \"INFO\"}");
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang", "en" }, Culture("zh-CN"), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.English, result.Language);
            Assert.Equal("en", Assert.Single(applied).Name);
            Assert.Null(result.SyncError);
            var json = JObject.Parse(File.ReadAllText(_configPath));
            Assert.Equal("en", json.Value<string>("Language"));
            Assert.Equal("INFO", json.Value<string>("LogLevel"));
        }

        [Fact]
        public void Apply_OptionWithoutASettingsFile_CreatesTheFileSoTheNextStartUsesTheSameLanguage()
        {
            // Arrange
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang=zh-CN" }, Culture("en-US"), applied.Add);
            var next = StartupCulture.Apply(_configPath, new string[0], Culture("en-US"), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, result.Language);
            Assert.Equal(AppLanguage.ChineseSimplified, next.Language);
            Assert.Equal(new[] { "zh-CN", "zh-CN" }, applied.Select(c => c.Name));
        }

        [Fact]
        public void Apply_Option_IsWrittenToTheSettingsFileOfTheOtherApplicationToo()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(_configPath, "{\"Language\": \"zh-CN\"}");
            File.WriteAllText(managerPath, "{\"Language\": \"en\"}");

            // Act: the option equals the own setting, but the other application disagrees
            var result = StartupCulture.Apply(_configPath, new[] { "--lang", "zh-CN" }, Culture("en-US"), _ => { });

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, result.Language);
            Assert.Null(result.SyncError);
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(managerPath));
        }

        [Fact]
        public void Apply_OptionCannotBeWrittenForTheOtherApplication_ReportsThatFileAndStillApplies()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(managerPath, "{ this is not json");
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang=zh-CN" }, Culture("en-US"), applied.Add);

            // Assert
            Assert.Equal("zh-CN", Assert.Single(applied).Name);
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
            Assert.Contains("appsettings.manager.json", result.SyncError);
        }

        [Fact]
        public void Apply_OptionEqualsSettings_DoesNotRewriteTheFile()
        {
            // Arrange
            const string content = "{\"Language\":\"en\"}";
            File.WriteAllText(_configPath, content);
            File.WriteAllText(Path.Combine(_directory, "appsettings.manager.json"), content);

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang", "EN" }, Culture("en-US"), _ => { });

            // Assert
            Assert.Equal(AppLanguage.English, result.Language);
            Assert.Equal(content, File.ReadAllText(_configPath));
        }

        [Theory]
        [InlineData("klingon")]
        [InlineData("zh-TW")]
        [InlineData("")]
        public void Apply_UnsupportedOption_IsIgnoredReportedAndLeavesTheSettingsAlone(string value)
        {
            // Arrange
            const string content = "{\"Language\":\"zh-CN\"}";
            File.WriteAllText(_configPath, content);
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang=" + value }, Culture("en-US"), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, result.Language);
            Assert.Equal(value, result.RejectedArgument);
            Assert.Equal("zh-CN", Assert.Single(applied).Name);
            Assert.Equal(content, File.ReadAllText(_configPath));
        }

        [Fact]
        public void Apply_OptionCannotBeWrittenToTheSettingsFile_StillAppliesItAndReportsTheFailure()
        {
            // Arrange: a directory where the settings file should be makes the write fail
            Directory.CreateDirectory(_configPath);
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang", "zh-CN" }, Culture("en-US"), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, result.Language);
            Assert.Equal("zh-CN", Assert.Single(applied).Name);
            Assert.False(string.IsNullOrEmpty(result.SyncError));
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("fr-FR")]
        [InlineData("zh-TW")]
        public void Apply_SystemOnANonSimplifiedChineseDisplayLanguage_LeavesTheCultureAlone(string systemCulture)
        {
            // Arrange
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new string[0], Culture(systemCulture), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.System, result.Language);
            Assert.Null(result.Culture);
            Assert.Empty(applied);
        }

        [Fact]
        public void Apply_SystemOnASimplifiedChineseDisplayLanguage_AppliesZhCn()
        {
            // Arrange
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, null, Culture("zh-SG"), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.System, result.Language);
            Assert.Equal("zh-CN", Assert.Single(applied).Name);
        }

        [Fact]
        public void Apply_NullApplyCulture_ThrowsArgumentNullException()
        {
            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => StartupCulture.Apply(_configPath, null, Culture("en-US"), null!));

            // Assert
            Assert.Equal("applyCulture", ex.ParamName);
        }

        [Fact]
        public void Apply_OptionWithoutAValue_IsReportedAsAnEmptyRejectedValueAndLeavesTheSettingsAlone()
        {
            // Arrange
            const string content = "{\"Language\":\"zh-CN\"}";
            File.WriteAllText(_configPath, content);
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "MyService", "--lang" }, Culture("en-US"), applied.Add);

            // Assert
            Assert.Equal(string.Empty, result.RejectedArgument);
            Assert.Equal(AppLanguage.ChineseSimplified, result.Language);
            Assert.Equal("zh-CN", Assert.Single(applied).Name);
            Assert.Equal(content, File.ReadAllText(_configPath));
        }

        [Fact]
        public void ApplyUiCulture_Culture_SetsTheCurrentAndTheDefaultThreadUiCulture()
        {
            // Arrange
            var previousCurrent = CultureInfo.CurrentUICulture;
            var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
            var english = Culture("en");

            try
            {
                // Act
                StartupCulture.ApplyUiCulture(english);

                // Assert
                Assert.Same(english, CultureInfo.CurrentUICulture);
                Assert.Same(english, CultureInfo.DefaultThreadCurrentUICulture);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCurrent;
                CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
            }
        }

        [Fact]
        public void ApplyUiCulture_NullCulture_ThrowsArgumentNullException()
        {
            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => StartupCulture.ApplyUiCulture(null!));

            // Assert
            Assert.Equal("culture", ex.ParamName);
        }

        #endregion

        #region LogOutcome

        [Fact]
        public void LogOutcome_RejectedOption_WarnsWithTheValueAndTheSupportedNames()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var result = new StartupCultureResult(AppLanguage.System, null, "klingon", null);

            // Act
            StartupCulture.LogOutcome(result, logger.Object);

            // Assert
            logger.Verify(l => l.Warn(It.Is<string>(m =>
                m.Contains("--lang") && m.Contains("'klingon'") && m.Contains("system") && m.Contains("en") && m.Contains("zh-CN")), null), Times.Once);
        }

        [Fact]
        public void LogOutcome_SyncFailure_WarnsWithTheReason()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var result = new StartupCultureResult(AppLanguage.English, Culture("en"), null, "Access to the path is denied.");

            // Act
            StartupCulture.LogOutcome(result, logger.Object);

            // Assert
            logger.Verify(l => l.Warn(It.Is<string>(m => m.Contains("Language") && m.Contains("Access to the path is denied.")), null), Times.Once);
        }

        [Fact]
        public void LogOutcome_NothingToReport_LogsNothing()
        {
            // Arrange
            var logger = new Mock<IServyLogger>(MockBehavior.Strict);

            // Act
            StartupCulture.LogOutcome(new StartupCultureResult(AppLanguage.ChineseSimplified, Culture("zh-CN"), null, null), logger.Object);
            StartupCulture.LogOutcome(null, logger.Object);

            // Assert
            logger.VerifyNoOtherCalls();
        }

        [Fact]
        public void LogOutcome_NoLoggerGiven_WritesBothWarningsToTheStaticLogger()
        {
            // Arrange
            var result = new StartupCultureResult(AppLanguage.System, null, "klingon", "Access to the path is denied.");

            // Act
            var log = LogCapture.Run(() => StartupCulture.LogOutcome(result));

            // Assert
            Assert.Contains("Ignored --lang 'klingon'", log);
            Assert.Contains("Access to the path is denied.", log);
        }

        #endregion
    }
}
