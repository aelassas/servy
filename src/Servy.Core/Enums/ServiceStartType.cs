namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines service start types for Windows services.
    /// </summary>
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
        /// To implement this, the service must first be set to <see cref="AutoStart"/> 
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
