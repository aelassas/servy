using Servy.Core.Enums;
using Servy.Manager.Config;
using Servy.Manager.Models;
using System.IO;
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
        /// <returns>A UI-friendly <see cref="Service"/> model.</returns>
        public static Service ToModel(Core.Domain.Service service)
        {
            if (service == null) return null;

            var app = (App)Application.Current;

            return new Service
            {
                Name = service.Name,
                Description = service.Description ?? string.Empty,
                Status = service.GetStatus(),
                StartupType = service.GetServiceStartupType() ?? ServiceStartType.Manual,
                UserSession = service.RunAsLocalSystem ? AppConfig.LocalSystem : service.UserAccount ?? string.Empty,
                IsInstalled = service.IsInstalled(),
                IsConfigurationAppAvailable = !string.IsNullOrEmpty(app.ConfigurationAppPublishPath) && File.Exists(app.ConfigurationAppPublishPath)
            };
        }
    }
}
