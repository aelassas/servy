namespace Servy.Core.Enums
{
    /// <summary>
    /// Represents the current status of a Windows service.
    /// Values 1-7 match the <see cref="System.ServiceProcess.ServiceControllerStatus"/> values.
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary>
        /// The status is not yet known or is being fetched. 
        /// This is the CLR default (0).
        /// </summary>
        None = 0,

        /// <summary>
        /// The service is not running.
        /// </summary>
        Stopped = 1,

        /// <summary>
        /// The service is starting.
        /// </summary>
        StartPending = 2,

        /// <summary>
        /// The service is stopping.
        /// </summary>
        StopPending = 3,

        /// <summary>
        /// The service is running.
        /// </summary>
        Running = 4,

        /// <summary>
        /// The service is continuing after being paused.
        /// </summary>
        ContinuePending = 5,

        /// <summary>
        /// The service is pausing.
        /// </summary>
        PausePending = 6,

        /// <summary>
        /// The service is paused.
        /// </summary>
        Paused = 7,

        /// <summary>
        /// Represents a service that is not installed on the system.
        /// </summary>
        NotInstalled = 8,
    }
}