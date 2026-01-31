using System.Diagnostics;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Represents a lifecycle hook and its associated operating system process.
    /// </summary>
    public class Hook
    {
        /// <summary>
        /// The logical name of the hook operation (for example, Pre-Launch or Post-Stop).
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// The process started by this hook.
        /// </summary>
        public Process Process { get; set; }
    }
}
