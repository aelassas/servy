using CommandLine;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.Services;
using static Servy.CLI.Helpers.Helper;

namespace Servy.CLI
{
    /// <summary>
    /// The main entry point for the Servy command-line interface application.
    /// Responsible for parsing command-line arguments and executing
    /// corresponding service management commands.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Servy Service executable filename.
        /// </summary>
        public const string ServyServiceExeFileName = "Servy.Service.CLI";

        /// <summary>
        /// Parses command-line arguments, invokes the appropriate command handlers,
        /// and returns an exit code indicating the success or failure of the operation.
        /// </summary>
        /// <param name="args">An array of command-line arguments.</param>
        /// <returns>Returns 0 on success; non-zero on error.</returns>
        static int Main(string[] args)
        {
            try
            {
                var serviceManager = new ServiceManager(
                            name => new ServiceControllerWrapper(name),
                            new WindowsServiceApi(),
                            new Win32ErrorProvider()
                            );
                var installValidator = new ServiceInstallValidator();

                var installCommand = new InstallServiceCommand(serviceManager, installValidator);
                var startCommand = new StartServiceCommand(serviceManager);
                var stopCommand = new StopServiceCommand(serviceManager);
                var restartCommand = new RestartServiceCommand(serviceManager);
                var uninstallCommand = new UninstallServiceCommand(serviceManager);

                CopyEmbeddedResource(ServyServiceExeFileName);

                var exiCode = Parser.Default.ParseArguments<
                    InstallServiceOptions,
                    UninstallServiceOptions,
                    StartServiceOptions,
                    StopServiceOptions,
                    RestartServiceOptions>(args)
                       .MapResult(
                        (InstallServiceOptions opts) => PrintAndReturn(installCommand.Execute(opts)),
                        (UninstallServiceOptions opts) => PrintAndReturn(uninstallCommand.Execute(opts)),
                        (StartServiceOptions opts) => PrintAndReturn(startCommand.Execute(opts)),
                        (StopServiceOptions opts) => PrintAndReturn(stopCommand.Execute(opts)),
                        (RestartServiceOptions opts) => PrintAndReturn(restartCommand.Execute(opts)),
                        errs => 1);

                return exiCode;
            }
            catch (Exception e)
            {
                Console.WriteLine("An unexpected error occured:", e.Message);
                return 1;
            }
        }
    }
}
