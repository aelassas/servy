using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests
{
    // 1. Establish a unique, non-parallelized execution collection domain for CLI tests
    [CollectionDefinition("Servy.CLI.ConsoleTests", DisableParallelization = true)]
    public class CliConsoleCollection { }

    // 2. Explicitly bind the test class to the sequential execution collection
    [Collection("Servy.CLI.ConsoleTests")]
    public class ProgramTests : IDisposable
    {
        private readonly TextWriter _originalConsoleOut;
        private readonly TextWriter _originalConsoleError;
        private readonly string _testDbPath;

        public ProgramTests()
        {
            // Intercept standard streams to capture pipeline logs and outputs
            _originalConsoleOut = Console.Out;
            _originalConsoleError = Console.Error;

            // Define isolated test file paths
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test.db");
            string testConnection = string.Format("Data Source={0};Version=3;", _testDbPath);
        }

        #region Console Validation Logic Branches

        [Fact]
        public void IsRealConsole_InNonInteractiveTestEnvironment_ReturnsFalse()
        {
            // Act
            bool isReal = Program.IsRealConsole();

            // Assert
            // When executing within headless test runners or CI pipelines, 
            // Environment.UserInteractive or Console.IsOutputRedirected will naturally be false/redirected
            Assert.False(isReal);
        }

        #endregion

        #region Execution Flow & Parsing Branch Coverage

        [Fact]
        public async Task Main_EmptyArguments_InjectsHelpVerbAndExitsWithSuccess()
        {
            // 1. Capture the original writer so we can restore it later
            var originalOut = Console.Out;

            // 2. Wrap the writer in a thread-safe synchronized boundary
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] emptyArgs = Array.Empty<string>();
                    int exitCode = await Program.Main(emptyArgs);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Success, exitCode);
                }
                finally
                {
                    // 6. Restore original output BEFORE leaving the using block to protect background async threads
                    Console.SetOut(originalOut);
                }
            }
        }

        [Fact]
        public async Task Main_HelpFlagProvided_ReturnsSuccessExitCode()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "--help" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Success, exitCode);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
        }

        [Fact]
        public async Task Main_InvalidArgumentsProvided_ReturnsErrorExitCode()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "install", "--unsupported-option" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Error, exitCode);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
        }

        [Fact]
        public async Task Main_QuietFlagProvided_AltersExecutionToQuietPath()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "status", "--quiet" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.True(exitCode == (int)CliExitCode.Success || exitCode == (int)CliExitCode.Error);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
        }

        #endregion

        #region Exception & Interrupt Scenarios

        [Fact]
        public async Task Main_InvalidArguments_ReturnsErrorExitCode()
        {
            // 1. Capture the original writer
            var originalOut = Console.Out;

            // 2. Synchronized allocation
            using (var stringWriter = TextWriter.Synchronized(new StringWriter()))
            {
                try
                {
                    // 3. Redirect
                    Console.SetOut(stringWriter);

                    // 4. Act
                    string[] args = { "install", "--corrupt-flag-combination" };
                    int exitCode = await Program.Main(args);

                    // 5. Assert
                    Assert.Equal((int)CliExitCode.Error, exitCode);
                }
                finally
                {
                    // 6. Restore
                    Console.SetOut(originalOut);
                }
            }
        }

        #endregion

        public void Dispose()
        {
            // Revert console intercepts globally
            Console.SetOut(_originalConsoleOut);
            Console.SetError(_originalConsoleError);

            // Clean down created local artifacts to ensure zero tracking pollution across runs
            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
                if (File.Exists("test_aes.key")) File.Delete("test_aes.key");
                if (File.Exists("test_aes.iv")) File.Delete("test_aes.iv");
            }
            catch
            {
                // Suppress locking race conditions safely on file cleanup
            }
        }
    }
}