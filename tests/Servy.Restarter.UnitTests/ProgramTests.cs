using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System;
using System.Configuration;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    [Collection(ProgramTestsCollection.Name)]
    public class ProgramTests : TempDirectoryTestBase
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

            // Isolate logging writes directly into a dynamic, unique temporary folder per test run
            _expectedLogFilePath = Path.Combine(TempDirectory, LogFileName);

            // Pre-seed the static logger so empty/missing argument calls route to the isolated temp directory
            Logger.Initialize(LogFileName, logDirectory: TempDirectory);

            // Capture the baseline configuration states to allow perfect recovery state rollback during Dispose
            _defaultConnection = ConfigurationManager.AppSettings["DefaultConnection"];
            _restartTimeoutSeconds = ConfigurationManager.AppSettings["RestartTimeoutSeconds"];
            _aesKeyFilePath = ConfigurationManager.AppSettings["Security:AESKeyFilePath"];
            _aesIvFilePath = ConfigurationManager.AppSettings["Security:AESIVFilePath"];

            // Supply an absolute file-backed SQLite database path and key paths in TempDirectory to satisfy AppFoldersHelper
            string fileDbPath = Path.Combine(TempDirectory, "RestarterTestDbNet48.db");
            string fileConnString = $"Data Source={fileDbPath};Version=3;";

            ConfigurationManager.AppSettings["DefaultConnection"] = fileConnString;
            ConfigurationManager.AppSettings["Security:AESKeyFilePath"] = Path.Combine(TempDirectory, "test_restarter.key");
            ConfigurationManager.AppSettings["Security:AESIVFilePath"] = Path.Combine(TempDirectory, "test_restarter.iv");

            // Open the persistent handle and initialize database schema
            _dbKeepAliveConnection = new SQLiteConnection(fileConnString);
            _dbKeepAliveConnection.Open();
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

            // The ExitsEarly half: without the guard's return, args[0] throws on the empty array
            // and the catch-all logs the pre-scoped-logger failure instead.
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to initialize or execute.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        public void Main_EmptyOrWhitespaceServiceName_SetsExitCodeTo1AndExitsEarly(string invalidName)
        {
            // Arrange
            string[] args = new string[] { invalidName, TempDirectory }; // Triggers: if (string.IsNullOrWhiteSpace(serviceName))

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Service name cannot be empty.");

            // The ExitsEarly half: without the guard's return, the blank name flows on to the
            // repository lookup and the not-managed branch logs and sets ExitCode = 1 as well.
            AssertLogDoesNotContainMessage("is not managed by Servy.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
        }

        #endregion

        #region Event Log Fallback & Security Guard Coverage

        /*
         * Note on Helper.EnsureEventSourceExists Exception Fallback Branch:
         * The catch block around Helper.EnsureEventSourceExists() (Step 1) catches EventLog creation or
         * access failures and falls back to file-only logging (EventLogLogger(..., isEventLogEnabled: false)).
         * Because AppConfig.EventSource is a compile-time constant ("Servy") and Helper delegates directly to
         * static System.Diagnostics.EventLog calls, triggering this exception in an integration test requires
         * running in an environment without Windows Event Log registry access.
         *
         * Note on DatabaseValidator.IsSqliteVersionSafe False Path:
         * The false branch of DatabaseValidator.IsSqliteVersionSafe inside Program.Main triggers a fatal exit
         * when the loaded System.Data.SQLite library version is below AppConfig.MinRequiredSqliteVersion.
         * Forcing this condition at the Program.Main integration level requires substituting the loaded native/managed
         * SQLite provider assembly at runtime. The underlying version validation rules are fully covered in DatabaseValidatorTests.cs.
         */

        #endregion

        #region Operational Pipeline & Validation Branches

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We pass an unmanaged service identifier string. Since the database is fresh and empty,
            // serviceRepository.GetByName(...) will return null, exercising the managed validation check.
            string serviceName = "UnmanagedNet48Service";
            string[] args = new string[] { serviceName, TempDirectory };

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public async Task Main_FallbackConfigurationParsing_HandlesInvalidTimeoutGracefully()
        {
            // Arrange
            // Inject an unparseable non-integer token directly into the runtime configuration matrix
            ConfigurationManager.AppSettings["RestartTimeoutSeconds"] = "NotAnInteger";

            string connString = ConfigurationManager.AppSettings["DefaultConnection"];
            string keyPath = ConfigurationManager.AppSettings["Security:AESKeyFilePath"];
            string ivPath = ConfigurationManager.AppSettings["Security:AESIVFilePath"];

            string serviceName = "UnmanagedNet48Service";
            string[] args = new string[] { serviceName, TempDirectory };

            // Ensure application key files and folder environment exist for Program.Main
            AppFoldersHelper.EnsureFolders(connString, keyPath, ivPath);

            // Seed a valid managed service via ServiceRepository so GetByName() can deserialize it properly
            using (var dbContext = new AppDbContext(connString))
            using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
            using (var secureData = new SecureData(protectedKeyProvider))
            {
                var dapperExecutor = new DapperExecutor(dbContext);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();
                var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                var service = new ServiceDto
                {
                    Name = serviceName,
                    ExecutablePath = @"C:\MockPath\Service.exe"
                };
                await repository.AddAsync(service, CancellationToken.None);
            }

            try
            {
                // Act
                Program.Main(args);

                // Assert
                // The application successfully bypassed the corrupted token string and fell back
                // to standard timeout bounds. Because the service does not actually exist in the SCM,
                // it detects ServiceNotFound, logs a warning, and sets ExitCode = 1.
                Assert.Equal(1, Environment.ExitCode);
                AssertLogContainsMessage($"Service '{serviceName}' no longer exists in the SCM; nothing to restart.");
            }
            finally
            {
                // Clean up the seeded service entry from the shared database context to prevent
                // side-effects or collision state leaks on subsequent unit test runs.
                using (var dbContext = new AppDbContext(connString))
                using (var protectedKeyProvider = new ProtectedKeyProvider(keyPath, ivPath))
                using (var secureData = new SecureData(protectedKeyProvider))
                {
                    var dapperExecutor = new DapperExecutor(dbContext);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();
                    var repository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                    var existing = repository.GetByName(serviceName, decrypt: false);
                    if (existing != null && existing.Id.HasValue)
                    {
                        await repository.DeleteAsync(existing.Id.Value, CancellationToken.None);
                    }
                }
            }
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

            Assert.True(File.Exists(_expectedLogFilePath), $"The diagnostic restarter log file was never initialized on disk at '{_expectedLogFilePath}'.");

            string logContent = File.ReadAllText(_expectedLogFilePath);
            Assert.Contains(expectedMessage, logContent);
        }

        /// <summary>
        /// Asserts the physical log output stream carries no signature from a later pipeline stage,
        /// which is what pins that a guard returned instead of logging and falling through.
        /// </summary>
        private void AssertLogDoesNotContainMessage(string unexpectedMessage)
        {
            // Force the static logger to flush its handle completely to disk
            Logger.Shutdown();

            Assert.True(File.Exists(_expectedLogFilePath), $"The diagnostic restarter log file was never initialized on disk at '{_expectedLogFilePath}'.");

            string logContent = File.ReadAllText(_expectedLogFilePath);
            Assert.DoesNotContain(unexpectedMessage, logContent);
        }

        #endregion

        public override void Dispose()
        {
            // Force logger teardown first to unlock active files
            Logger.Shutdown();

            // Explicitly unlock and drop the keep-alive memory connection reference
            _dbKeepAliveConnection?.Dispose();

            // Rollback AppSettings matrix states to maintain complete isolation integrity across sibling execution tracks
            ConfigurationManager.AppSettings["DefaultConnection"] = _defaultConnection;
            ConfigurationManager.AppSettings["RestartTimeoutSeconds"] = _restartTimeoutSeconds;
            ConfigurationManager.AppSettings["Security:AESKeyFilePath"] = _aesKeyFilePath;
            ConfigurationManager.AppSettings["Security:AESIVFilePath"] = _aesIvFilePath;

            // Clean up temporary local workspace state file markers if generated
            try
            {
                string keyPath = Path.Combine(TempDirectory, "test_restarter.key");
                string ivPath = Path.Combine(TempDirectory, "test_restarter.iv");
                if (File.Exists(keyPath)) File.Delete(keyPath);
                if (File.Exists(ivPath)) File.Delete(ivPath);
            }
            catch
            {
                // Suppress lock warnings on ephemeral files cleanup
            }

            base.Dispose();
        }
    }
}
