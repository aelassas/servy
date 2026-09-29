using Servy.Core.Config;
using Servy.Testing;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

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
            // through the test-only override instead of the application configuration.
            string fallbackDatabaseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);
            string testConnection = string.Format("Data Source={0};Version=3;", fallbackDatabaseFile);

            CoreSettingsLoader.TestOverride = new CoreSettings(testConnection, AesKeyFileName, AesIvFileName);
        }

        #region Console Validation Logic Branches

        [Fact]
        public void IsRealConsole_WhenHostIsNonInteractiveOrRedirected_ShortCircuitsToFalse()
        {
            if (Environment.UserInteractive
                  && !Console.IsOutputRedirected
                  && !Console.IsErrorRedirected)
            {
                return;
            }

            // Arrange
            // The early-return guard above is the exact complement of the first two guards of
            // IsRealConsole, so the body only ever runs in a state that short-circuits the
            // method. Pin that precondition explicitly rather than leaving it implicit in
            // the guard expression: this test covers the short-circuit only, and the Win32
            // half of the method (GetConsoleWindow and the Console.WindowHeight probe)
            // stays out of reach without a redirection seam on Program.
            bool shortCircuitStateHolds = !Environment.UserInteractive
                || Console.IsOutputRedirected
                || Console.IsErrorRedirected;

            // Act
            bool isReal = Program.IsRealConsole();

            // Assert
            Assert.True(shortCircuitStateHolds,
                "The early-return guard must leave only host states that short-circuit IsRealConsole.");
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
