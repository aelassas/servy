using CommandLine;
using Microsoft.Extensions.Configuration;
using Servy.CLI.Commands;
using Servy.CLI.Helpers;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
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
        /// Parses command-line arguments, invokes the appropriate command handlers,
        /// and returns an exit code indicating the success or failure of the operation.
        /// </summary>
        /// <param name="args">An array of command-line arguments.</param>
        /// <returns>Returns 0 on success; non-zero on error.</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var verbs = GetVerbs();
                if (args.Length == 0 || !verbs.Contains(args[0].ToLowerInvariant()))
                {
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
                builder.SetBasePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!)
                       .AddJsonFile("appsettings.cli.json", optional: true, reloadOnChange: true);
#endif
                var config = builder.Build();

                var connectionString = config.GetConnectionString("DefaultConnection") ?? AppConfig.DefaultConnectionString;
                var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                // Initialize shared dependencies
                var dbContext = new AppDbContext(connectionString);
                var dapperExecutor = new DapperExecutor(dbContext);
                var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                var secureData = new SecureData(protectedKeyProvider);
                var xmlSerializer = new XmlServiceSerializer();
                var serviceRepository = new ServiceRepository(dapperExecutor, secureData, xmlSerializer);

                var serviceManager = new ServiceManager(
                    name => new ServiceControllerWrapper(name),
                    new WindowsServiceApi(),
                    new Win32ErrorProvider(),
                    serviceRepository,
                    new WmiSearcher()
                    );

                var installValidator = new ServiceInstallValidator();

                var installCommand = new InstallServiceCommand(serviceManager, installValidator);
                var startCommand = new StartServiceCommand(serviceManager);
                var stopCommand = new StopServiceCommand(serviceManager);
                var restartCommand = new RestartServiceCommand(serviceManager);
                var uninstallCommand = new UninstallServiceCommand(serviceManager, serviceRepository);
                var serviceStatusCommand = new ServiceStatusCommand(serviceManager);
                var exportCommand = new ExportServiceCommand(serviceRepository);
                var importCommand = new ImportServiceCommand(serviceRepository, xmlSerializer, serviceManager);

                async Task Run()
                {
                    // Ensure db and security folders exist
                    AppFoldersHelper.EnsureFolders(connectionString, aesKeyFilePath, aesIVFilePath);

                    // Initialize the database
                    DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

                    var asm = Assembly.GetExecutingAssembly();

                    var resourceHelper = new ResourceHelper(serviceRepository);

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

                var exitCode = await Parser.Default.ParseArguments<
                        InstallServiceOptions,
                        UninstallServiceOptions,
                        StartServiceOptions,
                        StopServiceOptions,
                        ServiceStatusOptions,
                        RestartServiceOptions,
                        ExportServiceOptions,
                        ImportServiceOptions
                        >(args)
                    .MapResult(
                        async (InstallServiceOptions opts) => await PrintAndReturnAsync(installCommand.Execute(opts)),
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
            catch (Exception e)
            {
                Console.WriteLine($"An unexpected error occurred: {e.Message}");
                return 1;
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
