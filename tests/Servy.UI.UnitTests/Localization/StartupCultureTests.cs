using Moq;
using Servy.Core.Logging;
using Servy.Testing;
using Servy.UI.Localization;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
            _configPath = Path.Combine(_directory, "Servy.exe.config");
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

        private static string Config(params string[] keysAndValues)
        {
            var settings = string.Empty;
            for (var i = 0; i < keysAndValues.Length; i += 2)
            {
                settings += "    <add key=\"" + keysAndValues[i] + "\" value=\"" + keysAndValues[i + 1] + "\" />\r\n";
            }

            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <appSettings>\r\n" + settings + "  </appSettings>\r\n</configuration>\r\n";
        }

        private static Dictionary<string, string> ReadSettings(string path)
        {
            return XDocument.Load(path).Root.Element("appSettings").Elements("add")
                .ToDictionary(e => e.Attribute("key").Value, e => e.Attribute("value").Value);
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
        [InlineData("Language", "zh-CN", AppLanguage.ChineseSimplified)]
        [InlineData("Language", "en", AppLanguage.English)]
        [InlineData("Language", "system", AppLanguage.System)]
        [InlineData("Language", "ChineseSimplified", AppLanguage.ChineseSimplified)]
        [InlineData("Language", "klingon", AppLanguage.System)]
        [InlineData("Language", "", AppLanguage.System)]
        [InlineData("LogLevel", "INFO", AppLanguage.System)]
        public void ReadLanguage_StoredSetting_ReadsTheLanguageAndFallsBackToSystem(string key, string value, AppLanguage expected)
        {
            // Arrange
            File.WriteAllText(_configPath, Config(key, value));

            // Act
            var language = StartupCulture.ReadLanguage(_configPath);

            // Assert
            Assert.Equal(expected, language);
        }

        [Theory]
        [InlineData("<configuration><appSettings><add key=")]
        [InlineData("this is not a configuration file")]
        [InlineData("")]
        public void ReadLanguage_UnreadableFile_ReturnsSystemAndNeverThrows(string content)
        {
            // Arrange
            File.WriteAllText(_configPath, content);

            // Act
            var language = StartupCulture.ReadLanguage(_configPath);

            // Assert
            Assert.Equal(AppLanguage.System, language);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ReadLanguage_NullOrBlankPath_ReturnsSystem(string path)
        {
            // Act
            var language = StartupCulture.ReadLanguage(path);

            // Assert
            Assert.Equal(AppLanguage.System, language);
        }

        #endregion

        #region WriteLanguage

        [Fact]
        public void WriteLanguage_ExistingFile_StoresTheLanguageAndKeepsTheOtherSettings()
        {
            // Arrange
            File.WriteAllText(_configPath, Config("LogLevel", "DEBUG", "LogRotationSizeMB", "7"));

            // Act
            StartupCulture.WriteLanguage(_configPath, AppLanguage.ChineseSimplified);

            // Assert
            var settings = ReadSettings(_configPath);
            Assert.Equal("zh-CN", settings["Language"]);
            Assert.Equal("DEBUG", settings["LogLevel"]);
            Assert.Equal("7", settings["LogRotationSizeMB"]);
            Assert.Equal(3, settings.Count);
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
        }

        [Fact]
        public void WriteLanguage_LanguageAlreadyStored_ReplacesItInsteadOfAddingASecondEntry()
        {
            // Arrange
            File.WriteAllText(_configPath, Config("Language", "zh-CN"));

            // Act
            StartupCulture.WriteLanguage(_configPath, AppLanguage.English);

            // Assert
            var entry = Assert.Single(XDocument.Load(_configPath).Root.Element("appSettings").Elements("add"));
            Assert.Equal("Language", entry.Attribute("key").Value);
            Assert.Equal("en", entry.Attribute("value").Value);
        }

        [Fact]
        public void WriteLanguage_MissingFile_CreatesAConfigurationFileHoldingTheLanguage()
        {
            // Act
            StartupCulture.WriteLanguage(_configPath, AppLanguage.System);

            // Assert
            Assert.True(File.Exists(_configPath));
            Assert.Equal("system", ReadSettings(_configPath)["Language"]);
            Assert.Equal(AppLanguage.System, StartupCulture.ReadLanguage(_configPath));
        }

        [Fact]
        public void WriteLanguage_MalformedFile_ThrowsAndLeavesTheFileUntouched()
        {
            // Arrange
            const string content = "<configuration><appSettings><add key=";
            File.WriteAllText(_configPath, content);

            // Act
            var ex = Record.Exception(() => StartupCulture.WriteLanguage(_configPath, AppLanguage.English));

            // Assert
            Assert.IsAssignableFrom<ConfigurationException>(ex);
            Assert.Equal(content, File.ReadAllText(_configPath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WriteLanguage_NullOrBlankPath_ThrowsArgumentException(string path)
        {
            // Act
            var ex = Assert.Throws<ArgumentException>(() => StartupCulture.WriteLanguage(path, AppLanguage.English));

            // Assert
            Assert.Equal("configPath", ex.ParamName);
        }

        #endregion

        #region Settings files of both applications

        [Theory]
        [InlineData("Servy.exe.config", "Servy.Manager.exe.config")]
        [InlineData("Servy.Manager.exe.config", "Servy.exe.config")]
        [InlineData("SERVY.EXE.CONFIG", "Servy.Manager.exe.config")]
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
            var path = Path.Combine(_directory, "custom.config");

            // Act
            var files = StartupCulture.GetSettingsFiles(path);

            // Assert
            Assert.Equal(new[] { path }, files);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetSettingsFiles_NullOrBlankPath_ReturnsNoFile(string path)
        {
            // Act
            var files = StartupCulture.GetSettingsFiles(path);

            // Assert
            Assert.Empty(files);
        }

        [Fact]
        public void WriteLanguageToAll_BothFilesWritable_StoresTheLanguageInBothAndKeepsTheirOtherKeys()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            File.WriteAllText(_configPath, Config("LogLevel", "DEBUG"));
            File.WriteAllText(managerPath, Config("Language", "en", "RefreshIntervalInSeconds", "7"));

            // Act
            var failures = StartupCulture.WriteLanguageToAll(_configPath, AppLanguage.ChineseSimplified);

            // Assert
            Assert.Empty(failures);
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(managerPath));
            Assert.Equal("DEBUG", ReadSettings(_configPath)["LogLevel"]);
            Assert.Equal("7", ReadSettings(managerPath)["RefreshIntervalInSeconds"]);
        }

        [Fact]
        public void WriteLanguageToAll_OtherFileMissing_CreatesItSoBothApplicationsAgree()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");

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
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
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
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            File.WriteAllText(managerPath, "<configuration><appSettings><add key=");

            // Act
            var failures = StartupCulture.WriteLanguageToAll(_configPath, AppLanguage.ChineseSimplified);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
            Assert.StartsWith("Servy.Manager.exe.config: ", Assert.Single(failures));
            Assert.Equal("<configuration><appSettings><add key=", File.ReadAllText(managerPath));
        }

        [Fact]
        public void WriteLanguageToAll_OwnFileUnwritable_ThrowsAndLeavesTheOtherFileAlone()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            File.WriteAllText(_configPath, "<configuration><appSettings><add key=");
            File.WriteAllText(managerPath, Config("Language", "en"));

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
            File.WriteAllText(_configPath, Config("Language", "zh-CN"));
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
        public void Apply_WithoutADelegateOrASystemCulture_SwitchesTheUiCultureToTheStoredLanguage()
        {
            // Arrange
            File.WriteAllText(_configPath, Config("Language", "en"));
            var previousCurrent = CultureInfo.CurrentUICulture;
            var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;

            try
            {
                // Act
                var result = StartupCulture.Apply(_configPath, new[] { "MyService" });

                // Assert
                Assert.Equal(AppLanguage.English, result.Language);
                Assert.Equal("en", result.Culture?.Name);
                Assert.Same(result.Culture, CultureInfo.CurrentUICulture);
                Assert.Same(result.Culture, CultureInfo.DefaultThreadCurrentUICulture);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCurrent;
                CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
            }
        }

        [Fact]
        public void Apply_OptionDiffersFromSettings_OptionWinsAndIsWrittenToTheSettingsFile()
        {
            // Arrange
            File.WriteAllText(_configPath, Config("Language", "zh-CN", "LogLevel", "INFO"));
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang", "en" }, Culture("zh-CN"), applied.Add);

            // Assert
            Assert.Equal(AppLanguage.English, result.Language);
            Assert.Equal("en", Assert.Single(applied).Name);
            Assert.Null(result.SyncError);
            var settings = ReadSettings(_configPath);
            Assert.Equal("en", settings["Language"]);
            Assert.Equal("INFO", settings["LogLevel"]);
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
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            File.WriteAllText(_configPath, Config("Language", "zh-CN"));
            File.WriteAllText(managerPath, Config("Language", "en"));

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
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            File.WriteAllText(managerPath, "<configuration><appSettings><add key=");
            var applied = new List<CultureInfo>();

            // Act
            var result = StartupCulture.Apply(_configPath, new[] { "--lang=zh-CN" }, Culture("en-US"), applied.Add);

            // Assert
            Assert.Equal("zh-CN", Assert.Single(applied).Name);
            Assert.Equal(AppLanguage.ChineseSimplified, StartupCulture.ReadLanguage(_configPath));
            Assert.Contains("Servy.Manager.exe.config", result.SyncError);
        }

        [Fact]
        public void Apply_OptionEqualsSettings_DoesNotRewriteTheFile()
        {
            // Arrange
            var content = Config("Language", "en");
            File.WriteAllText(_configPath, content);
            File.WriteAllText(Path.Combine(_directory, "Servy.Manager.exe.config"), content);

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
            var content = Config("Language", "zh-CN");
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
            // Arrange: a file that is not a configuration file cannot be written to
            File.WriteAllText(_configPath, "<configuration><appSettings><add key=");
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
            var ex = Assert.Throws<ArgumentNullException>(() => StartupCulture.Apply(_configPath, null, Culture("en-US"), null));

            // Assert
            Assert.Equal("applyCulture", ex.ParamName);
        }

        [Fact]
        public void Apply_OptionWithoutAValue_IsReportedAsAnEmptyRejectedValueAndLeavesTheSettingsAlone()
        {
            // Arrange
            var content = Config("Language", "zh-CN");
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
            var ex = Assert.Throws<ArgumentNullException>(() => StartupCulture.ApplyUiCulture(null));

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
