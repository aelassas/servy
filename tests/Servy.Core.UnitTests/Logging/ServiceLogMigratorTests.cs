using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Testing;

namespace Servy.Core.UnitTests.Logging
{
    /// <summary>
    /// Unit tests for <see cref="ServiceLogMigrator"/>, which moves the wrappers' log from <c>logs\</c> to
    /// <c>logs\service\</c>.
    /// </summary>
    [Collection(LoggerCollection.Name)]
    public class ServiceLogMigratorTests : TempDirectoryTestBase
    {
        private const string FileName = "Servy.Service.log";

        private string LegacyFolder => Path.Combine(TempDirectory, "logs");

        private string ServiceFolder => Path.Combine(TempDirectory, "logs", "service");

        private string LegacyPath => Path.Combine(LegacyFolder, FileName);

        private string TargetPath => Path.Combine(ServiceFolder, FileName);

        public ServiceLogMigratorTests()
        {
            Directory.CreateDirectory(LegacyFolder);
        }

        [Fact]
        public void Migrate_NoLegacyLog_DoesNothing()
        {
            // Act
            var done = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.True(done);
            Assert.False(Directory.Exists(ServiceFolder));
        }

        [Fact]
        public void Migrate_NonEmptyLegacyLog_MovesItsContentToTheServiceFolderAndDeletesIt()
        {
            // Arrange
            File.WriteAllText(LegacyPath, "line 1\r\nline 2\r\n");

            // Act
            var done = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.True(done);
            Assert.Equal("line 1\r\nline 2\r\n", File.ReadAllText(TargetPath));
            Assert.False(File.Exists(LegacyPath));
        }

        [Fact]
        public void Migrate_ServiceLogAlreadyExists_AppendsTheLegacyContent()
        {
            // Arrange
            Directory.CreateDirectory(ServiceFolder);
            File.WriteAllText(TargetPath, "new\r\n");
            File.WriteAllText(LegacyPath, "old\r\n");

            // Act
            var done = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert: nothing written to the new log is lost
            Assert.True(done);
            Assert.Equal("new\r\nold\r\n", File.ReadAllText(TargetPath));
            Assert.False(File.Exists(LegacyPath));
        }

        [Fact]
        public void Migrate_EmptyLegacyLog_IsDeletedWithoutCreatingAnything()
        {
            // Arrange
            File.WriteAllText(LegacyPath, string.Empty);

            // Act
            var done = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.True(done);
            Assert.False(File.Exists(LegacyPath));
            Assert.False(File.Exists(TargetPath));
        }

        [Fact]
        public void Migrate_LegacyLogLocked_KeepsItAndReportsFalse()
        {
            // Arrange
            File.WriteAllText(LegacyPath, "held");

            // Act
            bool done;
            string log;
            using (new FileStream(LegacyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                (done, log) = LogCapture.Run(() => ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName));
            }

            // Assert
            Assert.False(done);
            Assert.True(File.Exists(LegacyPath));
            Assert.Equal("held", File.ReadAllText(LegacyPath));
            Assert.Contains("Could not migrate the former service log", log);
        }

        [Fact]
        public void Migrate_TwiceInARow_IsIdempotent()
        {
            // Arrange
            File.WriteAllText(LegacyPath, "once");

            // Act
            ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);
            var second = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.True(second);
            Assert.Equal("once", File.ReadAllText(TargetPath));
        }

        [Fact]
        public void Migrate_DeleteFailsAfterTheContentWasCopied_EmptiesTheFormerLogSoTheNextStartDoesNotAppendItAgain()
        {
            // Arrange
            // The other handle grants readers and writers but not deleters, which is the access File.Delete needs:
            // the copy succeeds, the delete does not, and the content is in the target with the former log still there.
            File.WriteAllText(LegacyPath, "once");

            // Act
            bool first;
            string log;
            using (new FileStream(LegacyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                (first, log) = LogCapture.Run(() => ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName));
            }

            var second = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.False(first);
            Assert.True(second);
            Assert.Equal("once", File.ReadAllText(TargetPath));
            Assert.False(File.Exists(LegacyPath));
            Assert.Contains("Could not migrate the former service log", log);
        }

        [Theory]
        [InlineData(null, "b", "c")]
        [InlineData("a", " ", "c")]
        [InlineData("a", "b", "")]
        public void Migrate_BlankArgument_Throws(string? legacy, string? service, string? file)
        {
            Assert.Throws<ArgumentException>(() => ServiceLogMigrator.Migrate(legacy!, service!, file!));
        }

        [Fact]
        public void DefaultPaths_AreTheVaultLogsAndItsServiceFolder()
        {
            Assert.Equal(Path.Combine(AppConfig.LogsFolderPath, "service"), AppConfig.ServiceLogsFolderPath);
            Assert.Equal("Servy.Service.log", AppConfig.ServyServiceLogFileName);
        }
    }
}
