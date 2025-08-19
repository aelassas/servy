using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Infrastructure.Data;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Servy.Infrastructure.Helpers
{
    /// <summary>
    /// Provides helper methods for inserting dummy service data into the database.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class DummyHelper
    {
        /// <summary>
        /// Inserts a specified number of dummy services into the database.
        /// </summary>
        /// <param name="connString">The connection string to the database.</param>
        /// <param name="count">The number of dummy services to insert.</param>
        /// <remarks>
        /// Each dummy service will have a unique name in the format "Dummy Service {i}" and default configuration values.
        /// Existing services with the same name will not be duplicated.
        /// </remarks>
        /// <example>
        /// <code>
        /// await DummyHelper.InsertDummyServices("Data Source=Servy.db", 5);
        /// </code>
        /// </example>
        public static async Task InsertDummyServices(string connString, int count)
        {
            try
            {
                var appContext = new AppDbContext(connString);
                var securePassword = new SecurePassword(new ProtectedKeyProvider("aes.dat", "iv.dat"));
                var xmlServiceSerializer = new XmlServiceSerializer();
                var serviceRepository = new ServiceRepository(new DapperExecutor(appContext), securePassword, xmlServiceSerializer);

                for (int i = 1; i <= count; i++)
                {
                    var serviceName = $"DummyService{i}";
                    var dto = await serviceRepository.GetByNameAsync(serviceName);

                    if (dto == null)
                    {
                        await serviceRepository.AddAsync(new ServiceDto
                        {
                            Name = serviceName,
                            Description = $"{serviceName} Description...",
                            ExecutablePath = @"c:\Program Files\nodejs\node.exe",
                            StartupType = (int)ServiceStartType.Automatic,
                            Priority = (int)ProcessPriority.Normal,
                            EnableRotation = false,
                            RotationSize = 10 * 1024 * 1024,
                            EnableHealthMonitoring = false,
                            HeartbeatInterval = 30,
                            MaxFailedChecks = 1,
                            MaxRestartAttempts = 1,
                            RecoveryAction = (int)RecoveryAction.RestartService,
                            RunAsLocalSystem = true,
                            PreLaunchTimeoutSeconds = 30,
                            PreLaunchRetryAttempts = 1,
                            PreLaunchIgnoreFailure = false,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while inserting dummy services: {ex}");
            }
        }
    }
}
