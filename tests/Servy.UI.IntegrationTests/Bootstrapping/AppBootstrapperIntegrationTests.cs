using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Infrastructure.Helpers;
using System.Reflection;
using System.Windows;
using Helper = Servy.Testing.Helper;

namespace Servy.UI.Bootstrapping.Tests
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
            _testDir = Path.Combine(Path.GetTempPath(), $"ServyBootstrapperTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);

            _appSettingsFile = Path.Combine(_testDir, "appsettings.json");
            _logFile = $"BootstrapperTest_{Guid.NewGuid():N}.log";
            _dbFile = Path.Combine(_testDir, "test.db");
            _keyFile = Path.Combine(_testDir, "test.key");
            _ivFile = Path.Combine(_testDir, "test.iv");

            // Generate clean mock dependencies
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

            // INTERCEPT MESSAGES: Inject a testing mode headless flag to drop interactive blocking dialog UI calls
            SetStaticBooleanMock(typeof(MessageBox), "_isHeadlessTestingMode", true);
        }

        public void Dispose()
        {
            Logger.Shutdown();
            ResetStaticField(typeof(MessageBox), "_isHeadlessTestingMode");
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
            Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(null!, _mockProcessKiller.Object));
        }

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(_options, null!));
        }

        #endregion

        #region Startup and Environmental Routing Tests

        [Fact]
        public async Task OnStartup_ValidEnvironment_ForcesSoftwareRenderingOnArg()
        {
            // Execute entirely within the persistent async STA context pump thread 
            // to ensure internal thread safety boundaries match Application.Current initialization rules.
            await Helper.RunInSTAContext(async () =>
            {
                var app = SecureCreateApplication();
                var bootstrapper = new AppBootstrapper(_options, _mockProcessKiller.Object);

                SetStaticBooleanMock(typeof(SecurityHelper), "_isAdministratorMockValue", true);
                SetStaticBooleanMock(typeof(DatabaseValidator), "_isSqliteVersionSafeMockValue", true);

                try
                {
                    // Push Software Rendering command line switch parameter
                    var startupArgs = CreateStartupEventArgs(new[] { AppConfig.ForceSoftwareRenderingArg });
                    bool proceed = bootstrapper.OnStartup(app, startupArgs);

                    Assert.True(proceed);
                    Assert.True(bootstrapper.ForceSoftwareRendering);
                }
                finally
                {
                    ResetStaticField(typeof(SecurityHelper), "_isAdministratorMockValue");
                    ResetStaticField(typeof(DatabaseValidator), "_isSqliteVersionSafeMockValue");
                }

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Reflection Infrastructure Scaffolding Helpers

        private Application SecureCreateApplication()
        {
            if (Application.Current == null)
            {
                // Force instantiate a headless application tracking scope bounds allocation context
                new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            }
            return Application.Current!;
        }

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

            // 2. Inject your custom test arguments directly into the private backing field
            // This bypasses the Environment.GetCommandLineArgs() fallback routine entirely.
            var backingField = typeof(StartupEventArgs).GetField("_args", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField != null)
            {
                backingField.SetValue(startupEventArgs, args);
            }
            else
            {
                throw new InvalidOperationException("Failed to locate private backing field '_args' inside StartupEventArgs.");
            }

            return startupEventArgs;
        }

        private void SetStaticBooleanMock(Type targetType, string fieldName, bool value)
        {
            var field = targetType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, value);
        }

        private void ResetStaticField(Type targetType, string fieldName)
        {
            var field = targetType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                if (field.FieldType == typeof(bool)) field.SetValue(null, false);
                else if (field.FieldType == typeof(string)) field.SetValue(null, null);
            }
        }

        #endregion
    }
}