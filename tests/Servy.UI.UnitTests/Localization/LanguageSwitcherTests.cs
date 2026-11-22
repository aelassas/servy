using Moq;
using Newtonsoft.Json.Linq;
using Servy.Testing;
using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
using System.IO;
using Xunit;

namespace Servy.UI.UnitTests.Localization
{
    // LogCapture re-initializes the process-wide static Logger, so every class that uses it runs in one collection
    [Collection("StaticLogger")]
    public class LanguageSwitcherTests : IDisposable
    {
        private const string Caption = "Servy";

        private readonly string _directory;
        private readonly string _configPath;
        private readonly Mock<IMessageBoxService> _messageBox;

        public LanguageSwitcherTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ServyLanguageSwitcherTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _configPath = Path.Combine(_directory, "appsettings.desktop.json");
            _messageBox = new Mock<IMessageBoxService>();
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

        private int _restarts;

        private void Restart()
        {
            _restarts++;
        }

        [Fact]
        public async Task ChangeAsync_UserConfirms_StoresTheLanguageForBothApplicationsAndRestarts()
        {
            // Arrange
            File.WriteAllText(_configPath, "{\"LogLevel\": \"INFO\"}");
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(managerPath, "{\"RefreshIntervalInSeconds\": 7}");
            var expectedText = string.Format(Strings.Msg_LanguageRestartConfirm, Strings.Language_ChineseSimplified, Caption);
            _messageBox.Setup(m => m.ShowConfirmAsync(expectedText, Caption)).ReturnsAsync(true);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.ChineseSimplified, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, selected);
            var desktop = JObject.Parse(File.ReadAllText(_configPath));
            Assert.Equal("zh-CN", desktop.Value<string>("Language"));
            Assert.Equal("INFO", desktop.Value<string>("LogLevel"));
            var manager = JObject.Parse(File.ReadAllText(managerPath));
            Assert.Equal("zh-CN", manager.Value<string>("Language"));
            Assert.Equal(7, manager.Value<int>("RefreshIntervalInSeconds"));
            Assert.Contains(Strings.Language_ChineseSimplified, expectedText);
            Assert.Contains(Caption, expectedText);
            Assert.Equal(1, _restarts);
            _messageBox.Verify(m => m.ShowConfirmAsync(expectedText, Caption), Times.Once);
            _messageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangeAsync_UserDeclines_StoresNothingAndDoesNotRestart()
        {
            // Arrange
            const string content = "{\"Language\": \"en\"}";
            File.WriteAllText(_configPath, content);
            _messageBox.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption)).ReturnsAsync(false);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.English, AppLanguage.ChineseSimplified, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.English, selected);
            Assert.Equal(content, File.ReadAllText(_configPath));
            Assert.Equal(new[] { _configPath }, Directory.GetFiles(_directory));
            Assert.Equal(0, _restarts);
            _messageBox.Verify(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption), Times.Once);
        }

        [Fact]
        public async Task ChangeAsync_SameLanguage_AsksNothingWritesNothingAndDoesNotRestart()
        {
            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.English, AppLanguage.English, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.English, selected);
            Assert.False(File.Exists(_configPath));
            Assert.Equal(0, _restarts);
            _messageBox.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("zh-CN")]
        [InlineData(2)]
        public async Task ChangeAsync_ParameterIsNotALanguage_KeepsTheCurrentLanguage(object? parameter)
        {
            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.English, parameter, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.English, selected);
            Assert.False(File.Exists(_configPath));
            Assert.Equal(0, _restarts);
            _messageBox.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ChangeAsync_SettingsFileCannotBeWritten_ShowsTheReasonKeepsTheCurrentLanguageAndDoesNotRestart()
        {
            // Arrange: a settings file that is not valid JSON is refused, not overwritten
            const string content = "{ this is not json";
            File.WriteAllText(_configPath, content);
            string? shown = null;
            _messageBox.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption)).ReturnsAsync(true);
            _messageBox.Setup(m => m.ShowErrorAsync(It.IsAny<string>(), Caption))
                .Callback<string, string>((message, _) => shown = message)
                .Returns(Task.CompletedTask);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.System, selected);
            Assert.Equal(content, File.ReadAllText(_configPath));
            Assert.NotNull(shown);
            var prefix = Strings.Msg_LanguageSaveFailed.Substring(0, Strings.Msg_LanguageSaveFailed.IndexOf("{0}", StringComparison.Ordinal));
            Assert.StartsWith(prefix, shown);
            Assert.True(shown.Length > prefix.Length, "The failure reason is missing from the message.");
            Assert.Equal(0, _restarts);
        }

        [Fact]
        public async Task ChangeAsync_OtherApplicationsFileCannotBeWritten_StillStoresTheOwnLanguageAndRestarts()
        {
            // Arrange: the Manager settings file is not valid JSON, the Desktop one is fine
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(managerPath, "{ this is not json");
            _messageBox.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption)).ReturnsAsync(true);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.English, selected);
            Assert.Equal(AppLanguage.English, StartupCulture.ReadLanguage(_configPath));
            Assert.Equal("{ this is not json", File.ReadAllText(managerPath));
            Assert.Equal(1, _restarts);
            _messageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangeAsync_UserConfirms_RestartsOnlyAfterBothFilesHoldTheLanguage()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(_configPath, "{\"Language\": \"en\"}");
            File.WriteAllText(managerPath, "{\"Language\": \"en\"}");
            _messageBox.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption)).ReturnsAsync(true);
            var storedAtRestart = new List<AppLanguage>();

            // Act
            await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.English, AppLanguage.ChineseSimplified, _messageBox.Object, Caption, () =>
            {
                storedAtRestart.Add(StartupCulture.ReadLanguage(_configPath));
                storedAtRestart.Add(StartupCulture.ReadLanguage(managerPath));
            });

            // Assert
            Assert.Equal(new[] { AppLanguage.ChineseSimplified, AppLanguage.ChineseSimplified }, storedAtRestart);
        }

        [Fact]
        public async Task ChangeAsync_OtherApplicationsFileCannotBeWritten_LogsTheFileThatWasNotUpdated()
        {
            // Arrange: the Manager settings file is not valid JSON, the Desktop one is fine
            File.WriteAllText(Path.Combine(_directory, "appsettings.manager.json"), "{ this is not json");
            _messageBox.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption)).ReturnsAsync(true);

            // Act
            var log = await LogCapture.RunAsync(async () =>
            {
                await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption, Restart);
            });

            // Assert
            Assert.Contains("Failed to save the Language setting to appsettings.manager.json: ", log);
            Assert.Equal(1, _restarts);
        }

        [Fact]
        public async Task ChangeAsync_NullMessageBoxServiceOrRestart_ThrowsArgumentNullException()
        {
            // Act
            var noMessageBox = await Assert.ThrowsAsync<ArgumentNullException>(
                () => LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, null!, Caption, Restart));
            var noRestart = await Assert.ThrowsAsync<ArgumentNullException>(
                () => LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption, null!));

            // Assert
            Assert.Equal("messageBoxService", noMessageBox.ParamName);
            Assert.Equal("restart", noRestart.ParamName);
        }
    }
}
