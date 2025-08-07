namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines the recovery actions for the service in case of failure.
    /// </summary>
    public enum RecoveryAction
    {
        /// <summary>
        /// No action will be taken.
        /// </summary>
        None,

        /// <summary>
        /// Restart the service.
        /// </summary>
        RestartService,

        /// <summary>
        /// Restart the process.
        /// </summary>
        RestartProcess,

        /// <summary>
        /// Restart the computer.
        /// </summary>
        RestartComputer,
    }

}
