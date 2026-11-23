using System.Diagnostics;
using Moq;
using Servy.Core.Helpers;
using Servy.Testing;
using Servy.UI.Bootstrapping;

namespace Servy.UI.UnitTests.Bootstrapping
{
    /// <summary>
    /// Covers <see cref="AppBootstrapper.OnExit"/> giving the background start-up time to finish, so that the resume of the
    /// Servy services paused for an update is not torn down with the process when the app exits during the pause (#8166).
    /// </summary>
    public class AppBootstrapperExitTests
    {
        [Fact]
        public void OnExit_BackgroundInitializationStillRunning_WaitsForItToFinish()
        {
            // Arrange
            var options = new BootstrapperOptions
            {
                LogFileName = "test.log",
                AppSettingsFileName = "appsettings.json",
                ResourcesNamespace = "Servy.UI.Tests",
                SecurityWarningTitle = "Admin Check Fail",
                SecurityWarningMessage = "Requires Administrative elevation.",
                SqliteVersionWarningTitle = "SQLite Core Fail",
                SqliteVersionWarningMessageFormat = "Version {0} is required, found {1}"
            };
            var bootstrapper = new AppBootstrapper(options, new Mock<IProcessKiller>().Object);
            var token = bootstrapper.AppLifetimeToken;

            // Like the start-up: unwinds on the exit's cancellation, then the finally resumes the paused services
            var backgroundInit = Task.Run(async () =>
            {
                await Task.Delay(Timeout.Infinite, token).ContinueWith(_ => { }, TaskScheduler.Default);
                await Task.Delay(500);
            });
            TestReflection.SetField(bootstrapper, "_backgroundInitTask", backgroundInit);

            // Act
            bootstrapper.OnExit(null!);

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, backgroundInit.Status);
        }

        [Fact]
        public void OnExit_ServicesAlreadyResumed_DoesNotWaitForTheRestOfTheStartup()
        {
            // Arrange
            var options = new BootstrapperOptions
            {
                LogFileName = "test.log",
                AppSettingsFileName = "appsettings.json",
                ResourcesNamespace = "Servy.UI.Tests",
                SecurityWarningTitle = "Admin Check Fail",
                SecurityWarningMessage = "Requires Administrative elevation.",
                SqliteVersionWarningTitle = "SQLite Core Fail",
                SqliteVersionWarningMessageFormat = "Version {0} is required, found {1}"
            };
            var bootstrapper = new AppBootstrapper(options, new Mock<IProcessKiller>().Object);

            // Like a start-up that resumed the paused services and now waits for the deferred warning dialog (#8171)
            var neverEnds = new TaskCompletionSource<bool>();
            TestReflection.SetField(bootstrapper, "_backgroundInitTask", neverEnds.Task);
            var resumed = TestReflection.GetField<TaskCompletionSource<bool>>(bootstrapper, "_servicesResumed");
            resumed.SetResult(true);
            var stopwatch = Stopwatch.StartNew();

            // Act
            bootstrapper.OnExit(null!);

            // Assert
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"OnExit waited {stopwatch.Elapsed.TotalSeconds:0.0} s.");
        }
    }
}
