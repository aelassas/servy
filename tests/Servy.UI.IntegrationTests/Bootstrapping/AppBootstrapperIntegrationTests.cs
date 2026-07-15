using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Infrastructure.Helpers;
using Servy.Testing;
using Servy.UI.Bootstrapping;
using System.Reflection;
using System.Windows;
using Helper = Servy.Testing.Helper;

namespace Servy.UI.IntegrationTests.Bootstrapping
{
    [Collection("Servy.UI.Bootstrapping.Tests")]
    public class AppBootstrapperIntegrationTests : IDisposable
    {
        private readonly string _testDir;
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
            _testDir = Path.Combine(Path.GetTempPath(), $"ServyBootstrapperTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);

            _appSettingsFile = Path.Combine(_testDir, "appsettings.json");
            _logFile = $"BootstrapperTest_{Guid.NewGuid():N}.log";
            _dbFile = Path.Combine(_testDir, "test.db");
            _keyFile = Path.Combine(_testDir, "test.key");
            _ivFile = Path.Combine(_testDir, "test.iv");

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

            // INTERCEPT MESSAGES: Safely enable UI Headless mode using the real "UiHeadless" wrapper assembly field.
            // This prevents blocking popups from stalling the test pipeline.
            var headlessType = Type.GetType("Servy.UI.Services.UiHeadless, Servy.UI")
                               ?? Type.GetType("Servy.UI.UiHeadless, Servy.UI");

            if (headlessType != null)
            {
                TestReflection.SetFieldStatic(headlessType, "<IsEnabled>k__BackingField", true);
            }
        }

        public void Dispose()
        {
            // Arrange
            Logger.Shutdown();

            // Clean up headless state
            var headlessType = Type.GetType("Servy.UI.Services.UiHeadless, Servy.UI")
                               ?? Type.GetType("Servy.UI.UiHeadless, Servy.UI");

            if (headlessType != null)
            {
                TestReflection.SetFieldStatic(headlessType, "<IsEnabled>k__BackingField", false);
            }

            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
                string globalLogPath = Path.Combine(Logger.LogsPath, _logFile);
                if (File.Exists(globalLogPath))
                {
                    File.Delete(globalLogPath);
                }
            }
            catch { /* Fail-silent on cleanup blocks */ }
        }

        #region Constructor Guard Tests

        [Fact]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(null!, _mockProcessKiller.Object));
        }

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(_options, null!));
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