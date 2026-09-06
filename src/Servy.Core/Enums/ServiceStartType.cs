namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines service start types for Windows services.
    /// </summary>
    /// <remarks>
    /// <b>CRITICAL ARCHITECTURAL NOTE:</b> The integer values assigned to this enumeration are actively
    /// persisted as <c>INTEGER</c> rows within the underlying SQLite database schema (e.g., <c>ServiceDto.StartupType</c>).
    /// They must never be renumbered, reordered, or deleted. In particular, <see cref="AutomaticDelayedStart"/> (5) is
    /// a Servy-defined internal sentinel with no native Win32 anchor, and changing its underlying integer value will
    /// corrupt existing database rows across upgrades.
    /// <para>
    /// Note: <c>default(ServiceStartType)</c> yields <see cref="Unknown"/>, which is not the product default
    /// (<see cref="Servy.Core.Config.AppConfig.DefaultStartupType"/> is <see cref="Automatic"/>) and is not a valid
    /// Win32 <c>dwStartType</c> either - zero is <c>SERVICE_BOOT_START</c> to the SCM. When initializing new fields,
    /// configurations, or data transfer objects, you must explicitly assign an appropriate default state value rather
    /// than relying on the CLR zero-initialization layout.
    /// </para>
    /// </remarks>
    public enum ServiceStartType : uint
    {
        /// <summary>
        /// The startup type could not be determined due to an error or unrecognized state.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The service starts automatically by the Service Control Manager during system startup.
        /// </summary>
        Automatic = 0x00000002,

        /// <summary>
        /// A service that starts automatically after other auto-start services
        /// are started plus a short delay.
        /// </summary>
        /// <remarks>
        /// <b>Internal sentinel.</b> This is NOT a valid Win32 <c>dwStartType</c>.
        /// To implement this, the service must first be set to <see cref="Automatic"/>
        /// via <c>ChangeServiceConfig</c>, followed by a separate call to
        /// <c>ChangeServiceConfig2</c> using <c>SERVICE_CONFIG_DELAYED_AUTO_START_INFO</c>.
        /// </remarks>
        AutomaticDelayedStart = 0x00000005,

        /// <summary>
        /// The service must be started manually by the user or an application.
        /// </summary>
        Manual = 0x00000003,

        /// <summary>
        /// The service is disabled and cannot be started.
        /// </summary>
        Disabled = 0x00000004,
    }
}
