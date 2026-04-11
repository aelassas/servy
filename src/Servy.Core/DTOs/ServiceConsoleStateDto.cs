namespace Servy.Core.DTOs
{
    /// <summary>
    /// A lightweight projection of a service's running state, specifically optimized 
    /// for high-frequency UI polling in the Console tab.
    /// </summary>
    public class ServiceConsoleStateDto : ICloneable
    {
        /// <summary>
        /// The current Process ID of the service.
        /// </summary>
        public int? Pid { get; set; }

        /// <summary>
        /// The current absolute path for standard output redirection.
        /// </summary>
        public string? ActiveStdoutPath { get; set; }

        /// <summary>
        /// The current absolute path for standard error redirection.
        /// </summary>
        public string? ActiveStderrPath { get; set; }

        /// <summary>
        /// Creates a copy of the current state.
        /// </summary>
        public object Clone()
        {
            return new ServiceConsoleStateDto
            {
                Pid = Pid,
                ActiveStdoutPath = ActiveStdoutPath,
                ActiveStderrPath = ActiveStderrPath
            };
        }
    }
}