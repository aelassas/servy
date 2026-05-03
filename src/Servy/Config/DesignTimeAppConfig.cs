using System;
using System.ComponentModel;
using Servy.Core.Config;

namespace Servy.Config
{
    /// <summary>
    /// A lightweight implementation of <see cref="IAppConfiguration"/> specifically for 
    /// the XAML designer to prevent constructor-chaining failures.
    /// </summary>
    public class DesignTimeAppConfig : IAppConfiguration
    {
        /// <summary>
        /// Returns true to ensure manager-related UI elements are visible in the designer.
        /// </summary>
        public bool IsManagerAppAvailable => true;

        /// <summary>
        /// Returns the default path from core configuration.
        /// </summary>
        public string? ManagerAppPublishPath => Core.Config.AppConfig.DefaultManagerAppPublishPath;

        /// <summary>
        /// Defaults to false for design-time sessions.
        /// </summary>
        public bool ForceSoftwareRendering => false;

        /// <summary>
        /// No-op implementation as design-time properties are static.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }
}