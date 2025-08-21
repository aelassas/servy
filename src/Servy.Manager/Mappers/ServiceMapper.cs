using Servy.Core.Enums;
using Servy.Manager.Config;
using Servy.Manager.Models;
using System;
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
                StartupType = null,
                Status = ServiceStatus.None,
                UserSession = service.RunAsLocalSystem ? AppConfig.LocalSystem : GetUserSessionDisplayName(service.UserAccount) ?? string.Empty,
                IsInstalled = false,
                IsConfigurationAppAvailable = app.IsConfigurationAppAvailable
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
