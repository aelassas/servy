using Microsoft.Extensions.Configuration;
using System.Windows;

namespace Servy.UI.Bootstrapping
{
    /// <summary>
    /// Configuration options used to initialize the <see cref="AppBootstrapper"/>.
    /// </summary>
    /// <remarks>
    /// This class allows project-specific settings (like resource namespaces, localized strings,
    /// and window factories) to be passed into the shared startup logic.
    /// </remarks>
    public class BootstrapperOptions
    {
        /// <summary>
        /// Gets or sets the name of the log file (e.g., "Servy.log"). Required.
        /// </summary>
        public string? LogFileName { get; set; }

        /// <summary>
        /// Gets or sets the base namespace where embedded resource files are located. Required.
        /// </summary>
        public string? ResourcesNamespace { get; set; }

        /// <summary>
        /// Gets or sets the name of the JSON configuration file (e.g., "appsettings.desktop.json"). Required.
        /// </summary>
        public string? AppSettingsFileName { get; set; }

        #region Localized Strings

        /// <summary>
        /// Gets or sets the title for the administrator privilege warning dialog. Required.
        /// </summary>
        public string? SecurityWarningTitle { get; set; }

        /// <summary>
        /// Gets or sets the message displayed when the application is not running as an administrator. Required.
        /// </summary>
        public string? SecurityWarningMessage { get; set; }

        /// <summary>
        /// Gets or sets the title for the embedded-resource extraction warning dialog. Optional.
        /// </summary>
        public string? ResourceExtractionWarningTitle { get; set; }

        /// <summary>
        /// Gets or sets the title for the SQLite version compatibility warning dialog. Required.
        /// </summary>
        public string? SqliteVersionWarningTitle { get; set; }

        /// <summary>
        /// Gets or sets the composite format string for the SQLite version error. Required.
        /// <c>{0}</c> is the detected version, <c>{1}</c> is the required version.
        /// </summary>
        public string? SqliteVersionWarningMessageFormat { get; set; }

        #endregion

        #region UI Factories

        /// <summary>
        /// Gets or sets a factory function to create the splash screen window. Optional.
        /// </summary>
        public Func<Window>? SplashWindowFactory { get; set; }

        /// <summary>
        /// Gets or sets a factory function to create and show the main application window.
        /// The function receives an optional service name for deep-linking. Optional.
        /// </summary>
        public Func<string?, Task<Window>>? MainWindowFactoryAsync { get; set; }

        #endregion

        /// <summary>
        /// Gets or sets an action to extract project-specific settings from the
        /// loaded <see cref="IConfiguration"/>. Optional.
        /// </summary>
        /// <remarks>
        /// This is called during the initialization sequence after the shared configuration
        /// (connection strings, log levels) has been processed.
        /// </remarks>
        public Action<IConfiguration>? CustomConfigAction { get; set; }
    }
}
