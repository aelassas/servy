namespace Servy.Core.DTOs
{
    /// <summary>
    /// The runtime state a running wrapper reports about its own service to the Servy host over the named pipe.
    /// </summary>
    /// <remarks>
    /// This is the only part of a service's row a wrapper can write. It is a dedicated contract rather than a
    /// <see cref="ServiceDto"/> for two reasons: the runtime fields of <see cref="ServiceDto"/> are
    /// <c>[JsonIgnore]</c> for the export files, so they would never cross the pipe, and a whole row would let a
    /// service account rewrite its own configuration, which the host must never accept.
    /// </remarks>
    public class ServiceRuntimeStateDto
    {
        /// <summary>
        /// Gets or sets the PID of the child process, or <see langword="null"/> when it is not running.
        /// </summary>
        public int? Pid { get; set; }

        /// <summary>
        /// Gets or sets the file the child process's standard output is redirected to, or <see langword="null"/>.
        /// </summary>
        public string ActiveStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the file the child process's standard error is redirected to, or <see langword="null"/>.
        /// </summary>
        public string ActiveStderrPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="PreviousStopTimeout"/> is written; when
        /// <see langword="false"/>, the stored value is kept.
        /// </summary>
        public bool UpdatePreviousStopTimeout { get; set; }

        /// <summary>
        /// Gets or sets the stop timeout, in seconds, the running instance was started with.
        /// </summary>
        public int? PreviousStopTimeout { get; set; }
    }
}
