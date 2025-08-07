namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines the different levels of process priority that can be assigned to a process.
    /// These priority levels influence how the operating system schedules CPU time for the process.
    /// Lower priorities mean the process gets less CPU time compared to higher priorities.
    /// Use RealTime priority with caution as it can starve other processes of CPU resources.
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// The process runs only when the system is idle and other processes are not using the CPU.
        /// This is the lowest priority level.
        /// </summary>
        Idle,

        /// <summary>
        /// The process has below normal priority, less than normal but higher than idle.
        /// </summary>
        BelowNormal,

        /// <summary>
        /// The process has normal priority, which is the default priority for processes.
        /// </summary>
        Normal,

        /// <summary>
        /// The process has above normal priority, higher than normal but lower than high.
        /// </summary>
        AboveNormal,

        /// <summary>
        /// The process has high priority, it receives more CPU time compared to normal priority.
        /// </summary>
        High,

        /// <summary>
        /// The process has real-time priority, the highest priority.
        /// Use with caution as it can monopolize CPU resources and starve other processes.
        /// </summary>
        RealTime
    }
}
