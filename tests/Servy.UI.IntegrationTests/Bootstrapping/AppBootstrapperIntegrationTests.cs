using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Infrastructure.Helpers;
using Servy.UI.Bootstrapping;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using Helper = Servy.Testing.Helper;

namespace Servy.UI.IntegrationTests.Bootstrapping
{
    [Collection("Servy.UI.Bootstrapping.Tests")]
    public class AppBootstrapperIntegrationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _logFile;
        private readonly string _keyFile;
        private readonly string _ivFile;
        private readonly BootstrapperOptions _options;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public AppBootstrapperIntegrationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ServyBootstrapperTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);

            _logFile = $"BootstrapperTest_{Guid.NewGuid():N}.log";
            _keyFile = Path.Combine(_testDir, "test.key");
            _ivFile = Path.Combine(_testDir, "test.iv");

            _mockProcessKiller = new Mock<IProcessKiller>();

            // Seed raw cryptographic assets on disk
            File.WriteAllBytes(_keyFile, new byte[32]);
            File.WriteAllBytes(_ivFile, new byte[16]);

            _options = new BootstrapperOptions
            {
                LogFileName = _logFile,
                ResourcesNamespace = "Servy.UI.Bootstrapping.Tests",
                SecurityWarningTitle = "Admin Check Fail",
                SecurityWarningMessage = "Requires Administrative elevation.",
                SqliteVersionWarningTitle = "SQLite Core Fail",
                SqliteVersionWarningMessageFormat = "Detected: {0}, Required: {1}"
            };

            Logger.Shutdown();

            // Intercept Win32 dialog triggers completely inside headless loops
            SetStaticBooleanMock(typeof(MessageBox), "_bypassAndReturnOk", true);
        }

        public void Dispose()
        {
            Logger.Shutdown();
            ResetStaticField(typeof(MessageBox), "_bypassAndReturnOk");

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
            catch { /* Fail-silent on disk cleanup blocks */ }
        }

        #region Constructor Guard Tests

        [Fact]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(null, _mockProcessKiller.Object));
        }

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AppBootstrapper(_options, null));
        }

        #endregion

        #region Startup and Environmental Routing Tests

        [Fact]
        public async Task OnStartup_ValidEnvironment_ForcesSoftwareRenderingOnArg()
        {
            // Execute inside the managed thread context message loop to stay decoupled from external race states
            await Helper.RunOnSTA(async () =>
            {
                var app = SecureCreateApplication();
                var bootstrapper = new AppBootstrapper(_options, _mockProcessKiller.Object);

                SetStaticBooleanMock(typeof(SecurityHelper), "_isAdministratorMockValue", true);
                SetStaticBooleanMock(typeof(DatabaseValidator), "_isSqliteVersionSafeMockValue", true);

                try
                {
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
                new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            }
            return Application.Current;
        }

        private StartupEventArgs CreateStartupEventArgs(string[] args)
        {
            var ctor = typeof(StartupEventArgs).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            var startupEventArgs = (StartupEventArgs)ctor.Invoke(null);
            var backingField = typeof(StartupEventArgs).GetField("_args", BindingFlags.NonPublic | BindingFlags.Instance);
            backingField?.SetValue(startupEventArgs, args);
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