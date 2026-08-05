namespace Servy.Restarter
{
    /// <summary>
    /// Represents the outcome of a service restart operation.
    /// </summary>
    public enum RestartResult
    {
        /// <summary>
        /// The service was successfully stopped (if needed) and started.
        /// </summary>
        Restarted,

        /// <summary>
        /// The target service does not exist in the SCM or vanished mid-flight.
        /// </summary>
        ServiceNotFound
    }
}
