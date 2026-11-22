using Moq;
using Servy.Testing;
using Servy.UI.Localization;
using Servy.UI.Resources;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
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

        private int _restarts;

        private static Dictionary<string, string> ReadSettings(string path)
        {
            return XDocument.Load(path).Root.Element("appSettings").Elements("add")
                .ToDictionary(e => e.Attribute("key").Value, e => e.Attribute("value").Value);
        }

        private void Restart()
        {
            _restarts++;
        }

        [Fact]
        public async Task ChangeAsync_UserConfirms_StoresTheLanguageForBothApplicationsAndRestarts()
        {
            // Arrange
            File.WriteAllText(_configPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <appSettings>\r\n    <add key=\"LogLevel\" value=\"INFO\" />\r\n  </appSettings>\r\n</configuration>\r\n");
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            File.WriteAllText(managerPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <appSettings>\r\n    <add key=\"RefreshIntervalInSeconds\" value=\"7\" />\r\n  </appSettings>\r\n</configuration>\r\n");
            var expectedText = string.Format(Strings.Msg_LanguageRestartConfirm, Strings.Language_ChineseSimplified, Caption);
            _messageBox.Setup(m => m.ShowConfirmAsync(expectedText, Caption)).ReturnsAsync(true);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.ChineseSimplified, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.ChineseSimplified, selected);
            var desktop = ReadSettings(_configPath);
            Assert.Equal("zh-CN", desktop["Language"]);
            Assert.Equal("INFO", desktop["LogLevel"]);
            var manager = ReadSettings(managerPath);
            Assert.Equal("zh-CN", manager["Language"]);
            Assert.Equal("7", manager["RefreshIntervalInSeconds"]);
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
            const string content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <appSettings>\r\n    <add key=\"Language\" value=\"en\" />\r\n  </appSettings>\r\n</configuration>\r\n";
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
        public async Task ChangeAsync_ParameterIsNotALanguage_KeepsTheCurrentLanguage(object parameter)
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
            // Arrange: a file that is not a valid configuration file is refused, not overwritten
            const string content = "<configuration><appSettings><add key=";
            File.WriteAllText(_configPath, content);
            string shown = null;
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
            // Arrange: the Manager configuration file is not valid, the Desktop one is fine
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            File.WriteAllText(managerPath, "<configuration><appSettings><add key=");
            _messageBox.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption)).ReturnsAsync(true);

            // Act
            var selected = await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption, Restart);

            // Assert
            Assert.Equal(AppLanguage.English, selected);
            Assert.Equal(AppLanguage.English, StartupCulture.ReadLanguage(_configPath));
            Assert.Equal("<configuration><appSettings><add key=", File.ReadAllText(managerPath));
            Assert.Equal(1, _restarts);
            _messageBox.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangeAsync_UserConfirms_RestartsOnlyAfterBothFilesHoldTheLanguage()
        {
            // Arrange
            var managerPath = Path.Combine(_directory, "Servy.Manager.exe.config");
            const string english = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <appSettings>\r\n    <add key=\"Language\" value=\"en\" />\r\n  </appSettings>\r\n</configuration>\r\n";
            File.WriteAllText(_configPath, english);
            File.WriteAllText(managerPath, english);
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
            // Arrange: the Manager configuration file is not valid, the Desktop one is fine
            File.WriteAllText(Path.Combine(_directory, "Servy.Manager.exe.config"), "<configuration><appSettings><add key=");
            _messageBox.Setup(m => m.ShowConfirmAsync(It.IsAny<string>(), Caption)).ReturnsAsync(true);

            // Act
            var log = await LogCapture.RunAsync(async () =>
            {
                await LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption, Restart);
            });

            // Assert
            Assert.Contains("Failed to save the Language setting to Servy.Manager.exe.config: ", log);
            Assert.Equal(1, _restarts);
        }

        [Fact]
        public async Task ChangeAsync_NullMessageBoxServiceOrRestart_ThrowsArgumentNullException()
        {
            // Act
            var noMessageBox = await Assert.ThrowsAsync<ArgumentNullException>(
                () => LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, null, Caption, Restart));
            var noRestart = await Assert.ThrowsAsync<ArgumentNullException>(
                () => LanguageSwitcher.ChangeAsync(_configPath, AppLanguage.System, AppLanguage.English, _messageBox.Object, Caption, null));

            // Assert
            Assert.Equal("messageBoxService", noMessageBox.ParamName);
            Assert.Equal("restart", noRestart.ParamName);
        }
    }
}
