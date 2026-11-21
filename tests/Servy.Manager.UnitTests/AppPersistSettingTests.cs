using Newtonsoft.Json.Linq;
using Servy.Testing;
using System.Text;

namespace Servy.Manager.UnitTests
{
    /// <summary>
    /// Tests for <see cref="App.PersistSetting(string, string, string)"/>, the path-taking overload that writes a
    /// setting into the Manager's JSON configuration file through <c>Helper.WriteFileAtomic</c>, for its batch
    /// counterpart <see cref="App.PersistSettings(string, IReadOnlyList{KeyValuePair{string, string}})"/>, and for
    /// <see cref="App.GetLayoutChanges"/>, which picks the window-layout settings <see cref="App.SaveWindowLayout"/> writes in that batch.
    /// </summary>
    [Collection(AmbientTestCollection.Name)]
    public class AppPersistSettingTests : IDisposable
    {
        /// <summary>
        /// A private directory per test, so the staging-file check sees only what this test wrote.
        /// </summary>
        private readonly string _directory;

        /// <summary>
        /// Creates the private directory used by the test.
        /// </summary>
        public AppPersistSettingTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ServyPersistSetting_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        /// <summary>
        /// Deletes the private directory and everything in it.
        /// </summary>
        public void Dispose()
        {
            try { Directory.Delete(_directory, true); } catch { /* best effort */ }
        }

        [Fact]
        public void PersistSetting_ExistingFile_UpdatesKeyKeepsOtherKeysAndLeavesNoStagingFile()
        {
            // Arrange
            var configPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(configPath, "{\"RefreshIntervalInSeconds\": 7, \"ServicesHiddenColumns\": \"Old\"}");

            // Act
            App.PersistSetting(configPath, "ServicesHiddenColumns", "Pid,Ram");

            // Assert
            var json = JObject.Parse(File.ReadAllText(configPath));
            Assert.Equal("Pid,Ram", json.Value<string>("ServicesHiddenColumns"));
            Assert.Equal(7, json.Value<int>("RefreshIntervalInSeconds"));
            Assert.Equal(new[] { configPath }, Directory.GetFiles(_directory));
        }

        [Fact]
        public void PersistSetting_MissingFile_CreatesUtf8FileWithoutBomAndLeavesNoStagingFile()
        {
            // Arrange
            var configPath = Path.Combine(_directory, "appsettings.manager.json");

            // Act
            App.PersistSetting(configPath, "IsDisplayNameColumnMigrated", "true");

            // Assert
            var bytes = File.ReadAllBytes(configPath);
            Assert.NotEmpty(bytes);
            Assert.NotEqual(0xEF, bytes[0]);
            var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
            Assert.Equal("true", json.Value<string>("IsDisplayNameColumnMigrated"));
            Assert.Single(json.Properties());
            Assert.Equal(new[] { configPath }, Directory.GetFiles(_directory));
        }

        [Fact]
        public void PersistSetting_ExistingFileIsNotValidJson_LogsAWarningDoesNotThrowAndLeavesTheFileAsItIs()
        {
            // Arrange
            var configPath = Path.Combine(_directory, "appsettings.manager.json");
            const string torn = "{\"RefreshIntervalInSeconds\": 7, \"ServicesHiddenCol";
            File.WriteAllText(configPath, torn);

            // Act
            var log = LogCapture.Run(() => App.PersistSetting(configPath, "ServicesHiddenColumns", "Pid,Ram"));

            // Assert
            Assert.Contains("Failed to save ServicesHiddenColumns setting", log);
            Assert.Equal(torn, File.ReadAllText(configPath));
            Assert.Equal(new[] { configPath }, Directory.GetFiles(_directory));
        }

        [Fact]
        public void PersistSettings_SeveralKeys_WritesThemAllKeepsOtherKeysAndLeavesNoStagingFile()
        {
            // Arrange
            var configPath = Path.Combine(_directory, "appsettings.manager.json");
            File.WriteAllText(configPath, "{\"RefreshIntervalInSeconds\": 7, \"ServicesColumnWidths\": \"Name=100\"}");
            var settings = new[]
            {
                new KeyValuePair<string, string>("MainWindowPlacement", "10,20,1200,700,Normal"),
                new KeyValuePair<string, string>("ServicesColumnWidths", "Name=180,Status=90"),
            };

            // Act
            var log = LogCapture.Run(() => App.PersistSettings(configPath, settings));

            // Assert
            var json = JObject.Parse(File.ReadAllText(configPath));
            Assert.Equal("10,20,1200,700,Normal", json.Value<string>("MainWindowPlacement"));
            Assert.Equal("Name=180,Status=90", json.Value<string>("ServicesColumnWidths"));
            Assert.Equal(7, json.Value<int>("RefreshIntervalInSeconds"));
            Assert.Equal(3, json.Properties().Count());
            Assert.Contains("Saved MainWindowPlacement setting", log);
            Assert.Contains("Saved ServicesColumnWidths setting", log);
            Assert.Equal(new[] { configPath }, Directory.GetFiles(_directory));
        }

        [Fact]
        public void PersistSettings_NoKeys_DoesNotCreateOrTouchTheFile()
        {
            // Arrange
            var missingPath = Path.Combine(_directory, "missing.json");
            var existingPath = Path.Combine(_directory, "appsettings.manager.json");
            const string content = "{ \"RefreshIntervalInSeconds\":   7 }";
            File.WriteAllText(existingPath, content);

            // Act
            App.PersistSettings(missingPath, Array.Empty<KeyValuePair<string, string>>());
            App.PersistSettings(existingPath, Array.Empty<KeyValuePair<string, string>>());

            // Assert: the existing file keeps its exact text, so it was not rewritten
            Assert.False(File.Exists(missingPath));
            Assert.Equal(content, File.ReadAllText(existingPath));
            Assert.Equal(new[] { existingPath }, Directory.GetFiles(_directory));
        }

        [Fact]
        public void PersistSettings_ExistingFileIsNotValidJson_LogsAWarningPerKeyAndLeavesTheFileAsItIs()
        {
            // Arrange
            var configPath = Path.Combine(_directory, "appsettings.manager.json");
            const string torn = "{\"RefreshIntervalInSeconds\": 7, \"ServicesHiddenCol";
            File.WriteAllText(configPath, torn);
            var settings = new[]
            {
                new KeyValuePair<string, string>("MainWindowPlacement", "10,20,1200,700,Normal"),
                new KeyValuePair<string, string>("LogsColumnWidths", "Time=200"),
            };

            // Act
            var log = LogCapture.Run(() => App.PersistSettings(configPath, settings));

            // Assert
            Assert.Contains("Failed to save MainWindowPlacement setting", log);
            Assert.Contains("Failed to save LogsColumnWidths setting", log);
            Assert.Equal(torn, File.ReadAllText(configPath));
            Assert.Equal(new[] { configPath }, Directory.GetFiles(_directory));
        }

        [Fact]
        public void GetLayoutChanges_EveryValueChanged_ReturnsTheThreeSettingsUnderTheirConfigurationKeys()
        {
            // Act
            var changes = App.GetLayoutChanges(
                string.Empty, "10,20,1200,700,Normal",
                "Name=100", "Name=180",
                string.Empty, "Time=200");

            // Assert
            Assert.Equal(
                new[]
                {
                    new KeyValuePair<string, string>("MainWindowPlacement", "10,20,1200,700,Normal"),
                    new KeyValuePair<string, string>("ServicesColumnWidths", "Name=180"),
                    new KeyValuePair<string, string>("LogsColumnWidths", "Time=200"),
                },
                changes);
        }

        [Fact]
        public void GetLayoutChanges_NullOrUnchangedValue_IsLeftOut()
        {
            // Act: the placement is unchanged, the services widths are not supplied, the logs widths are cleared
            var changes = App.GetLayoutChanges(
                "10,20,1200,700,Normal", "10,20,1200,700,Normal",
                "Name=100", null,
                "Time=200", string.Empty);

            // Assert
            Assert.Equal(new[] { new KeyValuePair<string, string>("LogsColumnWidths", string.Empty) }, changes);
        }

        [Fact]
        public void GetLayoutChanges_NothingChanged_ReturnsNoSettings()
        {
            // Act
            var changes = App.GetLayoutChanges(
                "10,20,1200,700,Normal", "10,20,1200,700,Normal",
                "Name=100", "Name=100",
                string.Empty, null);

            // Assert
            Assert.Empty(changes);
        }
    }
}
