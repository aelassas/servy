using CommandLine;
using Microsoft.Extensions.Configuration;
using Servy.CLI.Commands;
using Servy.CLI.Helpers;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Core.Validators;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System.Reflection;
using System.Runtime.InteropServices;
using static Servy.CLI.Helpers.Helper;
#if !DEBUG
using System.Diagnostics;
#endif

namespace Servy.CLI
{
    /// <summary>
    /// The main entry point for the Servy command-line interface application.
    /// Responsible for parsing command-line arguments and executing
    /// corresponding service management commands.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        private const string ResourcesNamespace = "Servy.CLI.Resources";

        /// <summary>
        /// The singleton instance of <see cref="SecureData"/> used for cryptographic operations.
        /// </summary>
        /// <remarks>
        /// This field holds sensitive AES and HMAC key material in memory. 
        /// It must be explicitly disposed of during application shutdown to trigger 
        /// strict memory-zeroing protocols via <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/>.
        /// </remarks>
        private static SecureData? _secureData;

        /// <summary>
        /// Parses command-line arguments, invokes the appropriate command handlers,
        /// and returns an exit code indicating the success or failure of the operation.
        /// </summary>
        /// <param name="args">An array of command-line arguments.</param>
        /// <returns>Returns 0 on success; non-zero on error.</returns>
        public static async Task<int> Main(string[] args)
        {
            Logger.Initialize("Servy.CLI.log");

            if (!DatabaseValidator.IsSqliteVersionSafe(out var detectedVersion))
            {
                Logger.Error($"[FATAL] Vulnerable SQLite version detected: {detectedVersion}. " +
                             $"Minimum required: {AppConfig.MinRequiredSqliteVersion} (CVE-2025-6965 mitigation).");

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL] Vulnerable SQLite version detected: {detectedVersion}");
                Console.WriteLine($"This version of Servy requires SQLite {AppConfig.MinRequiredSqliteVersion}+.");
                Console.ResetColor();

                // Exit with a specific error code to inform the OS/Admin of a security incompatibility
                Environment.Exit(1064);
            }

            try
            {
                var verbs = GetVerbs();
                var firstArg = args.Length > 0 ? args[0] : null;

                if (args.Length == 0 ||
                    (!verbs.Any(v => string.Equals(v, firstArg, StringComparison.OrdinalIgnoreCase)) && !(firstArg?.StartsWith("-") ?? false)))
                {
                    // Only inject the default verb if the user didn't provide a recognized verb 
                    // AND didn't provide a global flag like --version or --help
                    args = (new[] { GetVerbName<HelpOptions>() }).Concat(args).ToArray();
                }

                args[0] = args[0].ToLowerInvariant();

                var quiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) || a.Equals("-q", StringComparison.OrdinalIgnoreCase))
                    || !IsRealConsole();

                // Ensure event source exists
                Core.Helpers.Helper.EnsureEventSourceExists();

                // Load configuration from appsettings.json
                var builder = new ConfigurationBuilder();
#if DEBUG
                builder.AddJsonFile("appsettings.cli.json", optional: true, reloadOnChange: true);
#else
                var baseDirectory = AppFoldersHelper.GetAppDirectory();
                builder.SetBasePath(baseDirectory)
                       .AddJsonFile("appsettings.cli.json", optional: true, reloadOnChange: true);
#endif
                var config = builder.Build();

                var connectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                if (!Enum.TryParse<LogLevel>(config["LogLevel"], true, out var logLevel))
                {
                    logLevel = LogLevel.Info;
                }
                Logger.SetLogLevel(logLevel);

                if (!Enum.TryParse<DateRotationType>(config["LogRollingInterval"], true, out var dateRotationType))
                {
                    dateRotationType = DateRotationType.None;
                }
                Logger.SetDateRotationType(dateRotationType);

                if (int.TryParse(config["LogRotationSizeMB"], out var size) && size > 0)
                {
                    Logger.SetLogRotationSize(size);
                }
                else
                {
                    Logger.SetLogRotationSize(Logger.DefaultLogRotationSizeMB);
                }

                if (int.TryParse(config["MaxBackupLogFiles"], out var maxBackupFiles) && maxBackupFiles >= 0) Logger.SetMaxBackupLogFiles(maxBackupFiles);
                else Logger.SetMaxBackupLogFiles(Logger.DefaultMaxBackupLogFiles);

                string rawUseLocalTimeForRotationConfig = config["UseLocalTimeForRotation"] ?? AppConfig.DefaultUseLocalTimeForRotation.ToString();

                if (!bool.TryParse(rawUseLocalTimeForRotationConfig, out bool useLocalTimeForRotation))
                {
                    useLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation;
                }
                Logger.SetUseLocalTimeForRotation(useLocalTimeForRotation);

                // Initialize shared dependencies
                var dbContext = new AppDbContext(connectionString);
                var dapperExecutor = new DapperExecutor(dbContext);
                var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                _secureData = new SecureData(protectedKeyProvider);
                var xmlSerializer = new XmlServiceSerializer();
                var jsonSerializer = new JsonServiceSerializer();
                var serviceRepository = new ServiceRepository(dapperExecutor, _secureData, xmlSerializer, jsonSerializer);

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

                var xmlServiceValidator = new XmlServiceValidator(processHelper, serviceValidationRules);
                var jsonServiceValidator = new JsonServiceValidator(processHelper, serviceValidationRules);
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

                    // Initialize the database
                    DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

                    var asm = Assembly.GetExecutingAssembly();

                    var resourceHelper = new ResourceHelper(serviceRepository, processKiller);

                    // Copy service executable from embedded resources
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceCLIFileName, "exe", true, true))
                    {
                        Console.WriteLine($"Failed copying embedded resource: {AppConfig.ServyServiceCLIExe}");
                    }

                    // Copy Sysinternals from embedded resources
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        Console.WriteLine($"Failed copying embedded resource: {AppConfig.HandleExe}");
                    }

#if DEBUG
                    // Copy debug symbols from embedded resources (only in debug builds)
                    if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceCLIFileName, "pdb", false))
                    {
                        Console.WriteLine($"Failed copying embedded resource: {AppConfig.ServyServiceCLIFileName}.pdb");
                    }
#endif
                }

                if (quiet)
                {
                    await Run();
                }
                else
                {
                    await ConsoleHelper.RunWithLoadingAnimation(async () =>
                    {
                        await Run();
                    });
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
                        async (Options.InstallServiceOptions opts) => await PrintAndReturnAsync(installCommand.Execute(opts)),
                        async (UninstallServiceOptions opts) => await PrintAndReturnAsync(uninstallCommand.Execute(opts)),
                        async (StartServiceOptions opts) => await PrintAndReturnAsync(startCommand.Execute(opts)),
                        async (StopServiceOptions opts) => await PrintAndReturnAsync(stopCommand.Execute(opts)),
                        async (RestartServiceOptions opts) => await PrintAndReturnAsync(restartCommand.Execute(opts)),
                        async (ServiceStatusOptions opts) => await PrintAndReturnAsync(Task.FromResult(serviceStatusCommand.Execute(opts))),
                        async (ExportServiceOptions opts) => await PrintAndReturnAsync(exportCommand.Execute(opts)),
                        async (ImportServiceOptions opts) => await PrintAndReturnAsync(importCommand.Execute(opts)),
                        // Wrap synchronous error result in Task
                        errs =>
                        {
                            if (errs.IsHelp())
                                return Task.FromResult(0);

                            if (errs.IsVersion())
                                return Task.FromResult(0);

                            return Task.FromResult(1);
                        }
                    );

                return exitCode;
            }
            catch (Exception ex)
            {
                Logger.Error("An unexpected error occurred in the main execution flow.", ex);
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return 1;
            }
            finally
            {
                _secureData?.Dispose();
                Logger.Shutdown();
            }
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
            // This catches scenarios where a console is allocated but not visible or interactive.
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
