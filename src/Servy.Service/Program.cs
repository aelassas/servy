using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using Servy.Service.Native;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Servy.Service
{
    /// <summary>
    /// Contains the application entry point for the Servy Windows service.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The namespace in the assembly where the embedded service resources are located.
        /// </summary>
        private const string ResourcesNamespace = "Servy.Service.Resources";

        /// <summary>
        /// The base file name (without extension) of the embedded Servy Restarter executable.
        /// </summary>
        private const string ServyRestarterExeFileName = "Servy.Restarter";

        /// <summary>
        /// Main entry point of the Servy Windows service application.
        /// Extracts required embedded resources and starts the service host.
        /// </summary>
        static async Task Main()
        {
            _ = NativeMethods.FreeConsole();
            _ = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);

            // Copy service executable from embedded resources
            var asm = Assembly.GetExecutingAssembly();
            string eventSource = AppConfig.ServiceNameEventSource;

            // Ensure event source exists
            Helper.EnsureEventSourceExists();

            var config = ConfigurationManager.AppSettings;

            var connectionString = config["DefaultConnection"] ?? AppConfig.DefaultConnectionString;
            var aesKeyFilePath = config["Security:AESKeyFilePath"] ?? AppConfig.DefaultAESKeyPath;
            var aesIVFilePath = config["Security:AESIVFilePath"] ?? AppConfig.DefaultAESIVPath;

            // Initialize database and helpers
            var dbContext = new AppDbContext(connectionString);
            DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);

            var dapperExecutor = new DapperExecutor(dbContext);
            var protectedKeyProvider = new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath);
            var securePassword = new SecurePassword(protectedKeyProvider);
            var xmlSerializer = new XmlServiceSerializer();

            var serviceRepository = new ServiceRepository(dapperExecutor, securePassword, xmlSerializer);
            var resourceHelper = new ResourceHelper(serviceRepository);

            if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, ServyRestarterExeFileName, "exe", false))
            {
                EventLog.WriteEntry(
                    eventSource,
                    $"Failed copying embedded resource: {ServyRestarterExeFileName}.exe",
                    EventLogEntryType.Error
                );
                return; // stop if critical resource missing
            }

#if DEBUG
            // Copy debug symbols from embedded resources (only in debug builds)
            if (!await resourceHelper.CopyEmbeddedResource(asm, ResourcesNamespace, ServyRestarterExeFileName, "pdb", false))
            {
                EventLog.WriteEntry(
                    eventSource,
                    $"Failed copying embedded resource: {ServyRestarterExeFileName}.pdb",
                    EventLogEntryType.Warning
                );
            }
#endif
            
            ServiceBase[] servicesToRun =
            {
                new Service()
            };
            ServiceBase.Run(servicesToRun);
        }

    }
}
