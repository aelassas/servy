using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Testing;
using System;
using System.IO;
using Xunit;

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

        // The suffix the migrator renames the former log with; private there, so the tests that exercise the
        // resumed-migration path fail loudly if it ever changes.
        private string StagingPath => LegacyPath + ".migrating";

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
        public void Migrate_FormerLogHeldByAWriterWithoutDeleteSharing_CopiesNothingAndTheNextStartMovesItOnce()
        {
            // Arrange
            // The other handle grants readers and writers but not deleters, which is the access the rename needs:
            // the rename fails before anything is read, so the content is still only in the former log.
            File.WriteAllText(LegacyPath, "once");

            // Act
            bool first;
            string log;
            using (new FileStream(LegacyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                (first, log) = LogCapture.Run(() => ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName));
            }

            var copiedWhileHeld = File.Exists(TargetPath);
            var second = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.False(first);
            Assert.False(copiedWhileHeld);
            Assert.True(second);
            Assert.Equal("once", File.ReadAllText(TargetPath));
            Assert.False(File.Exists(LegacyPath));
            Assert.False(File.Exists(StagingPath));
            Assert.Contains("Could not migrate the former service log", log);
        }

        [Fact]
        public void Migrate_FormerLogHeldByAReaderWithoutDeleteSharing_CopiesNothingAndTheNextStartMovesItOnce()
        {
            // Arrange
            // The other handle shares reads only, so it denies the DELETE the rename needs and the write access an
            // in-place emptying would need. The rename is the first step, so nothing reaches the new log.
            File.WriteAllText(LegacyPath, "once");

            // Act
            bool first;
            string log;
            using (new FileStream(LegacyPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                (first, log) = LogCapture.Run(() => ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName));
            }

            var copiedWhileHeld = File.Exists(TargetPath);
            var second = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.False(first);
            Assert.False(copiedWhileHeld);
            Assert.True(second);
            Assert.Equal("once", File.ReadAllText(TargetPath));
            Assert.False(File.Exists(LegacyPath));
            Assert.False(File.Exists(StagingPath));
            Assert.Contains("Could not migrate the former service log", log);
        }

        [Fact]
        public void Migrate_StagedLogLeftByAnInterruptedMigration_IsMovedAndDeleted()
        {
            // Arrange
            File.WriteAllText(StagingPath, "interrupted\r\n");

            // Act
            var done = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.True(done);
            Assert.Equal("interrupted\r\n", File.ReadAllText(TargetPath));
            Assert.False(File.Exists(StagingPath));
        }

        [Fact]
        public void Migrate_StagedLogAndAFormerLogWrittenSince_MovesTheStagedOneFirst()
        {
            // Arrange
            File.WriteAllText(StagingPath, "older\r\n");
            File.WriteAllText(LegacyPath, "newer\r\n");

            // Act
            var done = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.True(done);
            Assert.Equal("older\r\nnewer\r\n", File.ReadAllText(TargetPath));
            Assert.False(File.Exists(LegacyPath));
            Assert.False(File.Exists(StagingPath));
        }

        [Fact]
        public void Migrate_EmptyStagedLog_IsDeletedWithoutCreatingAnything()
        {
            // Arrange
            File.WriteAllText(StagingPath, string.Empty);

            // Act
            var done = ServiceLogMigrator.Migrate(LegacyFolder, ServiceFolder, FileName);

            // Assert
            Assert.True(done);
            Assert.False(File.Exists(StagingPath));
            Assert.False(File.Exists(TargetPath));
        }

        [Theory]
        [InlineData(null, "b", "c")]
        [InlineData("a", " ", "c")]
        [InlineData("a", "b", "")]
        public void Migrate_BlankArgument_Throws(string legacy, string service, string file)
        {
            Assert.Throws<ArgumentException>(() => ServiceLogMigrator.Migrate(legacy, service, file));
        }

        [Fact]
        public void DefaultPaths_AreTheVaultLogsAndItsServiceFolder()
        {
            Assert.Equal(Path.Combine(AppConfig.LogsFolderPath, "service"), AppConfig.ServiceLogsFolderPath);
            Assert.Equal("Servy.Service.log", AppConfig.ServyServiceLogFileName);
        }
    }
}
