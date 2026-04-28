using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Models;
using System;
using System.Threading.Tasks;

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
        /// <remarks>
        /// <para>
        /// <b>Performance Note:</b> This method performs a shallow mapping of static metadata only. 
        /// Volatile OS-level states (Status, IsInstalled, StartupType) are initialized to placeholder 
        /// defaults to prevent blocking the UI thread with SCM queries during bulk mapping.
        /// </para>
        /// <para>
        /// These "Pending" values are expected to be reconciled by the background 
        /// monitoring loop immediately following the initial load.
        /// </para>
        /// </remarks>
        /// <param name="service">The domain service instance.</param>
        /// <param name="isDesktopAppAvailable">Indicates whether the configuration application is available.</param>
        /// <param name="calculatePerf">Whether to calculate CPU and RAM usage.</param>
        /// <param name="processHelper">Injected process helper used to gather usage metrics.</param>
        /// <returns>A UI-friendly <see cref="Service"/> model.</returns>
        public static async Task<Service> ToModelAsync(Core.Domain.Service service, bool isDesktopAppAvailable, bool calculatePerf, IProcessHelper processHelper)
        {
            if (service == null) return null;

            double? cpuUsage = null;
            long? ramUsage = null;
            if (calculatePerf && service.Pid.HasValue && processHelper != null)
            {
                var processMetrics = await Task.Run(() => processHelper.GetProcessTreeMetrics(service.Pid.Value));
                cpuUsage = processMetrics.CpuUsage;
                ramUsage = processMetrics.RamUsage;
            }

            return new Service
            {
                Name = service.Name,
                Description = service.Description ?? string.Empty,
                StartupType = null,
                Status = ServiceStatus.None,
                LogOnAs = service.RunAsLocalSystem ? AppConfig.LocalSystem : GetLogOnAsDisplayName(service.UserAccount) ?? string.Empty,
                IsInstalled = false,
                IsDesktopAppAvailable = isDesktopAppAvailable,
                Pid = service.Pid,
                IsPidEnabled = service.Pid != null,
                CpuUsage = cpuUsage,
                RamUsage = ramUsage,
                StdoutPath = service.StdoutPath,
                StderrPath = service.StderrPath,
                ActiveStdoutPath = service.ActiveStdoutPath,
                ActiveStderrPath = service.ActiveStderrPath,
            };
        }

        /// <summary>
        /// Converts a <see cref="ServiceItemBase"/> into its corresponding <see cref="Service"/> domain model representation.
        /// </summary>
        /// <param name="item">The source service item to be converted.</param>
        /// <returns>
        /// A populated <see cref="Service"/> object if <paramref name="item"/> is not null; otherwise, <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// This method facilitates the mapping between UI/DTO service items and the core domain model. 
        /// It includes polymorphic handling: if the input is a <see cref="ConsoleService"/>, the 
        /// <c>StdoutPath</c> and <c>StderrPath</c> properties are also preserved in the resulting model.
        /// </remarks>
        public static Service ToModel(ServiceItemBase item)
        {
            if (item == null) return null;
            var service = new Service
            {
                Name = item.Name,
                Pid = item.Pid,
                IsPidEnabled = item.Pid != null,
            };
            if (item is ConsoleService consoleService)
            {
                service.StdoutPath = consoleService.StdoutPath;
                service.StderrPath = consoleService.StderrPath;
            }
            return service;
        }

        /// <summary>
        /// Gets user session display name.
        /// </summary>
        /// <param name="userSession">User session.</param>
        /// <returns>ser session display name.</returns>
        public static string GetLogOnAsDisplayName(string userSession)
        {
            if (string.IsNullOrEmpty(userSession))
                return AppConfig.LocalSystem;
            if (userSession.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase))
                return AppConfig.LocalSystem;
            return userSession;
        }
    }
}