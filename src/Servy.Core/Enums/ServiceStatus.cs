namespace Servy.Core.Enums
{
    /// <summary>
    /// Represents the current status of a Windows service.
    /// Matches the <see cref="System.ServiceProcess.ServiceControllerStatus"/> values.
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary>
        /// The service is not running.
        /// </summary>
        Stopped = 0,

        /// <summary>
        /// The service is starting.
        /// </summary>
        StartPending = 1,

        /// <summary>
        /// The service is stopping.
        /// </summary>
        StopPending = 2,

        /// <summary>
        /// The service is running.
        /// </summary>
        Running = 3,

        /// <summary>
        /// The service is continuing after being paused.
        /// </summary>
        ContinuePending = 4,

        /// <summary>
        /// The service is pausing.
        /// </summary>
        PausePending = 5,

        /// <summary>
        /// The service is paused.
        /// </summary>
        Paused = 6,

        /// <summary>
        /// Represents a "Fetching..." or unknown state used in the Manager UI.
        /// </summary>
        None = 7,

        /// <summary>
        /// Represents a service that is not installed.
        /// </summary>
        NotInstalled = 8,
    }
}
