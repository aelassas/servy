using Moq;
using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
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
            _configPath = Path.Combine(_directory, "Servy.exe.config");
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
            File.WriteAllText(_configPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <appSettings>\r\n    <add key=\"LogLevel\" value=\"INFO\" />\r\n  </appSettings>\r\n</configuration>\r\n");
            var expectedText = string.Format(Strings.Msg_LanguageRestartRequired, Strings.Language_ChineseSimplified, Caption);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.ChineseSimplified, _messageBox.Object, Caption);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, selected);
            var settings = XDocument.Load(_configPath).Root.Element("appSettings").Elements("add")
                .ToDictionary(e => e.Attribute("key").Value, e => e.Attribute("value").Value);
            Assert.Equal("zh-CN", settings["Language"]);
            Assert.Equal("INFO", settings["LogLevel"]);
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
        public async Task ChangeAsync_ParameterIsNotALanguage_KeepsTheCurrentLanguage(object parameter)
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
            // Arrange: a file that is not a valid configuration file is refused, not overwritten
            const string content = "<configuration><appSettings><add key=";
            File.WriteAllText(_configPath, content);
            string shown = null;
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
                () => LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, null, Caption));

            // Assert
            Assert.Equal("messageBoxService", ex.ParamName);
        }
    }
}
