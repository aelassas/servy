using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Manager.Config;
using Servy.Manager.Models;

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
        /// Converts a <see cref="PerformanceService"/> instance to a <see cref="Service"/> model.
        /// </summary>
        /// <param name="service">The <see cref="PerformanceService"/> instance to convert. Cannot be null.</param>
        /// <returns>A new <see cref="Service"/> object populated with values from the specified <paramref name="service"/>.</returns>
        public static Service ToModel(PerformanceService service)
        {
            return new Service
            {
                Name = service.Name,
                Pid = service.Pid,
                IsPidEnabled = service.Pid != null,
            };
        }

        /// <summary>
        /// Converts a <see cref="ConsoleService"/> instance to a <see cref="Service"/> model.
        /// </summary>
        /// <param name="service">The <see cref="ConsoleService"/> instance to convert. Cannot be null.</param>
        /// <returns>A new <see cref="Service"/> object populated with values from the specified <paramref name="service"/>.</returns>
        public static Service ToModel(ConsoleService service)
        {
            return new Service
            {
                Name = service.Name,
                Pid = service.Pid,
                IsPidEnabled = service.Pid != null,
                StdoutPath = service.StdoutPath,
                StderrPath = service.StderrPath,
            };
        }

        /// <summary>
        /// Converts a <see cref="DependencyService"/> instance to a <see cref="Service"/> model.
        /// </summary>
        /// <param name="service">The <see cref="DependencyService"/> instance to convert. Cannot be null.</param>
        /// <returns>A new <see cref="Service"/> object populated with values from the specified <paramref name="service"/>.</returns>
        public static Service ToModel(DependencyService service)
        {
            return new Service
            {
                Name = service.Name,
                Pid = service.Pid,
                IsPidEnabled = service.Pid != null,
            };
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