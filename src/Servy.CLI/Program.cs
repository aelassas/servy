using CommandLine;
using Microsoft.Extensions.Configuration;
using Servy.CLI.Commands;
using Servy.CLI.Helpers;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Validation;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.Validation;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using static Servy.CLI.Helpers.Helper;

namespace Servy.CLI
{
    /// <summary>
    /// Defines exit codes for the Servy command-line interface.
    /// </summary>
    public enum CliExitCode
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// A generic error occurred during execution.
        /// </summary>
        Error = 1,

        /// <summary>
        /// The environment is incompatible with the application (e.g., vulnerable SQLite version).
        /// </summary>
        IncompatibleEnvironment = 2
    }

    /// <summary>
    /// The main entry point for the Servy command-line interface application.
    /// Responsible for parsing command-line arguments and executing
    /// corresponding service management commands.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        private const string ResourcesNamespace = "Servy.CLI.Resources";

        /// <summary>
        /// Parses command-line arguments, invokes the appropriate command handlers,
        /// and returns an exit code indicating the success or failure of the operation.
        /// </summary>
        /// <param name="args">An array of command-line arguments.</param>
        /// <returns>Returns 0 on success; non-zero on error.</returns>
        public static async Task<int> Main(string[] args)
        {
            using (var cts = new CancellationTokenSource())
            {
                // Hook Ctrl+C and Ctrl+Break
                Console.CancelKeyPress += (s, e) =>
                {
                    if (!cts.IsCancellationRequested)
                    {
                        e.Cancel = true;          // first press: graceful cancellation
                        cts.Cancel();
                        Console.WriteLine("Cancelling... press Ctrl+C again to force exit.");
                    }
                    // second press: leave e.Cancel = false -> process terminates
                };

                IAppDbContext? dbContext = null;
                ProtectedKeyProvider? protectedKeyProvider = null;
                SecureData? secureData = null;
                try
                {
                    var verbs = GetVerbs();
                    var firstArg = args.Length > 0 ? args[0] : null;

                    if (args.Length == 0)
                    {
                        // Explicitly inject the default help verb only when no commands or arguments are provided
                        args = (new[] { GetVerbName<HelpOptions>() }).Concat(args).ToArray();
                    }
                    else if (!verbs.Any(v => string.Equals(v, firstArg, StringComparison.OrdinalIgnoreCase)) && !(firstArg?.StartsWith("-") ?? false))
                    {
                        // Detect a mistyped or unrecognized command that does not start with a global flag dash
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine($"Error: Unknown command '{firstArg}'. See '--help' for available options.");
                        Console.ResetColor();

                        return (int)CliExitCode.Error;
                    }

                    args[0] = args[0].ToLowerInvariant();

                    var quiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) || a.Equals("-q", StringComparison.OrdinalIgnoreCase))
                        || !IsRealConsole();

                    // Load configuration from appsettings.json
                    var builder = new ConfigurationBuilder();
#if DEBUG
                    builder.AddJsonFile("appsettings.cli.json", optional: true, reloadOnChange: false);
#else
                    var baseDirectory = AppFoldersHelper.GetAppDirectory();
                    builder.SetBasePath(baseDirectory)
                           .AddJsonFile("appsettings.cli.json", optional: true, reloadOnChange: false);
#endif
                    var config = builder.Build();

                    var connectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                    var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                    var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                    LoggerConfigurator.ConfigureFromAppSettings(config, "Servy.CLI.log");

                    if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
                    {
                        Logger.Error($"[FATAL] Vulnerable SQLite version detected: {detectedVersion}. " +
                                     $"Minimum required: {AppConfig.MinRequiredSqliteVersion} (CVE-2025-6965 mitigation).");

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine($"[CRITICAL] Vulnerable SQLite version detected: {detectedVersion}");
                        Console.Error.WriteLine($"This version of Servy requires SQLite {AppConfig.MinRequiredSqliteVersion}+.");
                        Console.ResetColor();

                        // Exit with a CLI-specific sentinel instead of a SCM Win32 error code
                        return (int)CliExitCode.IncompatibleEnvironment;
                    }

                    // Initialize shared dependencies
                    dbContext = new AppDbContext(connectionString);
                    var dapperExecutor = new DapperExecutor(dbContext);
                    protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                    secureData = new SecureData(protectedKeyProvider);
                    var xmlSerializer = new XmlServiceSerializer();
                    var jsonSerializer = new JsonServiceSerializer();
                    var serviceRepository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer, jsonSerializer);

                    Func<string, IServiceControllerWrapper> controllerFactory = name => new ServiceControllerWrapper(name);
                    var serviceManager = new ServiceManager(
                        controllerFactory,
                        new ServiceControllerProvider(controllerFactory),
                        new WindowsServiceApi(),
                        new Win32ErrorProvider(),
                        serviceRepository
                        );

                    var processHelper = new ProcessHelper();
                    var processKiller = new ProcessKiller();
                    var serviceValidationRules = new ServiceValidationRules(processHelper);
                    var installValidator = new ServiceInstallValidator(serviceValidationRules);

                    var installCommand = new InstallServiceCommand(serviceManager, installValidator);
                    var startCommand = new StartServiceCommand(serviceManager);
                    var stopCommand = new StopServiceCommand(serviceManager);
                    var restartCommand = new RestartServiceCommand(serviceManager);
                    var uninstallCommand = new UninstallServiceCommand(serviceManager, serviceRepository);
                    var serviceStatusCommand = new ServiceStatusCommand(serviceManager);
                    var exportCommand = new ExportServiceCommand(serviceRepository);

                    var xmlServiceValidator = new XmlServiceValidator(serviceValidationRules);
                    var jsonServiceValidator = new JsonServiceValidator(serviceValidationRules);
                    var importCommand = new ImportServiceCommand(
                        serviceRepository,
                        xmlSerializer,
                        jsonSerializer,
                        serviceManager,
                        xmlServiceValidator,
                        jsonServiceValidator,
                        processHelper
                        );

                    async Task Run()
                    {
                        // Ensure db and security folders exist
                        AppFoldersHelper.EnsureFolders(connectionString, aesKeyFilePath, aesIVFilePath);

                        // Ensure event source exists
                        Core.Helpers.Helper.EnsureEventSourceExists();

                        // Initialize the database
                        DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

                        var asm = Assembly.GetExecutingAssembly();

                        var sh = new ServiceHelper(serviceRepository);
                        var resourceHelper = new ResourceHelper(sh, processKiller);

                        // Copy service executable from embedded resources
                        if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceCLIFileName, "exe", true, true))
                        {
                            throw new InvalidOperationException($"Failed to extract embedded resource '{AppConfig.ServyServiceCLIExe}'. " +
                                "CLI cannot start safely - see file log for details.");
                        }

                        // Copy Sysinternals from embedded resources
                        var handleExeFileName = RuntimeInformation.OSArchitecture == Architecture.Arm64
                            ? AppConfig.HandleExeARM64FileName
                            : AppConfig.HandleExeX64FileName;
                        if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, handleExeFileName, "exe", false))
                        {
                            Logger.Warn($"Failed copying embedded resource: {handleExeFileName}; process-tree handle features may be degraded.");
                        }

#if DEBUG
                        // Copy debug symbols from embedded resources (only in debug builds)
                        if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceCLIFileName, "pdb", false))
                        {
                            Logger.Warn($"Failed copying embedded resource: {AppConfig.ServyServiceCLIFileName}.pdb");
                        }
#endif
                    }

                    // Helper to defer runtime execution (DB Init, Extraction) until AFTER successful argument parsing
                    async Task<int> ExecuteWithRuntimeAsync(Func<Task<CommandResult>> commandAction)
                    {
                        if (quiet) await Run();
                        else await ConsoleHelper.RunWithLoadingAnimation(async () => { await Run(); });

                        return await PrintAndReturnAsync(commandAction());
                    }

                    var parser = new Parser(with =>
                    {
                        with.HelpWriter = Console.Out;
                    });

                    var exitCode = await parser.ParseArguments<
                        Options.InstallServiceOptions,
                        UninstallServiceOptions,
                        StartServiceOptions,
                        StopServiceOptions,
                        ServiceStatusOptions,
                        RestartServiceOptions,
                        ExportServiceOptions,
                        ImportServiceOptions
                        >(args)
                        .MapResult(
                            async (Options.InstallServiceOptions opts) => await ExecuteWithRuntimeAsync(() => installCommand.ExecuteAsync(opts, cts.Token)),
                            async (UninstallServiceOptions opts) => await ExecuteWithRuntimeAsync(() => uninstallCommand.ExecuteAsync(opts, cts.Token)),
                            async (StartServiceOptions opts) => await ExecuteWithRuntimeAsync(() => startCommand.ExecuteAsync(opts, cts.Token)),
                            async (StopServiceOptions opts) => await ExecuteWithRuntimeAsync(() => stopCommand.ExecuteAsync(opts, cts.Token)),
                            async (RestartServiceOptions opts) => await ExecuteWithRuntimeAsync(() => restartCommand.ExecuteAsync(opts, cts.Token)),
                            async (ServiceStatusOptions opts) => await ExecuteWithRuntimeAsync(() => Task.FromResult(serviceStatusCommand.Execute(opts, cts.Token))),
                            async (ExportServiceOptions opts) => await ExecuteWithRuntimeAsync(() => exportCommand.ExecuteAsync(opts, cts.Token)),
                            async (ImportServiceOptions opts) => await ExecuteWithRuntimeAsync(() => importCommand.ExecuteAsync(opts, cts.Token)),
                            // Wrap synchronous error result in Task
                            errs =>
                            {
                                if (errs.IsHelp() || errs.IsVersion())
                                    return Task.FromResult((int)CliExitCode.Success);

                                return Task.FromResult((int)CliExitCode.Error);
                            }
                        );

                    return exitCode;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nOperation cancelled by user.");
                    return (int)CliExitCode.Error;
                }
                catch (Exception ex)
                {
                    Logger.Error("An unexpected error occurred in the main execution flow.", ex);
                    Console.Error.WriteLine($"An unexpected error occurred: {ex.Message}");
                    return (int)CliExitCode.Error;
                }
                finally
                {
                    TryRun(() => secureData?.Dispose(), nameof(secureData));
                    TryRun(() => protectedKeyProvider?.Dispose(), nameof(protectedKeyProvider));
                    TryRun(() => dbContext?.Dispose(), nameof(dbContext));
                    TryRun(Logger.Shutdown, nameof(Logger.Shutdown));
                }
            }
        }

        /// <summary>
        /// Attempts to execute a cleanup action, suppressing any exceptions to ensure 
        /// the teardown process proceeds.
        /// </summary>
        /// <param name="action">The delegate containing the cleanup logic to execute.</param>
        /// <param name="name">The descriptive name of the component for logging purposes.</param>
        private static void TryRun(Action action, string name)
        {
            try { action(); }
            catch (Exception ex) { Logger.Warn($"{name} cleanup failed.", ex); }
        }

        /// <summary>
        /// Retrieves the window handle used by the console associated with the calling process.
        /// </summary>
        /// <returns>
        /// A handle to the window used by the console, or <see cref="IntPtr.Zero"/> if there is no such associated window.
        /// </returns>
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        /// <summary>
        /// Performs a multi-layered check to determine if the current process is running in a real, 
        /// interactive console window.
        /// </summary>
        /// <returns>
        /// <c>true</c> if a physical or emulated terminal is attached that supports interactive 
        /// features (like cursor manipulation); <c>false</c> if the output is redirected, 
        /// running in Session 0 (SYSTEM account), or headless.
        /// </returns>
        /// <remarks>
        /// This method validates the environment using four distinct checks:
        /// <list type="number">
        /// <item>
        /// <description><b>Session 0 Check:</b> Uses <see cref="Environment.UserInteractive"/> to see if the process can interact with a desktop.</description>
        /// </item>
        /// <item>
        /// <description><b>Redirection Check:</b> Checks if standard output or error has been piped to a file or another process.</description>
        /// </item>
        /// <item>
        /// <description><b>Win32 Handle Check:</b> Verifies a window handle exists via <c>GetConsoleWindow</c> to catch "ghost" consoles.</description>
        /// </item>
        /// <item>
        /// <description><b>Buffer Access Check:</b> Attempts to read <see cref="Console.WindowHeight"/> to ensure the console buffer is actually reachable.</description>
        /// </item>
        /// </list>
        /// </remarks>
        public static bool IsRealConsole()
        {
            // 1. Session 0 check (detects Services/SYSTEM account)
            if (!Environment.UserInteractive) return false;

            // 2. Standard redirection check (detects '>' or '|')
            if (Console.IsOutputRedirected || Console.IsErrorRedirected) return false;

            // 3. Win32 check: Is there actually a window handle?
            if (GetConsoleWindow() == IntPtr.Zero) return false;

            try
            {
                // 4. Property check: Accessing buffer properties on a redirected 
                // handle will throw an IOException (Invalid Descriptor).
                return Console.WindowHeight > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}