using Moq;
using Newtonsoft.Json.Linq;
using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
using System.IO;
using Xunit;

namespace Servy.UI.UnitTests.Localization
{
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

        [Fact]
        public async Task ChangeAsync_DifferentLanguage_StoresItAndSaysItAppliesAtTheNextStart()
        {
            // Arrange
            File.WriteAllText(_configPath, "{\"LogLevel\": \"INFO\"}");
            var expectedText = string.Format(Strings.Msg_LanguageRestartRequired, Strings.Language_ChineseSimplified, Caption);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.ChineseSimplified, _messageBox.Object, Caption);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, selected);
            var json = JObject.Parse(File.ReadAllText(_configPath));
            Assert.Equal("zh-CN", json.Value<string>("Language"));
            Assert.Equal("INFO", json.Value<string>("LogLevel"));
            Assert.Contains(Strings.Language_ChineseSimplified, expectedText);
            Assert.Contains(Caption, expectedText);
            _messageBox.Verify(m => m.ShowInfoAsync(expectedText, Caption), Times.Once);
            _messageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangeAsync_SameLanguage_WritesNothingAndShowsNothing()
        {
            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.English, AppLanguage.English, _messageBox.Object, Caption);

            // Assert
            Assert.Equal(AppLanguage.English, selected);
            Assert.False(File.Exists(_configPath));
            _messageBox.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("zh-CN")]
        [InlineData(2)]
        public async Task ChangeAsync_ParameterIsNotALanguage_KeepsTheCurrentLanguage(object? parameter)
        {
            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.English, parameter, _messageBox.Object, Caption);

            // Assert
            Assert.Equal(AppLanguage.English, selected);
            Assert.False(File.Exists(_configPath));
            _messageBox.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ChangeAsync_SettingsFileCannotBeWritten_ShowsTheReasonAndKeepsTheCurrentLanguage()
        {
            // Arrange: a settings file that is not valid JSON is refused, not overwritten
            const string content = "{ this is not json";
            File.WriteAllText(_configPath, content);
            string? shown = null;
            _messageBox.Setup(m => m.ShowErrorAsync(It.IsAny<string>(), Caption))
                .Callback<string, string>((message, _) => shown = message)
                .Returns(Task.CompletedTask);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption);

            // Assert
            Assert.Equal(AppLanguage.System, selected);
            Assert.Equal(content, File.ReadAllText(_configPath));
            Assert.NotNull(shown);
            var prefix = Strings.Msg_LanguageSaveFailed.Substring(0, Strings.Msg_LanguageSaveFailed.IndexOf("{0}", StringComparison.Ordinal));
            Assert.StartsWith(prefix, shown);
            Assert.True(shown.Length > prefix.Length, "The failure reason is missing from the message.");
            _messageBox.Verify(m => m.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangeAsync_NullMessageBoxService_ThrowsArgumentNullException()
        {
            // Act
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                () => LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, null!, Caption));

            // Assert
            Assert.Equal("messageBoxService", ex.ParamName);
        }
    }
}
