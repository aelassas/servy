namespace Servy.Core.Helpers
{
    /// <summary>
    /// Represents a snapshot of performance metrics captured from a Windows process or process tree.
    /// </summary>
    /// <remarks>
    /// This object is typically returned by <see cref="ProcessHelper.GetProcessMetrics"/> or 
    /// <see cref="ProcessHelper.GetProcessTreeMetrics"/> to provide atomic access to CPU and RAM data.
    /// </remarks>
    public class ProcessMetrics
    {
        /// <summary>
        /// Gets the CPU usage percentage.
        /// </summary>
        /// <value>
        /// A value between 0.0 and 100.0, rounded to one decimal place. 
        /// For process trees, this value is capped at 100.0.
        /// </value>
        public double CpuUsage { get; }

        /// <summary>
        /// Gets the physical memory usage in bytes.
        /// </summary>
        /// <value>
        /// The number of private bytes allocated for the process, equivalent to 
        /// "Private Working Set" in Windows Task Manager.
        /// </value>
        public long RamUsage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessMetrics"/> class.
        /// </summary>
        /// <param name="cpuUsage">The calculated CPU usage percentage.</param>
        /// <param name="ramUsage">The captured RAM usage in bytes.</param>
        public ProcessMetrics(double cpuUsage, long ramUsage)
        {
            CpuUsage = cpuUsage;
            RamUsage = ramUsage;
        }
    }
}