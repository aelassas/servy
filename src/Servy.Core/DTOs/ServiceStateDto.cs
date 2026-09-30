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

        /// <summary>
        /// Gets or sets the stop timeout in seconds the wrapper last ran with, or
        /// <see langword="null"/> when the service has never recorded one.
        /// </summary>
        /// <remarks>
        /// This is the value a running wrapper writes back about itself, not the configured
        /// <see cref="ServiceDto.StopTimeout"/>: the two differ for as long as a configuration change
        /// has not been picked up by a restart. It is runtime state for the same reason the three
        /// properties above are - it is produced by the wrapper at run time and is meaningless until
        /// the service has started at least once - so it belongs in the runtime-state database rather
        /// than on <see cref="ServiceDto"/>.
        /// </remarks>
        [SqlColumn("INTEGER")]
        public int? PreviousStopTimeout { get; set; }
    }
}
