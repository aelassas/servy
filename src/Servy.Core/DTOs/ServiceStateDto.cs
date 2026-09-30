namespace Servy.Core.DTOs
{
    /// <summary>
    /// The runtime state a running Servy wrapper writes back about its own service.
    /// </summary>
    /// <remarks>
    /// These values live in the separate runtime-state database (<c>Servy.state.db</c>) rather than in
    /// the configuration database (<c>Servy.db</c>). The split exists so a service log-on account can be
    /// granted write access to its own runtime state without being able to write any service's
    /// configuration. Nothing here is configuration: every property is produced by the wrapper at run
    /// time and is meaningless until the service has started at least once.
    /// <para>
    /// Any runtime-state column added in the future belongs here, not on <see cref="ServiceDto"/>.
    /// </para>
    /// </remarks>
    public class ServiceStateDto
    {
        /// <summary>
        /// Gets or sets the name of the service this state belongs to.
        /// </summary>
        /// <remarks>
        /// This is the Windows service name, the same key <see cref="ServiceDto.Name"/> carries in the
        /// configuration database. It is the primary key of the runtime-state row.
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process id of the wrapped child process, or <see langword="null"/> when the
        /// service is not running or has never run.
        /// </summary>
        [SqlColumn("INTEGER")]
        public int? Pid { get; set; }

        /// <summary>
        /// Gets or sets the standard output log path the wrapper is currently writing to, or
        /// <see langword="null"/> when it is not writing one.
        /// </summary>
        /// <remarks>
        /// This is the resolved path actually in use, which differs from the configured
        /// <see cref="ServiceDto.StdoutPath"/> once rotation has renamed a file.
        /// </remarks>
        [SqlColumn("TEXT")]
        public string ActiveStdoutPath { get; set; }

        /// <summary>
        /// Gets or sets the standard error log path the wrapper is currently writing to, or
        /// <see langword="null"/> when it is not writing one.
        /// </summary>
        /// <remarks>
        /// This is the resolved path actually in use, which differs from the configured
        /// <see cref="ServiceDto.StderrPath"/> once rotation has renamed a file.
        /// </remarks>
        [SqlColumn("TEXT")]
        public string ActiveStderrPath { get; set; }
    }
}
