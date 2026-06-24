namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines the recovery actions for the service in case of failure.
    /// </summary>
    /// <remarks>
    /// <b>CRITICAL ARCHITECTURAL NOTE:</b> The integer values assigned to this enumeration are actively 
    /// persisted within the SQLite configuration database layer. They must never be renumbered or 
    /// reordered, as doing so will corrupt recovery orchestration configurations across existing database deployments.
    /// </remarks>
    public enum RecoveryAction
    {
        /// <summary>
        /// No action will be taken.
        /// </summary>
        None = 0,

        /// <summary>
        /// Restart the service.
        /// </summary>
        RestartService = 1,

        /// <summary>
        /// Restart the process.
        /// </summary>
        RestartProcess = 2,

        /// <summary>
        /// Restart the computer.
        /// </summary>
        RestartComputer = 3,
    }
}