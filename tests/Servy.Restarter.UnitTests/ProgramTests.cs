using Servy.Core.Logging;
using Servy.Infrastructure.Data;
using System;
using System.Configuration;
using System.Data.SQLite;
using System.IO;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    [CollectionDefinition("RestarterProgramTests", DisableParallelization = true)]
    public class RestarterProgramTestsCollection
    {
        // Enforces strict sequential isolation across the execution suite
    }

    [Collection("RestarterProgramTests")]
    public class ProgramTests : IDisposable
    {
        // CONSTANT STRINGS HOISTING: Centralize artifact filenames to prevent cleanup drift
        private const string LogFileName = "Servy.Restarter.log";

        private readonly string _expectedLogFilePath;
        private readonly SQLiteConnection _dbKeepAliveConnection;
        private readonly string _defaultConnection;
        private readonly string _restartTimeoutSeconds;
        private readonly string _aesKeyFilePath;
        private readonly string _aesIvFilePath;

        public ProgramTests()
        {
            // Reset global exit code before each test execution block
            Environment.ExitCode = 0;

            _expectedLogFilePath = Path.Combine(Logger.LogsPath, LogFileName);

            // Capture the baseline configuration states to allow perfect recovery state rollback during Dispose
            _defaultConnection = ConfigurationManager.AppSettings["DefaultConnection"];
            _restartTimeoutSeconds = ConfigurationManager.AppSettings["RestartTimeoutSeconds"];
            _aesKeyFilePath = ConfigurationManager.AppSettings["Security:AESKeyFilePat"];
            _aesIvFilePath = ConfigurationManager.AppSettings["Security:AESIVFilePath"];

            // Open the persistent handle to anchor the shared memory segment lifecycle
            _dbKeepAliveConnection = new SQLiteConnection(_defaultConnection);
            _dbKeepAliveConnection.Open();

            // Bootstrap the schema table directly into the shared memory segment
            SQLiteDbInitializer.Initialize(_dbKeepAliveConnection);
        }

        #region Guard Conditions Branch Coverage

        [Fact]
        public void Main_MissingArguments_SetsExitCodeTo1AndExitsEarly()
        {
            // Arrange
            string[] args = new string[0]; // Triggers: if (args.Length == 0)

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Missing required argument: service name.");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Main_EmptyOrWhitespaceServiceName_SetsExitCodeTo1AndExitsEarly(string invalidName)
        {
            // Arrange
            string[] args = new string[] { invalidName }; // Triggers: if (string.IsNullOrWhiteSpace(serviceName))

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Service name cannot be empty.");
        }

        #endregion

        #region Operational Pipeline & Validation Branches

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We pass an unmanaged service identifier string. Since the memory database is fresh and empty,
            // serviceRepository.GetByName(...) will return null, exercising the managed validation check.
            string serviceName = "UnmanagedNet48Service";
            string[] args = new string[] { serviceName };

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public void Main_FallbackConfigurationParsing_HandlesInvalidTimeoutGracefully()
        {
            // Arrange
            // Inject an unparseable non-integer token token directly into the runtime configuration matrix
            ConfigurationManager.AppSettings["RestartTimeoutSeconds"] = "NotAnInteger";

            string serviceName = "UnmanagedNet48Service";
            string[] args = new string[] { serviceName };

            // Act
            Program.Main(args);

            // Assert
            // Program continues validation utilizing fallback default configuration bounds instead of throwing,
            // passing the configuration validation seam but breaking on unmanaged database constraints.
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        #endregion

        #region Verification Helpers

        /// <summary>
        /// Scans the physical log output stream for the expected diagnostic signatures to discriminate between crash paths.
        /// </summary>
        private void AssertLogContainsMessage(string expectedMessage)
        {
            // Force the static logger to flush its handle completely to disk
            Logger.Shutdown();

            Assert.True(File.Exists(_expectedLogFilePath), "The diagnostic restarter log file was never initialized on disk.");

            string logContent = File.ReadAllText(_expectedLogFilePath);
            Assert.Contains(expectedMessage, logContent);
        }

        #endregion

        public void Dispose()
        {
            // Force logger teardown first to unlock active files
            Logger.Shutdown();

            // Explicitly unlock and drop the keep-alive memory connection reference
            _dbKeepAliveConnection?.Dispose();

            // Rollback AppSettings matrix states to maintain complete isolation integrity across sibling execution tracks
            ConfigurationManager.AppSettings["DefaultConnection"] = _defaultConnection;
            ConfigurationManager.AppSettings["RestartTimeoutSeconds"] = _restartTimeoutSeconds;

            // Clean up temporary local workspace state file markers if generated
            try
            {
                if (File.Exists(_expectedLogFilePath)) File.Delete(_expectedLogFilePath);
                if (File.Exists(_aesKeyFilePath)) File.Delete(_aesKeyFilePath);
                if (File.Exists(_aesIvFilePath)) File.Delete(_aesIvFilePath);
            }
            catch
            {
                // Suppress lock warnings on ephemeral files cleanup
            }
        }
    }
}