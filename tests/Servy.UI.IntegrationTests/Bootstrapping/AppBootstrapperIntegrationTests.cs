using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Infrastructure.Helpers;
using Servy.Testing;
using Servy.UI.Bootstrapping;
using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using Helper = Servy.Testing.Helper;

namespace Servy.UI.IntegrationTests.Bootstrapping
{
    [Collection(UiStaCollection.Name)]
    public class AppBootstrapperIntegrationTests : TempDirectoryTestBase
    {
        private readonly string _logFile;
        private readonly string _keyFile;
        private readonly string _ivFile;
        private readonly BootstrapperOptions _options;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public AppBootstrapperIntegrationTests()
        {
            // Arrange
            _logFile = $"BootstrapperTest_{Guid.NewGuid():N}.log";
            _keyFile = Path.Combine(TempDirectory, "test.key");
            _ivFile = Path.Combine(TempDirectory, "test.iv");

            _mockProcessKiller = new Mock<IProcessKiller>();

            // Seed raw cryptographic assets on disk
            File.WriteAllBytes(_keyFile, new byte[32]);
            File.WriteAllBytes(_ivFile, new byte[16]);

            _options = CreateValidOptions();
            _options.LogFileName = _logFile;

            Logger.Shutdown();
        }

        public override void Dispose()
        {
            Logger.Shutdown();

            // Clear SQLite connection pools so any open DB locks are released
            SQLiteConnection.ClearAllPools();

            try
            {
                string globalLogPath = Path.Combine(Logger.LogsPath, _logFile);
                if (File.Exists(globalLogPath))
                {
                    File.Delete(globalLogPath);
                }
            }
            catch { /* Fail-silent on disk cleanup blocks */ }

            base.Dispose(); // Retrying recursive delete of TempDirectory
        }

        #region Constructor Guard Tests

        [Fact]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(null, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(_options, null));
            Assert.Equal("processKiller", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidLogFileName_ThrowsArgumentException(string invalidLogFileName)
        {
            // Arrange
            var options = CreateValidOptions();
            options.LogFileName = invalidLogFileName;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AppBootstrapper(options, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
            Assert.StartsWith("BootstrapperOptions.LogFileName is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidResourcesNamespace_ThrowsArgumentException(string invalidResourcesNamespace)
        {
            // Arrange
            var options = CreateValidOptions();
            options.ResourcesNamespace = invalidResourcesNamespace;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AppBootstrapper(options, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
            Assert.StartsWith("BootstrapperOptions.ResourcesNamespace is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_InvalidSecurityWarningTitle_ThrowsArgumentException(string invalidSecurityWarningTitle)
        {
            // Arrange
            var options = CreateValidOptions();
            options.SecurityWarningTitle = invalidSecurityWarningTitle;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AppBootstrapper(options, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
            Assert.StartsWith("BootstrapperOptions.SecurityWarningTitle is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_InvalidSecurityWarningMessage_ThrowsArgumentException(string invalidSecurityWarningMessage)
        {
            // Arrange
            var options = CreateValidOptions();
            options.SecurityWarningMessage = invalidSecurityWarningMessage;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AppBootstrapper(options, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
            Assert.StartsWith("BootstrapperOptions.SecurityWarningMessage is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_InvalidSqliteVersionWarningTitle_ThrowsArgumentException(string invalidSqliteVersionWarningTitle)
        {
            // Arrange
            var options = CreateValidOptions();
            options.SqliteVersionWarningTitle = invalidSqliteVersionWarningTitle;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AppBootstrapper(options, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
            Assert.StartsWith("BootstrapperOptions.SqliteVersionWarningTitle is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_InvalidSqliteVersionWarningMessageFormat_ThrowsArgumentException(string invalidFormat)
        {
            // Arrange
            var options = CreateValidOptions();
            options.SqliteVersionWarningMessageFormat = invalidFormat;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AppBootstrapper(options, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
            Assert.StartsWith("BootstrapperOptions.SqliteVersionWarningMessageFormat is required.", ex.Message);
        }

        private BootstrapperOptions CreateValidOptions()
        {
            return new BootstrapperOptions
            {
                LogFileName = "test.log",
                ResourcesNamespace = "Servy.UI.Tests",
                SecurityWarningTitle = "Admin Check Fail",
                SecurityWarningMessage = "Requires Administrative elevation.",
                SqliteVersionWarningTitle = "SQLite Core Fail",
                SqliteVersionWarningMessageFormat = "Version {0} is required, found {1}"
            };
        }

        #endregion

        #region Startup and Environmental Routing Tests

        [Fact]
        public async Task OnStartup_ValidEnvironment_ForcesSoftwareRenderingOnArg()
        {
            // Execute inside the managed thread context message loop to stay decoupled from external race states
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var app = Helper.EnsureApplication();
                var bootstrapper = new AppBootstrapper(_options, _mockProcessKiller.Object);

                // Both environment gates OnStartup runs before the assertion are steered through their
                // production seams, so "ValidEnvironment" is an arrangement of this test rather than a
                // property of the host it happens to run on. The counters are what make that visible:
                // production code that stops reading a seam fails here instead of silently passing
                // because the runner is elevated and ships a recent SQLite. They are asserted as
                // "consulted at all" rather than as an exact count because IsAdministrator is also
                // read by the ACL hardening OnStartup performs on the key and database files.
                var originalAdminSeam = SecurityHelper.IsAdministratorCore;
                var originalSqliteSeam = DatabaseValidator.GetSqliteVersion;

                int adminChecks = 0;
                int sqliteChecks = 0;
                SecurityHelper.IsAdministratorCore = () => { adminChecks++; return true; };
                DatabaseValidator.GetSqliteVersion = () => { sqliteChecks++; return AppConfig.MinRequiredSqliteVersion.ToString(); };

                try
                {
                    var startupArgs = CreateStartupEventArgs(new[] { AppConfig.ForceSoftwareRenderingArg });

                    // Act
                    bool proceed = bootstrapper.OnStartup(app, startupArgs);

                    // Assert
                    Assert.True(adminChecks > 0, "The elevation check never read SecurityHelper.IsAdministratorCore, so this test measured the host instead of its arrangement.");
                    Assert.True(sqliteChecks > 0, "The SQLite version check never read DatabaseValidator.GetSqliteVersion, so this test measured the host instead of its arrangement.");
                    Assert.True(proceed);
                    Assert.True(bootstrapper.ForceSoftwareRendering);
                }
                finally
                {
                    SecurityHelper.IsAdministratorCore = originalAdminSeam;
                    DatabaseValidator.GetSqliteVersion = originalSqliteSeam;
                }

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Reflection Infrastructure Scaffolding Helpers

        private StartupEventArgs CreateStartupEventArgs(string[] args)
        {
            // 1. Target the internal parameterless constructor used by the WPF runtime lifecycle
            var ctor = typeof(StartupEventArgs).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (ctor == null)
            {
                throw new InvalidOperationException("Failed to locate the internal parameterless constructor for StartupEventArgs.");
            }

            var startupEventArgs = (StartupEventArgs)ctor.Invoke(null);

            // 2. Inject your custom test arguments directly into the private backing field using TestReflection
            try
            {
                TestReflection.SetField(startupEventArgs, "_args", args);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("Failed to locate private backing field '_args' inside StartupEventArgs.", ex);
            }

            return startupEventArgs;
        }

        #endregion
    }
}
