using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.UnitTests.Security
{
    /// <summary>
    /// Covers <see cref="ServyExePermissionsHardener"/>: where it looks for <c>Set-ServyExePermissions.ps1</c>, how it
    /// starts PowerShell, how each exit code is reported, and that it never throws. The outcome of a run is only
    /// distinguishable in the log, so these tests drive the static <see cref="Logger"/> - hence the sequential
    /// logger collection.
    /// </summary>
    [Collection(LoggerCollection.Name)]
    public class ServyExePermissionsHardenerTests : IDisposable
    {
        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "servy-hardener-" + Guid.NewGuid().ToString("N"));

        #region FindScriptPath

        [Fact]
        public void FindScriptPath_ReleaseLayout_ScriptNextToExecutable_ReturnsIt()
        {
            // Arrange
            Directory.CreateDirectory(_tempRoot);
            var script = Path.Combine(_tempRoot, AppConfig.SetServyExePermissionsScriptFileName);
            File.WriteAllText(script, "# test");

            // Act
            var found = ServyExePermissionsHardener.FindScriptPath(_tempRoot, searchSetupFolders: false);

            // Assert
            Assert.Equal(Path.GetFullPath(script), found);
        }

        [Fact]
        public void FindScriptPath_ReleaseLayout_ScriptOnlyInAParentSetupFolder_ReturnsNull()
        {
            // Arrange: the release lookup must not wander out of the install folder
            var appDir = Path.Combine(_tempRoot, "app");
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(Path.Combine(_tempRoot, ServyExePermissionsHardener.SetupFolderName));
            File.WriteAllText(Path.Combine(_tempRoot, ServyExePermissionsHardener.SetupFolderName, AppConfig.SetServyExePermissionsScriptFileName), "# test");

            // Act
            var found = ServyExePermissionsHardener.FindScriptPath(appDir, searchSetupFolders: false);

            // Assert
            Assert.Null(found);
        }

        [Fact]
        public void FindScriptPath_DebugLayout_ScriptInRepositorySetupFolder_ReturnsIt()
        {
            // Arrange: src/Servy/bin/Debug/net/win-x64 under a repository root holding setup/
            var binDir = Path.Combine(_tempRoot, "src", "Servy", "bin", "Debug", "net", "win-x64");
            Directory.CreateDirectory(binDir);
            var setupDir = Path.Combine(_tempRoot, ServyExePermissionsHardener.SetupFolderName);
            Directory.CreateDirectory(setupDir);
            var script = Path.Combine(setupDir, AppConfig.SetServyExePermissionsScriptFileName);
            File.WriteAllText(script, "# test");

            // Act
            var found = ServyExePermissionsHardener.FindScriptPath(binDir, searchSetupFolders: true);

            // Assert
            Assert.Equal(script, found);
        }

        [Fact]
        public void FindScriptPath_DebugLayout_NoSetupFolderAnywhere_ReturnsNull()
        {
            // Arrange
            var binDir = Path.Combine(_tempRoot, "bin", "Debug");
            Directory.CreateDirectory(binDir);

            // Act
            var found = ServyExePermissionsHardener.FindScriptPath(binDir, searchSetupFolders: true);

            // Assert
            Assert.Null(found);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FindScriptPath_BlankBaseDirectory_ReturnsNull(string baseDirectory)
        {
            // Act
            var found = ServyExePermissionsHardener.FindScriptPath(baseDirectory, searchSetupFolders: true);

            // Assert
            Assert.Null(found);
        }

        #endregion

        #region BuildStartInfo

        [Fact]
        public void BuildStartInfo_RunsTheScriptNonInteractivelyForTheAccount()
        {
            // Act
            var psi = ServyExePermissionsHardener.BuildStartInfo(
                @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                @"C:\Program Files\Servy\Set-ServyExePermissions.ps1",
                @"DOMAIN\gMSA$");

            // Assert
            Assert.Equal(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", psi.FileName);
            Assert.Equal(
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"C:\\Program Files\\Servy\\Set-ServyExePermissions.ps1\" -TargetAccount \"DOMAIN\\gMSA$\"",
                psi.Arguments);
            Assert.False(psi.UseShellExecute);
            Assert.True(psi.RedirectStandardOutput);
            Assert.True(psi.RedirectStandardError);
            Assert.True(psi.CreateNoWindow);
        }

        #endregion

        #region HardenAsync

        [Fact]
        public async Task HardenAsync_ScriptExitsZero_ReturnsTrueAndRunsItForTheTrimmedAccount()
        {
            // Arrange
            var sut = new TestableHardener { Result = new ExePermissionsScriptResult(0, "Successfully hardened") };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("  .\\svc-account  ", CancellationToken.None));

            // Assert
            Assert.True(capture.Result);
            var psi = Assert.Single(sut.Runs);
            Assert.EndsWith("-TargetAccount \".\\svc-account\"", psi.Arguments);
            Assert.Contains("-File \"" + TestableHardener.FakeScriptPath + "\"", psi.Arguments);
            Assert.Equal(AppConfig.SetServyExePermissionsTimeoutMs, sut.TimeoutMs);
            Assert.Contains("hardened Servy's binaries, configuration files and database for '.\\svc-account'", capture.Log);
        }

        [Fact]
        public async Task HardenAsync_ScriptExitsTwo_ReturnsFalseAndWarnsWithTheOutput()
        {
            // Arrange
            var sut = new TestableHardener { Result = new ExePermissionsScriptResult(2, "Not hardened (missing): Servy.Service.CLI.exe") };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Contains("could not harden every file for 'svc'", capture.Log);
            Assert.Contains("Not hardened (missing): Servy.Service.CLI.exe", capture.Log);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task HardenAsync_ScriptFails_ReturnsFalseAndLogsTheExitCode(int exitCode)
        {
            // Arrange
            var sut = new TestableHardener { Result = new ExePermissionsScriptResult(exitCode, "FAILED to harden") };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Contains($"failed for 'svc' with exit code {exitCode}", capture.Log);
            Assert.Contains("FAILED to harden", capture.Log);
        }

        [Fact]
        public async Task HardenAsync_ScriptTimesOut_ReturnsFalseAndLogsTheTimeout()
        {
            // Arrange
            var sut = new TestableHardener { Result = new ExePermissionsScriptResult(null, "partial output") };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Contains($"did not finish within {AppConfig.SetServyExePermissionsTimeoutMs / AppConfig.MillisecondsPerSecond} seconds for 'svc'", capture.Log);
        }

        [Fact]
        public async Task HardenAsync_ScriptNotFound_ReturnsFalseWithoutRunningAnything()
        {
            // Arrange
            var sut = new TestableHardener { ScriptPath = null };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Empty(sut.Runs);
            Assert.Contains($"{AppConfig.SetServyExePermissionsScriptFileName} was not found", capture.Log);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task HardenAsync_BlankAccount_ReturnsFalseWithoutRunningAnything(string account)
        {
            // Arrange
            var sut = new TestableHardener();

            // Act
            var result = await sut.HardenAsync(account, CancellationToken.None);

            // Assert
            Assert.False(result);
            Assert.Empty(sut.Runs);
        }

        [Fact]
        public async Task HardenAsync_RunnerCancelled_ReturnsFalseInsteadOfThrowing()
        {
            // Arrange
            var sut = new TestableHardener { Exception = new OperationCanceledException() };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Contains("was cancelled", capture.Log);
        }

        [Fact]
        public async Task HardenAsync_PowerShellCannotStart_ReturnsFalseInsteadOfThrowing()
        {
            // Arrange
            var sut = new TestableHardener { Exception = new Win32Exception(2, "The system cannot find the file specified") };

            // Act
            var capture = await LogCapture.RunAsync(() => sut.HardenAsync("svc", CancellationToken.None));

            // Assert
            Assert.False(capture.Result);
            Assert.Contains($"Failed to run {AppConfig.SetServyExePermissionsScriptFileName} for 'svc'", capture.Log);
        }

        #endregion

        #region RunScriptAsync (real process)

        [Fact]
        public async Task RunScriptAsync_ProcessExits_ReturnsItsExitCodeAndOutput()
        {
            // Arrange
            var sut = new TestableHardener();
            var psi = CmdStartInfo("/c echo hardened-out & echo hardened-err 1>&2 & exit 3");

            // Act
            var result = await sut.RunRealScriptAsync(psi, 30_000, CancellationToken.None);

            // Assert
            Assert.Equal(3, result.ExitCode);
            Assert.Contains("hardened-out", result.Output);
            Assert.Contains("hardened-err", result.Output);
        }

        [Fact]
        public async Task RunScriptAsync_ProcessOutlivesTheTimeout_IsStoppedAndReportsNoExitCode()
        {
            // Arrange
            var sut = new TestableHardener();
            // PowerShell sleeping directly (not a child of cmd.exe) so stopping the process leaves nothing behind
            var psi = CmdStartInfo(string.Empty);
            psi.FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            psi.Arguments = "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30";
            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await sut.RunRealScriptAsync(psi, 500, CancellationToken.None);

            // Assert
            Assert.Null(result.ExitCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"the process was not stopped at the timeout ({stopwatch.Elapsed})");
        }

        private static ProcessStartInfo CmdStartInfo(string arguments) => new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        #endregion

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }

        /// <summary>
        /// Replaces the script lookup and the process launch with recorded fakes.
        /// </summary>
        private sealed class TestableHardener : ServyExePermissionsHardener
        {
            public const string FakeScriptPath = @"C:\Servy\Set-ServyExePermissions.ps1";

            public string ScriptPath { get; set; } = FakeScriptPath;

            public ExePermissionsScriptResult Result { get; set; } = new ExePermissionsScriptResult(0, string.Empty);

            public Exception Exception { get; set; }

            public List<ProcessStartInfo> Runs { get; } = new List<ProcessStartInfo>();

            public int TimeoutMs { get; private set; }

            public Task<ExePermissionsScriptResult> RunRealScriptAsync(ProcessStartInfo startInfo, int timeoutMs, CancellationToken cancellationToken)
                => base.RunScriptAsync(startInfo, timeoutMs, cancellationToken);

            protected override string ResolveScriptPath() => ScriptPath;

            protected override Task<ExePermissionsScriptResult> RunScriptAsync(ProcessStartInfo startInfo, int timeoutMs, CancellationToken cancellationToken)
            {
                Runs.Add(startInfo);
                TimeoutMs = timeoutMs;
                if (Exception != null)
                    throw Exception;
                return Task.FromResult(Result);
            }
        }
    }
}
