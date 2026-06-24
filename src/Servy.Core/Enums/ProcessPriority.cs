namespace Servy.Core.Enums
{
    /// <summary>
    /// Defines the different levels of process priority that can be assigned to a process.
    /// </summary>
    /// <remarks>
    /// <b>CRITICAL NOTE:</b> These integers are persisted directly to the SQLite database configuration layer. 
    /// Do not reorder or alter these values, as it will corrupt process configurations on existing installations.
    /// </remarks>
    public enum ProcessPriority
    {
        /// <summary>Lowest priority level; runs only when the system is idle.</summary>
        Idle = 0,

        /// <summary>Below normal priority; higher than idle but less than normal.</summary>
        BelowNormal = 1,

        /// <summary>Normal priority; the default priority for processes.</summary>
        Normal = 2,

        /// <summary>Above normal priority; higher than normal but lower than high.</summary>
        AboveNormal = 3,

        /// <summary>High priority; receives significant CPU scheduling precedence.</summary>
        High = 4,

        /// <summary>Real-time priority; highest priority. Monopolizes CPU resources.</summary>
        RealTime = 5
    }
}