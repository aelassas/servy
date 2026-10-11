using Servy.CLI.Runtime;
using Servy.CLI.UnitTests.Runtime;
using Servy.Core.Config;
using Servy.Testing;

namespace Servy.CLI.UnitTests
{
    [Collection(ConsoleTestCollection.Name)]
    public class ProgramTests : IDisposable
    {
        private const string AesKeyFileName = "test_aes.key";
        private const string AesIvFileName = "test_aes.iv";
        private const string DatabaseFileName = "Test_Servy.db";

        private readonly TextWriter _originalConsoleOut;
        private readonly TextWriter _originalConsoleError;

        public ProgramTests()
        {
            // Arrange
            _originalConsoleOut = Console.Out;
            _originalConsoleError = Console.Error;

            // The core settings are read-only in production (always the vault under ProgramData),
            // so the fixture points Program.Main at an isolated database and a RELATIVE key path
            // through the test-only override instead of appsettings.cli.json.
            string fallbackDatabaseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);
            string testConnection = string.Format("Data Source={0};Version=3;", fallbackDatabaseFile);

            CoreSettingsLoader.TestOverride = new CoreSettingsLoader.CoreSettings(testConnection, AesKeyFileName, AesIvFileName);
        }

        #region Console Validation Logic Branches

        [Fact]
        public void IsRealConsole_WhenHostIsNonInteractiveOrRedirected_ShortCircuitsToFalse()
        {
            Assert.SkipWhen(Environment.UserInteractive
                && !Console.IsOutputRedirected
                && !Console.IsErrorRedirected,
                "Requires a non-interactive/redirected host.");

            // Arrange
            // The skip guard above is the exact complement of the first two guards of
            // IsRealConsole, so the body only ever runs in a state that short-circuits the
            // method. Pin that precondition explicitly rather than leaving it implicit in
            // the skip expression: this test covers the short-circuit only, and the Win32
            // half of the method (GetConsoleWindow and the Console.WindowHeight probe)
            // stays out of reach without a redirection seam on Program.
            bool shortCircuitStateHolds = !Environment.UserInteractive
                || Console.IsOutputRedirected
                || Console.IsErrorRedirected;

            // Act
            bool isReal = Program.IsRealConsole();

            // Assert
            Assert.True(shortCircuitStateHolds,
                "The skip guard must leave only host states that short-circuit IsRealConsole.");
            Assert.False(isReal);
        }

        #endregion

        #region Execution Flow & Parsing Branch Coverage

        [Fact]
        public async Task Main_EmptyArguments_InjectsHelpVerbAndExitsWithSuccess()
        {
            // Arrange
            string[] emptyArgs = Array.Empty<string>();

            // Act
            var result = await ConsoleCapture.RunAsync(async () =>
            {
                return await Program.Main(emptyArgs);
            });

            // Assert
            // Match against the actual verbs listed in the auto-generated help index screen
            Assert.Equal((int)CliExitCode.Success, result.Result);
            Assert.Contains("install", result.StdOut);
            Assert.Contains("uninstall", result.StdOut);
        }

        [Fact]
        public async Task Main_HelpFlagProvided_ReturnsSuccessExitCode()
        {
            // Arrange
            string[] args = { "--help" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            // Match against the actual verbs listed in the auto-generated help index screen
            Assert.Equal((int)CliExitCode.Success, result.Result);
            Assert.Contains("install", result.StdOut);
            Assert.Contains("uninstall", result.StdOut);
        }

        [Fact]
        public async Task Main_QuietFlagProvided_AltersExecutionToQuietPath()
        {
            // Arrange
            // Supply the service name via the required explicit option switch (-n)
            // to satisfy CommandLineParser constraints and route into the quiet logic path.
            string[] args = { "start", "-n", "NonExistentServiceForTestingOnly", "--quiet" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            // The command fails before it reaches the start handler: the fixture overrides the key with a
            // relative AESKeyFilePath, and the start verb is mapped with requireDatabase: true, so
            // AppFoldersHelper.EnsureFolders rejects the non-absolute key path and Main's catch-all
            // returns Error (1). The fixture is deliberately left relative: absolute paths would make
            // this unit test touch the real ProgramData folder ACLs, create the event source and
            // extract the embedded service executable.
            Assert.Equal((int)CliExitCode.Error, result.Result);

            // Verify that no loading animation frames or status text fragments were written to stdout
            Assert.True(string.IsNullOrEmpty(result.StdOut), "Console output should be completely suppressed when the --quiet flag is supplied.");

            // Stderr is the other half of the contract and the channel this scenario actually uses:
            // --quiet bypasses the loading animation (the isQuiet branch of ExecuteWithRuntimeAsync in Program.cs) but does not silence failure
            // reporting, which Helper.PrintAndReturn writes to Console.Error. Assert it here so a
            // regression that routes the failure message to stdout, or drops it entirely, is caught.
            Assert.False(string.IsNullOrWhiteSpace(result.StdErr), "The failure message must still reach stderr; --quiet suppresses the progress animation only.");

            // start is the eighth MapResult lambda mapped with requireDatabase: true, and the only one
            // Main_DatabaseBoundVerb_BootstrapsDatabaseBeforeReachingItsHandler does not cover. Pinning
            // the message here is what makes that mapping falsifiable: remapped to requireDatabase:
            // false the verb would reach EnsureServiceBinariesAsync, or its own handler, and still end
            // in an Error exit with some text on stderr - which the three assertions above cannot tell
            // apart from this one.
            Assert.Contains("aesKeyFilePath must be an absolute path", result.StdErr);
        }

        [Fact]
        public async Task Main_UnknownCommandProvided_ReportsItAndReturnsErrorExitCode()
        {
            // Arrange
            // A verb that is not in GetVerbs() and does not start with a global flag dash, which is
            // the exact shape the unknown-command guard exists to reject. No database, configuration
            // or embedded-resource bootstrap is needed: the guard runs before all of them.
            string[] args = { "frobnicate" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            // Before the guard existed a mistyped verb was silently coerced to the help verb and exited
            // 0, masking failures in automation, so the non-zero exit code is half the contract.
            Assert.Equal((int)CliExitCode.Error, result.Result);

            // The other half: the message names the offending argument and reaches stderr, which is the
            // only place in the solution that text is produced, so this assertion cannot pass unless the
            // guard branch itself ran.
            Assert.Contains("Unknown command 'frobnicate'", result.StdErr);
        }

        [Fact]
        public async Task Main_StatusOfMissingService_TakesFastPathAndPrintsNotInstalled()
        {
            // Arrange
            // status is the one verb mapped with requireDatabase: false and requireBinaries: false, so it is
            // the only one that reaches its handler through ExecuteWithRuntimeAsync's fast path - no
            // EnsureDatabase, no EnsureServiceBinariesAsync, and therefore no ProgramData folder ACLs, no
            // event source and no embedded-resource extraction. ServiceManager.GetServiceStatus returns null
            // for a name the SCM does not know, and IsServiceInstalled then reports false, so the command
            // returns Ok with the NotInstalled token.
            string[] args = { "status", "-n", "NonExistentServiceForTestingOnly" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            // This is the only test that reaches the single PrintAndReturnAsync line the nine MapResult verb
            // lambdas return through. Main_QuietFlagProvided_AltersExecutionToQuietPath and the
            // Main_DatabaseBoundVerb_BootstrapsDatabaseBeforeReachingItsHandler theory enter the other
            // eight lambdas too, but they throw in the bootstrap before they get there.
            Assert.Equal((int)CliExitCode.Success, result.Result);
            Assert.Contains("Service status for 'NonExistentServiceForTestingOnly': NotInstalled", result.StdOut);
        }

        [Theory]
        [InlineData("install -n NonExistentServiceForTestingOnly -p C:\\NonExistent\\app.exe")]
        [InlineData("uninstall -n NonExistentServiceForTestingOnly")]
        [InlineData("stop -n NonExistentServiceForTestingOnly")]
        [InlineData("restart -n NonExistentServiceForTestingOnly")]
        [InlineData("export -n NonExistentServiceForTestingOnly -c xml -p C:\\NonExistent\\out.xml")]
        [InlineData("import -c xml -p C:\\NonExistent\\in.xml")]
        [InlineData("show")]
        public async Task Main_DatabaseBoundVerb_BootstrapsDatabaseBeforeReachingItsHandler(string commandLine)
        {
            // Arrange
            // These seven verbs are seven of the eight MapResult lambdas mapped with
            // requireDatabase: true - start, the eighth, is pinned the same way by
            // Main_QuietFlagProvided_AltersExecutionToQuietPath - so
            // ExecuteWithRuntimeAsync runs EnsureDatabase before the handler. The fixture overrides the
            // key with a relative AESKeyFilePath, so AppFoldersHelper.EnsureFolders rejects it on its
            // absolute-path check - before any folder, ACL, event source, database or embedded
            // resource work - and Main's catch-all reports that message on stderr. A verb remapped
            // to requireDatabase: false would reach its handler, or EnsureServiceBinariesAsync
            // first, and report something else. status is deliberately absent: it is the one verb
            // mapped false, pinned by Main_StatusOfMissingService_TakesFastPathAndPrintsNotInstalled.
            string[] args = commandLine.Split(' ');

            // Act
            var result = await ConsoleCapture.RunAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.Result);
            Assert.Contains("aesKeyFilePath must be an absolute path", result.StdErr);
        }

        [Fact]
        public async Task Main_KnownVerbMissingRequiredOption_ReturnsErrorExitCode()
        {
            // Arrange
            // A recognized verb passes the unknown-command guard, so the missing required -n is reported by
            // CommandLineParser as a MissingRequiredOptionError. That is neither help nor version, which is
            // the one arm of the MapResult error lambda the suite never exercised: the empty-args and --help
            // tests both take its IsHelp() branch.
            string[] args = { "status" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () =>
            {
                return await Program.Main(args);
            });

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.Result);
            Assert.DoesNotContain("Service status for", result.StdOut);
        }

        #endregion

        #region Runtime Seam: Bootstrap Branches

        [Fact]
        public async Task RunAsync_VulnerableSqliteVersion_ReturnsIncompatibleEnvironmentBeforeAnyBootstrap()
        {
            // Arrange
            var runtime = new FakeCliRuntime { SqliteVersionSafe = false, SqliteVersion = "3.40.0" };
            string[] args = { "show" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () => await Program.RunAsync(args, runtime));

            // Assert
            // The CLI-specific sentinel 2, not a generic error, and nothing past the version check ran.
            Assert.Equal((int)CliExitCode.IncompatibleEnvironment, result.Result);
            Assert.Contains("[CRITICAL] Vulnerable SQLite version detected: 3.40.0", result.StdErr);
            Assert.Contains($"This version of Servy requires SQLite {AppConfig.MinRequiredSqliteVersion}+.", result.StdErr);
            Assert.Equal(new[] { nameof(ICliRuntime.IsSqliteVersionSafe) }, runtime.Calls);
        }

        [Fact]
        public async Task RunAsync_BootstrapCancelled_ReportsTheCancellationAndReturnsError()
        {
            // Arrange
            // A cancelled bootstrap (Ctrl+C during the database or binaries step) surfaces as OperationCanceledException,
            // which Main reports with its own message instead of the generic "unexpected error" one.
            var runtime = new FakeCliRuntime { EnsureDatabaseThrows = new OperationCanceledException() };
            string[] args = { "show", "--quiet" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () => await Program.RunAsync(args, runtime));

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.Result);
            Assert.Contains("Operation cancelled by user.", result.StdErr);
            Assert.DoesNotContain("An unexpected error occurred", result.StdErr);
        }

        [Theory]
        [InlineData("install -n NonExistentServiceForTestingOnly -p C:\\NonExistent\\app.exe --quiet")]
        [InlineData("start -n NonExistentServiceForTestingOnly --quiet")]
        [InlineData("restart -n NonExistentServiceForTestingOnly --quiet")]
        [InlineData("import -c xml -p C:\\NonExistent\\in.xml --quiet")]
        public async Task RunAsync_VerbNeedingTheBinaries_EnsuresTheDatabaseThenTheBinariesBeforeItsHandler(string commandLine)
        {
            // Arrange
            // These four verbs are the MapResult lambdas mapped with requireBinaries: true. The binaries step throws,
            // so the handler is never reached and the stderr text can only come from that step.
            var runtime = new FakeCliRuntime { EnsureServiceBinariesThrows = new InvalidOperationException("binaries step reached") };
            string[] args = commandLine.Split(' ');

            // Act
            var result = await ConsoleCapture.RunAsync(async () => await Program.RunAsync(args, runtime));

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.Result);
            Assert.Contains("An unexpected error occurred: binaries step reached", result.StdErr);
            Assert.Equal(
                new[] { nameof(ICliRuntime.IsSqliteVersionSafe), nameof(ICliRuntime.EnsureDatabase), nameof(ICliRuntime.EnsureServiceBinariesAsync) },
                runtime.Calls);
        }

        [Fact]
        public async Task RunAsync_InteractiveConsole_RunsTheBootstrapBehindTheLoadingAnimation()
        {
            // Arrange
            var runtime = new FakeCliRuntime { RealConsole = true, EnsureDatabaseThrows = new InvalidOperationException("database step reached") };
            string[] args = { "show" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () => await Program.RunAsync(args, runtime));

            // Assert
            // The database step runs inside the animation, not before or after it.
            Assert.Equal((int)CliExitCode.Error, result.Result);
            Assert.Contains("An unexpected error occurred: database step reached", result.StdErr);
            Assert.Equal(
                new[]
                {
                    nameof(ICliRuntime.IsSqliteVersionSafe),
                    nameof(ICliRuntime.IsRealConsole),
                    nameof(ICliRuntime.RunWithLoadingAnimationAsync),
                    nameof(ICliRuntime.EnsureDatabase),
                },
                runtime.Calls);
        }

        [Fact]
        public async Task RunAsync_NoRealConsole_RunsTheBootstrapWithoutTheLoadingAnimation()
        {
            // Arrange
            var runtime = new FakeCliRuntime { RealConsole = false, EnsureDatabaseThrows = new InvalidOperationException("database step reached") };
            string[] args = { "show" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () => await Program.RunAsync(args, runtime));

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.Result);
            Assert.Equal(
                new[] { nameof(ICliRuntime.IsSqliteVersionSafe), nameof(ICliRuntime.IsRealConsole), nameof(ICliRuntime.EnsureDatabase) },
                runtime.Calls);
        }

        [Fact]
        public async Task RunAsync_QuietFlag_SkipsTheConsoleProbeAndTheLoadingAnimation()
        {
            // Arrange
            // --quiet short-circuits before the console is probed, even on a real console.
            var runtime = new FakeCliRuntime { RealConsole = true, EnsureDatabaseThrows = new InvalidOperationException("database step reached") };
            string[] args = { "show", "--quiet" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () => await Program.RunAsync(args, runtime));

            // Assert
            Assert.Equal((int)CliExitCode.Error, result.Result);
            Assert.Equal(new[] { nameof(ICliRuntime.IsSqliteVersionSafe), nameof(ICliRuntime.EnsureDatabase) }, runtime.Calls);
        }

        [Fact]
        public async Task RunAsync_StatusVerb_TakesTheFastPathWithoutAnyBootstrapStep()
        {
            // Arrange
            // status is the one verb mapped with requireDatabase: false and requireBinaries: false.
            var runtime = new FakeCliRuntime { RealConsole = true };
            string[] args = { "status", "-n", "NonExistentServiceForTestingOnly" };

            // Act
            var result = await ConsoleCapture.RunAsync(async () => await Program.RunAsync(args, runtime));

            // Assert
            Assert.Equal((int)CliExitCode.Success, result.Result);
            Assert.Contains("Service status for 'NonExistentServiceForTestingOnly': NotInstalled", result.StdOut);
            Assert.Equal(new[] { nameof(ICliRuntime.IsSqliteVersionSafe) }, runtime.Calls);
        }

        #endregion

        #region Console Probe, Cancellation and Cleanup Seams

        /// <summary>
        /// A settable <see cref="IConsoleEnvironment"/> for the <see cref="Program.IsRealConsole(IConsoleEnvironment)"/> checks.
        /// </summary>
        private sealed class FakeConsoleEnvironment : IConsoleEnvironment
        {
            public bool UserInteractive { get; set; } = true;
            public bool IsOutputRedirected { get; set; }
            public bool IsErrorRedirected { get; set; }
            public IntPtr ConsoleWindow { get; set; } = new IntPtr(42);
            public int WindowHeight { get; set; } = 30;
            public bool WindowHeightThrows { get; set; }
            public int ProbesAfterRedirection { get; private set; }

            public IntPtr GetConsoleWindow()
            {
                ProbesAfterRedirection++;
                return ConsoleWindow;
            }

            public int GetWindowHeight()
            {
                ProbesAfterRedirection++;
                if (WindowHeightThrows) throw new IOException("The handle is invalid.");
                return WindowHeight;
            }
        }

        [Fact]
        public void IsRealConsole_InteractiveConsoleWithAWindowAndABuffer_ReturnsTrue()
        {
            // Arrange
            var console = new FakeConsoleEnvironment();

            // Act
            bool isReal = Program.IsRealConsole(console);

            // Assert
            Assert.True(isReal);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        public void IsRealConsole_Session0OrRedirectedOutput_ReturnsFalseWithoutTouchingTheWin32Console(bool userInteractive, bool outputRedirected, bool errorRedirected)
        {
            // Arrange
            var console = new FakeConsoleEnvironment
            {
                UserInteractive = userInteractive,
                IsOutputRedirected = outputRedirected,
                IsErrorRedirected = errorRedirected,
            };

            // Act
            bool isReal = Program.IsRealConsole(console);

            // Assert
            Assert.False(isReal);
            Assert.Equal(0, console.ProbesAfterRedirection);
        }

        [Fact]
        public void IsRealConsole_NoConsoleWindow_ReturnsFalseWithoutReadingTheBuffer()
        {
            // Arrange
            var console = new FakeConsoleEnvironment { ConsoleWindow = IntPtr.Zero, WindowHeightThrows = true };

            // Act
            bool isReal = Program.IsRealConsole(console);

            // Assert
            Assert.False(isReal);
            Assert.Equal(1, console.ProbesAfterRedirection);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        public void IsRealConsole_EmptyOrUnreachableBuffer_ReturnsFalse(int windowHeight, bool windowHeightThrows)
        {
            // Arrange
            var console = new FakeConsoleEnvironment { WindowHeight = windowHeight, WindowHeightThrows = windowHeightThrows };

            // Act
            bool isReal = Program.IsRealConsole(console);

            // Assert
            Assert.False(isReal);
        }

        [Fact]
        public void ConsoleEnvironment_ReportsTheProcessSessionAndRedirectionState()
        {
            // Arrange
            var console = new ConsoleEnvironment();

            // Act
            bool userInteractive = console.UserInteractive;
            bool outputRedirected = console.IsOutputRedirected;
            bool errorRedirected = console.IsErrorRedirected;

            // Assert
            Assert.Equal(Environment.UserInteractive, userInteractive);
            Assert.Equal(Console.IsOutputRedirected, outputRedirected);
            Assert.Equal(Console.IsErrorRedirected, errorRedirected);
        }

        [Fact]
        public void HandleCancelKeyPress_FirstPress_MarksItHandledCancelsAndWritesTheNotice()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            using var error = new StringWriter();
            bool markedHandled = false;

            // Act
            bool started = Program.HandleCancelKeyPress(cts, () => markedHandled = true, error);

            // Assert
            // Marking the press handled is what keeps the process alive while the command cancels.
            Assert.True(started);
            Assert.True(markedHandled);
            Assert.True(cts.IsCancellationRequested);
            Assert.Contains(Program.CancellingMessage, error.ToString());
        }

        [Fact]
        public void HandleCancelKeyPress_SecondPress_LeavesItUnhandledSoTheProcessTerminates()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            using var error = new StringWriter();
            bool markedHandled = false;

            // Act
            bool started = Program.HandleCancelKeyPress(cts, () => markedHandled = true, error);

            // Assert
            Assert.False(started);
            Assert.False(markedHandled);
            Assert.Equal(string.Empty, error.ToString());
        }

        [Fact]
        public void TryRun_ActionThrows_SwallowsTheExceptionSoTheTeardownContinues()
        {
            // Arrange
            bool secondCleanupRan = false;

            // Act
            var ex = Record.Exception(() =>
            {
                Program.TryRun(() => throw new ObjectDisposedException("dbContext"), "dbContext");
                Program.TryRun(() => secondCleanupRan = true, "secureData");
            });

            // Assert
            Assert.Null(ex);
            Assert.True(secondCleanupRan);
        }

        #endregion

        public void Dispose()
        {
            // Revert console intercepts globally
            Console.SetOut(_originalConsoleOut);
            Console.SetError(_originalConsoleError);

            CoreSettingsLoader.TestOverride = null;

            // Clean environment layout files using consistent BaseDirectory resolution
            try
            {
                string keyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AesKeyFileName);
                if (File.Exists(keyPath)) File.Delete(keyPath);

                string ivPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AesIvFileName);
                if (File.Exists(ivPath)) File.Delete(ivPath);

                string testDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);
                if (File.Exists(testDb))
                {
                    File.Delete(testDb);
                }
            }
            catch
            {
                // Suppress file deletion locks on cleanup
            }
        }
    }
}
