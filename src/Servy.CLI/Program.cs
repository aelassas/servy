using CommandLine;
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
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static Servy.CLI.Helpers.Helper;

namespace Servy.CLI
{
    /// <summary>
    /// The main entry point for the Servy command-line interface application.
    /// Responsible for parsing command-line arguments and executing
    /// corresponding service management commands.
    /// </summary>
    public class Program
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
        static async Task<int> Main(string[] args)
        {
            try
            {
                var verbs = GetVerbs();
                if (args.Length == 0 || !verbs.Contains(args[0].ToLowerInvariant()))
                {
                    args = (new[] { GetVerbName<HelpOptions>() }).Concat(args).ToArray();
                }

                if (args.Length > 0)
                {
                    args[0] = args[0].ToLowerInvariant();
                }

                var quiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                 a.Equals("-q", StringComparison.OrdinalIgnoreCase));

                var config = ConfigurationManager.AppSettings;

                var connectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
                var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
                var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

                // Initialize shared dependencies
                var dbContext = new AppDbContext(connectionString);
                var dapperExecutor = new DapperExecutor(dbContext);
                var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
                var securePassword = new SecurePassword(protectedKeyProvider);
                var xmlSerializer = new XmlServiceSerializer();
                var serviceRepository = new ServiceRepository(dapperExecutor, securePassword, xmlSerializer);

                var serviceManager = new ServiceManager(
                    name => new ServiceControllerWrapper(name),
                    new WindowsServiceApi(),
                    new Win32ErrorProvider(),
                    serviceRepository,
                    new WmiSearcher()
                    );

                var installValidator = new ServiceInstallValidator(serviceManager);

                var installCommand = new InstallServiceCommand(serviceManager, installValidator, serviceRepository);
                var startCommand = new StartServiceCommand(serviceManager);
                var stopCommand = new StopServiceCommand(serviceManager);
                var restartCommand = new RestartServiceCommand(serviceManager);
                var uninstallCommand = new UninstallServiceCommand(serviceManager, serviceRepository);
                var serviceStatusCommand = new ServiceStatusCommand(serviceManager);
                var exportCommand = new ExportServiceCommand(serviceRepository);
                var importCommand = new ImportServiceCommand(serviceRepository);

                void Run()
                {
                    // Ensure db and security folders exist
                    AppFoldersHelper.EnsureFolders(connectionString, aesKeyFilePath, aesIVFilePath);

                    // Initialize the database
                    DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

                    var asm = Assembly.GetExecutingAssembly();

                    // Copy service executable from embedded resources
                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceCLIFileName, "exe"))
                    {
                        Console.WriteLine($"Failed copying embedded resource: {AppConfig.ServyServiceCLIExe}");
                    }

                    // Copy Sysinternals from embedded resources
                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.HandleExeFileName, "exe", false))
                    {
                        Console.WriteLine($"Failed copying embedded resource: {AppConfig.HandleExe}");
                    }

#if DEBUG
                    // Copy debug symbols from embedded resources (only in debug builds)
                    if (!ResourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, AppConfig.ServyServiceCLIFileName, "pdb", false))
                    {
                        Console.WriteLine($"Failed copying embedded resource: {AppConfig.ServyServiceCLIFileName}.pdb");
                    }
#else
                    // Copy *.dll from embedded resources
                    var dllResources = new List<DllResource>
                    {
                        new DllResource{ FileNameWithoutExtension = "Servy.Core"},
                        new DllResource{ FileNameWithoutExtension = "Dapper"},
                        new DllResource{ FileNameWithoutExtension = "Microsoft.Bcl.AsyncInterfaces"},
                        new DllResource{ FileNameWithoutExtension = "Newtonsoft.Json"},
                        new DllResource{ FileNameWithoutExtension = "Servy.Infrastructure"},
                        new DllResource{ FileNameWithoutExtension = "System.Data.SQLite"},
                        new DllResource{ FileNameWithoutExtension = "System.Runtime.CompilerServices.Unsafe"},
                        new DllResource{ FileNameWithoutExtension = "System.Threading.Tasks.Extensions"},
                        new DllResource{ FileNameWithoutExtension = "SQLite.Interop", Subfolder = "x64"},
                        new DllResource{ FileNameWithoutExtension = "SQLite.Interop", Subfolder = "x86"},
                    };

                    CopyDllResources(asm, dllResources);
#endif
                }

                if (quiet)
                {
                    Run();
                }
                else
                {
                    await ConsoleHelper.RunWithLoadingAnimation(() =>
                    {
                        Run();
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
                        async (StartServiceOptions opts) => await PrintAndReturnAsync(Task.FromResult(startCommand.Execute(opts))),
                        async (StopServiceOptions opts) => await PrintAndReturnAsync(Task.FromResult(stopCommand.Execute(opts))),
                        async (RestartServiceOptions opts) => await PrintAndReturnAsync(Task.FromResult(restartCommand.Execute(opts))),
                        async (ServiceStatusOptions opts) => await PrintAndReturnAsync(Task.FromResult(serviceStatusCommand.Execute(opts))),
                        async (ExportServiceOptions opts) => await PrintAndReturnAsync(exportCommand.Execute(opts)),
                        async (ImportServiceOptions opts) => await PrintAndReturnAsync(importCommand.Execute(opts)),
                        errs => Task.FromResult(1) // Wrap synchronous error result in Task
                    );

                return exitCode;
            }
            catch (Exception e)
            {
                Console.WriteLine($"An unexpected error occurred: {e.Message}");
                return 1;
            }
        }

        #region Helpers

        /// <summary>
        /// Attempts to copy the specified embedded DLL resources from the given <see cref="Assembly"/>.  
        /// Displays a message box if the operation fails.
        /// </summary>
        /// <param name="asm">
        /// The <see cref="Assembly"/> that contains the embedded DLL resources.
        /// </param>
        /// <param name="dllResources">
        /// A list of <see cref="DllResource"/> objects representing the DLLs to copy.
        /// </param>
        /// <param name="stopServices">
        /// Indicates whether running Servy services should be stopped before copying and restarted afterward.  
        /// Default is <c>true</c>.
        /// </param>
        /// <remarks>
        /// This method wraps <see cref="ResourceHelper.CopyDLLResources"/> and provides  
        /// user-facing error reporting via a console if copying fails.
        /// </remarks>
        private static void CopyDllResources(Assembly asm, List<DllResource> dllResources, bool stopServices = true)
        {
            if (!ResourceHelper.CopyDLLResources(asm, ResourcesNamespace, dllResources, stopServices))
            {
                Console.WriteLine($"Failed copying embedded DLL resources.");
            }
        }

        #endregion
    }
}
