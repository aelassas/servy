using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Infrastructure.Helpers;
using Servy.Testing;
using Servy.UI.Bootstrapping;
using System.Data.SQLite;
using System.Reflection;
using System.Windows;
using Helper = Servy.Testing.Helper;

namespace Servy.UI.IntegrationTests.Bootstrapping
{
    [Collection("UiSta")]
    public class AppBootstrapperIntegrationTests : TempDirectoryTestBase
    {
        private readonly string _appSettingsFile;
        private readonly string _logFile;
        private readonly string _dbFile;
        private readonly string _keyFile;
        private readonly string _ivFile;
        private readonly BootstrapperOptions _options;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public AppBootstrapperIntegrationTests()
        {
            // Arrange
            _appSettingsFile = Path.Combine(TempDirectory, "appsettings.json");
            _logFile = $"BootstrapperTest_{Guid.NewGuid():N}.log";
            _dbFile = Path.Combine(TempDirectory, "test.db");
            _keyFile = Path.Combine(TempDirectory, "test.key");
            _ivFile = Path.Combine(TempDirectory, "test.iv");

            _mockProcessKiller = new Mock<IProcessKiller>();

            // Scaffold appsettings configurations
            var jsonConfig = $@"{{
                ""ConnectionStrings"": {{
                    ""DefaultConnection"": ""Data Source={_dbFile.Replace("\\", "\\\\")}""
                }},
                ""Security"": {{
                    ""AESKeyFilePath"": ""{_keyFile.Replace("\\", "\\\\")}"",
                    ""AESIVFilePath"": ""{_ivFile.Replace("\\", "\\\\")}""
                }}
            }}";
            File.WriteAllText(_appSettingsFile, jsonConfig);

            // Seed raw cryptographic assets to avoid runtime validation errors
            File.WriteAllBytes(_keyFile, new byte[32]);
            File.WriteAllBytes(_ivFile, new byte[16]);

            _options = new BootstrapperOptions
            {
                LogFileName = _logFile,
                AppSettingsFileName = _appSettingsFile,
                ResourcesNamespace = "Servy.UI.Bootstrapping.Tests",
                SecurityWarningTitle = "Admin Check Fail",
                SecurityWarningMessage = "Requires Administrative elevation.",
                SqliteVersionWarningTitle = "SQLite Core Fail",
                SqliteVersionWarningMessageFormat = "Detected: {0}, Required: {1}"
            };

            // Force static environmental resets
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
            catch { /* Fail-silent on log file cleanup */ }

            base.Dispose(); // Retrying recursive delete of TempDirectory
        }

        #region Constructor Guard Tests

        [Fact]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(null!, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(_options, null!));
            Assert.Equal("processKiller", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidLogFileName_ThrowsArgumentException(string? invalidLogFileName)
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
        public void Constructor_InvalidAppSettingsFileName_ThrowsArgumentException(string? invalidAppSettingsFileName)
        {
            // Arrange
            var options = CreateValidOptions();
            options.AppSettingsFileName = invalidAppSettingsFileName;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AppBootstrapper(options, _mockProcessKiller.Object));
            Assert.Equal("options", ex.ParamName);
            Assert.StartsWith("BootstrapperOptions.AppSettingsFileName is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidResourcesNamespace_ThrowsArgumentException(string? invalidResourcesNamespace)
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
        [InlineData("   ")]
        public void Constructor_InvalidSqliteVersionWarningMessageFormat_ThrowsArgumentException(string? invalidFormat)
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
                AppSettingsFileName = "appsettings.json",
                ResourcesNamespace = "Servy.UI.Tests",
                SqliteVersionWarningMessageFormat = "Version {0} is required, found {1}"
            };
        }

        #endregion

        #region Startup and Environmental Routing Tests

        [Fact]
        public async Task OnStartup_ValidEnvironment_ForcesSoftwareRenderingOnArg()
        {
            // Execute entirely within the persistent async STA context pump thread
            // to ensure internal thread safety boundaries match Application.Current initialization rules.
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                var app = Helper.EnsureApplication();
                var bootstrapper = new AppBootstrapper(_options, _mockProcessKiller.Object);

                // Use TrySetStaticField to dynamically intercept these configurations ONLY if the legacy
                // core assemblies contain mock seams. If they don't exist on this version, it safely proceeds,
                // relying on native environmental verification on local workstations.
                bool hasAdminMock = TrySetStaticField(typeof(SecurityHelper), "_isAdministratorMockValue", true);
                bool hasSqliteMock = TrySetStaticField(typeof(DatabaseValidator), "_isSqliteVersionSafeMockValue", true);

                try
                {
                    // Push Software Rendering command line switch parameter
                    var startupArgs = CreateStartupEventArgs(new[] { AppConfig.ForceSoftwareRenderingArg });

                    // Act
                    bool proceed = bootstrapper.OnStartup(app, startupArgs);

                    // Assert
                    Assert.True(proceed);
                    Assert.True(bootstrapper.ForceSoftwareRendering);
                }
                finally
                {
                    if (hasAdminMock) TestReflection.SetFieldStatic(typeof(SecurityHelper), "_isAdministratorMockValue", false);
                    if (hasSqliteMock) TestReflection.SetFieldStatic(typeof(DatabaseValidator), "_isSqliteVersionSafeMockValue", false);
                }

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Reflection Infrastructure Scaffolding Helpers

        private StartupEventArgs CreateStartupEventArgs(string[] args)
        {
            // 1. Target the internal parameterless constructor used by the WPF runtime lifecycle
            var ctor = typeof(StartupEventArgs).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

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

        /// <summary>
        /// Attempts to configure a static boolean field, returning false instead of crashing if the target field is missing.
        /// Useful for optional environment-dependent integration test configurations.
        /// </summary>
        private bool TrySetStaticField(Type targetType, string fieldName, bool value)
        {
            try
            {
                TestReflection.SetFieldStatic(targetType, fieldName, value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        #endregion
    }
}
