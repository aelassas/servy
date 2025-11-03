namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines service start types for Windows services.
    /// </summary>
    public enum ServiceStartType : uint
    {
        /// <summary>
        /// The service starts automatically by the Service Control Manager during system startup.
        /// </summary>
        Automatic = 0x00000002,

        /// <summary>
        /// The service starts automatically, but with a delay after other auto-start services.
        /// </summary>
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
