using CommandLine;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Options;
using Servy.CLI.Runtime;
using Servy.CLI.Validation;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.Validation;
using Servy.Infrastructure.Data;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public static class Program
    {
        /// <summary>
        /// The message written to standard error when the first Ctrl+C or Ctrl+Break starts a graceful cancellation.
        /// </summary>
        internal const string CancellingMessage = "Cancelling... press Ctrl+C again to force exit.";

        /// <summary>
        /// Parses command-line arguments, invokes the appropriate command handlers,
        /// and returns an exit code indicating the success or failure of the operation.
        /// </summary>
        /// <param name="args">An array of command-line arguments.</param>
        /// <returns>Returns 0 on success; non-zero on error.</returns>
        public static Task<int> Main(string[] args) => RunAsync(args, new CliRuntime());

        /// <summary>
        /// Parses command-line arguments, invokes the appropriate command handlers,
        /// and returns an exit code indicating the success or failure of the operation.
        /// </summary>
        /// <param name="args">An array of command-line arguments.</param>
        /// <param name="runtime">
        /// The machine-touching bootstrap steps: the SQLite version check, the database, the embedded binaries and the
        /// console probes. <see cref="Main"/> passes <see cref="CliRuntime"/>; tests pass a fake.
        /// </param>
        /// <returns>Returns 0 on success; non-zero on error.</returns>
        internal static async Task<int> RunAsync(string[] args, ICliRuntime runtime)
        {
            // Record the System variables as they are at start, so one changed later is picked up (#7393)
            SystemEnvironmentHelper.InitializeForApplication();

            using (var cts = new CancellationTokenSource())
            {
                // Hook Ctrl+C and Ctrl+Break
                ConsoleCancelEventHandler onCancel = (s, e) => HandleCancelKeyPress(cts, () => e.Cancel = true, Console.Error);
                Console.CancelKeyPress += onCancel;

                IAppDbContext dbContext = null;
                ProtectedKeyProvider protectedKeyProvider = null;
                SecureData secureData = null;
                try
                {
                    var verbs = GetVerbs();
                    var firstArg = args.Length > 0 ? args[0] : null;

                    if (args.Length == 0)
                    {
                        // Explicitly inject the default help verb only when no commands or arguments are provided
                        args = (new[] { GetVerbName<HelpOptions>() }).Concat(args).ToArray();
                    }
                    else if (!verbs.Any(v => string.Equals(v, firstArg, StringComparison.OrdinalIgnoreCase)) && !firstArg.StartsWith("-"))
                    {
                        // Detect a mistyped or unrecognized command that does not start with a global flag dash
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine($"Error: Unknown command '{firstArg}'. See '--help' for available options.");
                        Console.ResetColor();

                        return (int)CliExitCode.Error;
                    }

                    args[0] = args[0].ToLowerInvariant();

                    var config = ConfigurationManager.AppSettings;

                    var coreSettings = CoreSettingsLoader.Load();
                    var connectionString = coreSettings.ConnectionString;
                    var aesKeyFilePath = coreSettings.AESKeyFilePath;
                    var aesIVFilePath = coreSettings.AESIVFilePath;

                    LoggerConfigurator.ConfigureFromAppSettings(config, "Servy.CLI.log");
                    CoreSettingsLoader.WarnAboutIgnoredSettings(config, "Servy.CLI.exe.config");

                    if (!runtime.IsSqliteVersionSafe(out var detectedVersion))
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

                    // Initialize database shared dependencies
                    var appDbContext = new AppDbContext(connectionString);
                    dbContext = appDbContext;
                    var dapperExecutor = new DapperExecutor(appDbContext);
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
                        serviceRepository,
                        new ServyExePermissionsHardener(),
                        new NamedPipesService()
                        );

                    var processHelper = new ProcessHelper();
                    var serviceValidationRules = new ServiceValidationRules(processHelper);
                    var installValidator = new ServiceInstallValidator(serviceValidationRules);

                    var installCommand = new InstallServiceCommand(serviceManager, installValidator);
                    var startCommand = new StartServiceCommand(serviceManager);
                    var stopCommand = new StopServiceCommand(serviceManager);
                    var restartCommand = new RestartServiceCommand(serviceManager);
                    var uninstallCommand = new UninstallServiceCommand(serviceManager);
                    var serviceStatusCommand = new ServiceStatusCommand(serviceManager);
                    var exportCommand = new ExportServiceCommand(serviceRepository);
                    var showCommand = new ShowServiceCommand(serviceRepository, serviceManager);

                    var xmlServiceValidator = new XmlServiceValidator(serviceValidationRules);
                    var jsonServiceValidator = new JsonServiceValidator(serviceValidationRules);
                    var importCommand = new ImportServiceCommand(
                        serviceRepository,
                        serviceManager,
                        xmlServiceValidator,
                        jsonServiceValidator,
                        processHelper
                        );

                    // Helper to defer targeted runtime initialization until AFTER successful argument parsing
                    async Task<int> ExecuteWithRuntimeAsync(Func<Task<CommandResult>> commandAction, bool isQuiet, bool requireDatabase = true, bool requireBinaries = false)
                    {
                        async Task BootstrapAsync()
                        {
                            if (requireDatabase) runtime.EnsureDatabase(connectionString, aesKeyFilePath, aesIVFilePath, appDbContext);
                            if (requireBinaries) await runtime.EnsureServiceBinariesAsync(serviceRepository, controllerFactory, cts.Token);
                        }

                        if (!requireDatabase && !requireBinaries)
                        {
                            // Fast path: no initialization overhead needed
                        }
                        else if (isQuiet || !runtime.IsRealConsole())
                        {
                            await BootstrapAsync();
                        }
                        else
                        {
                            await runtime.RunWithLoadingAnimationAsync(BootstrapAsync);
                        }

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
                        ImportServiceOptions,
                        ShowServiceOptions
                        >(args)
                        .MapResult(
                             async (Options.InstallServiceOptions opts) => await ExecuteWithRuntimeAsync(() => installCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: true),
                            async (UninstallServiceOptions opts) => await ExecuteWithRuntimeAsync(() => uninstallCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: false),
                            async (StartServiceOptions opts) => await ExecuteWithRuntimeAsync(() => startCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: true),
                            async (StopServiceOptions opts) => await ExecuteWithRuntimeAsync(() => stopCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: false),
                            async (RestartServiceOptions opts) => await ExecuteWithRuntimeAsync(() => restartCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: true),
                            async (ServiceStatusOptions opts) => await ExecuteWithRuntimeAsync(() => Task.FromResult(serviceStatusCommand.Execute(opts, cts.Token)), opts.Quiet, requireDatabase: false, requireBinaries: false),
                            async (ExportServiceOptions opts) => await ExecuteWithRuntimeAsync(() => exportCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: false),
                            async (ImportServiceOptions opts) => await ExecuteWithRuntimeAsync(() => importCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: true),
                            async (ShowServiceOptions opts) => await ExecuteWithRuntimeAsync(() => showCommand.ExecuteAsync(opts, cts.Token), opts.Quiet, requireDatabase: true, requireBinaries: false),
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
                    Console.Error.WriteLine("\nOperation cancelled by user.");
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
                    Console.CancelKeyPress -= onCancel;
                    TryRun(() => secureData?.Dispose(), nameof(secureData));
                    TryRun(() => protectedKeyProvider?.Dispose(), nameof(protectedKeyProvider));
                    TryRun(() => dbContext?.Dispose(), nameof(dbContext));
                    TryRun(Logger.Shutdown, nameof(Logger.Shutdown));
                }
            }
        }

        /// <summary>
        /// Handles a Ctrl+C or Ctrl+Break press: the first press starts a graceful cancellation, a later press is left
        /// unhandled so the process terminates.
        /// </summary>
        /// <param name="cts">The source whose token cancels the running command.</param>
        /// <param name="markHandled">Marks the key press as handled, so the process keeps running while the command cancels.</param>
        /// <param name="error">The writer the cancellation notice is written to.</param>
        /// <returns><see langword="true"/> when this press started the graceful cancellation; otherwise <see langword="false"/>.</returns>
        internal static bool HandleCancelKeyPress(CancellationTokenSource cts, Action markHandled, TextWriter error)
        {
            if (!cts.IsCancellationRequested)
            {
                markHandled();          // first press: graceful cancellation
                cts.Cancel();
                error.WriteLine(CancellingMessage);
                return true;
            }

            // second press: leave e.Cancel = false -> process terminates
            return false;
        }

        /// <summary>
        /// Attempts to execute a cleanup action, suppressing any exceptions to ensure
        /// the teardown process proceeds.
        /// </summary>
        /// <param name="action">The delegate containing the cleanup logic to execute.</param>
        /// <param name="name">The descriptive name of the component for logging purposes.</param>
        internal static void TryRun(Action action, string name)
        {
            try { action(); }
            catch (Exception ex) { Logger.Warn($"{name} cleanup failed.", ex); }
        }

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
        public static bool IsRealConsole() => IsRealConsole(new ConsoleEnvironment());

        /// <summary>
        /// Performs the checks of <see cref="IsRealConsole()"/> against the given console and session properties.
        /// </summary>
        /// <param name="console">The console and session properties to probe.</param>
        /// <returns>
        /// <c>true</c> if a physical or emulated terminal is attached that supports interactive
        /// features (like cursor manipulation); otherwise <c>false</c>.
        /// </returns>
        internal static bool IsRealConsole(IConsoleEnvironment console)
        {
            // 1. Session 0 check (detects Services/SYSTEM account)
            if (!console.UserInteractive) return false;

            // 2. Standard redirection check (detects '>' or '|')
            if (console.IsOutputRedirected || console.IsErrorRedirected) return false;

            // 3. Win32 check: Is there actually a window handle?
            // This catches scenarios where a console is allocated but not visible or interactive.
            if (console.GetConsoleWindow() == IntPtr.Zero) return false;

            try
            {
                // 4. Property check: Accessing buffer properties on a redirected
                // handle will throw an IOException (Invalid Descriptor).
                return console.GetWindowHeight() > 0;
            }
            catch
            {
                return false;
            }
        }

    }
}
