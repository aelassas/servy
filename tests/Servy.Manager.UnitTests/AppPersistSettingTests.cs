using Newtonsoft.Json.Linq;
using System.Text;

namespace Servy.Manager.UnitTests
{
    /// <summary>
    /// Tests for <see cref="App.PersistSetting(string, string, string)"/>, the path-taking overload that writes a
    /// setting into the Manager's JSON configuration file through <c>Helper.WriteFileAtomic</c>.
    /// </summary>
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
    }
}
