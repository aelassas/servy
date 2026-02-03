namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a Windows service being tracked for console tailing.
    /// </summary>
    public class ConsoleService : ServiceItemBase
    {
        /// <summary>
        /// Gets or sets the stdout path.
        /// </summary>
        public string StdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the stderr path.
        /// </summary>
        public string StderrPath { get; set; }

    }
}