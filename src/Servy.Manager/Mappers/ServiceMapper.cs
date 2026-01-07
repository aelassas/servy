using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Models;
using System.Windows;

namespace Servy.Manager
{
    /// <summary>
    /// Maps domain Service objects to WPF Service models for UI binding.
    /// </summary>
    public static class ServiceMapper
    {
        /// <summary>
        /// Maps a <see cref="Core.Domain.Service"/> to a <see cref="Service"/>.
        /// </summary>
        /// <param name="service">The domain service instance.</param>
        /// <param name="calculatePerf">Whether to calculate CPU and RAM usage.</param>
        /// <returns>A UI-friendly <see cref="Service"/> model.</returns>
        public static async Task<Service> ToModelAsync(Core.Domain.Service service, bool calculatePerf)
        {
            if (service == null) return null;

            var app = (App)Application.Current;

            double? cpuUsage = null;
            long? ramUsage = null;
            if (calculatePerf && service.Pid.HasValue)
            {
                cpuUsage = await Task.Run(() => ProcessHelper.GetCpuUsage(service.Pid.Value));
                ramUsage = await Task.Run(() => ProcessHelper.GetRamUsage(service.Pid.Value));
            }

            return new Service
            {
                Name = service.Name,
                Description = service.Description ?? string.Empty,
                StartupType = null,
                Status = ServiceStatus.None,
                UserSession = service.RunAsLocalSystem ? AppConfig.LocalSystem : GetUserSessionDisplayName(service.UserAccount) ?? string.Empty,
                IsInstalled = false,
                IsConfigurationAppAvailable = app.IsConfigurationAppAvailable,
                Pid = service.Pid,
                IsPidEnabled = service.Pid != null,
                CpuUsage = cpuUsage,
                RamUsage = ramUsage,
            };
        }

        /// <summary>
        /// Gets user session display name.
        /// </summary>
        /// <param name="userSession">User session.</param>
        /// <returns>ser session display name.</returns>
        public static string GetUserSessionDisplayName(string userSession)
        {
            if (string.IsNullOrEmpty(userSession))
                return AppConfig.LocalSystem;
            if (userSession.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase))
                return AppConfig.LocalSystem;
            return userSession;
        }
    }
}
