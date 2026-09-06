using Servy.Testing;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests
{
    [Collection("SequentialConsoleTests")]
    public class ProgramTests : IDisposable
    {
        private const string AppSettingsFileName = "appsettings.cli.json";
        private const string AesKeyFileName = "test_aes.key";
        private const string AesIvFileName = "test_aes.iv";
        private const string DatabaseFileName = "Test_Servy.db";

        // Resolves onto the appsettings.cli.json that Servy.CLI.csproj copies to the output
        // directory, not onto a file this suite owns, so its previous contents are saved and
        // restored the same way the Console streams are.
        private readonly string _cliConfigPath;
        private readonly string _originalCliConfigJson;
        private readonly TextWriter _originalConsoleOut;
        private readonly TextWriter _originalConsoleError;

        public ProgramTests()
        {
            // Arrange
            // Establish isolated files environment for execution runs
            _cliConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettingsFileName);
            _originalCliConfigJson = File.Exists(_cliConfigPath) ? File.ReadAllText(_cliConfigPath) : null;

            _originalConsoleOut = Console.Out;
            _originalConsoleError = Console.Error;

            // Generate a valid mock configuration structure to bypass missing setting errors
            string fallbackDatabaseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);
            string testConnection = string.Format("Data Source={0};Version=3;", fallbackDatabaseFile);

            string mockConfigJson = "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"" + testConnection.Replace("\\", "\\\\") + "\"\r\n" +
                "  },\r\n" +
                "  \"Security\": {\r\n" +
                "    \"AESKeyFilePath\": \"" + AesKeyFileName + "\",\r\n" +
                "    \"AESIVFilePath\": \"" + AesIvFileName + "\"\r\n" +
                "  }\r\n" +
                "}";

            File.WriteAllText(_cliConfigPath, mockConfigJson);
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
            // The command fails because the service is not found in the database/SCM, returning Error (1)
            Assert.Equal((int)CliExitCode.Error, result.Result);

            // Verify that no loading animation frames or status text fragments were written to stdout
            Assert.True(string.IsNullOrEmpty(result.StdOut), "Console output should be completely suppressed when the --quiet flag is supplied.");

            // Stderr is the other half of the contract and the channel this scenario actually uses:
            // --quiet bypasses the loading animation (Program.cs:237) but does not silence failure
            // reporting, which Helper.PrintAndReturn writes to Console.Error. Assert it here so a
            // regression that routes the failure message to stdout, or drops it entirely, is caught.
            Assert.False(string.IsNullOrWhiteSpace(result.StdErr), "The failure message must still reach stderr; --quiet suppresses the progress animation only.");
        }

        #endregion

        public void Dispose()
        {
            // Revert console intercepts globally
            Console.SetOut(_originalConsoleOut);
            Console.SetError(_originalConsoleError);

            // Clean environment layout files using consistent BaseDirectory resolution
            try
            {
                // appsettings.cli.json is a build output, so put back what was found rather
                // than leaving the output directory without it: a --no-build re-run would
                // otherwise fall back to the optional-config path silently.
                if (_originalCliConfigJson != null)
                {
                    File.WriteAllText(_cliConfigPath, _originalCliConfigJson);
                }
                else if (File.Exists(_cliConfigPath))
                {
                    File.Delete(_cliConfigPath);
                }

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
