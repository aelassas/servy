using Servy.Core.Logging;

namespace Servy.Restarter.Bootstrap
{
    /// <summary>
    /// Seam over the machine-touching start-up calls that <see cref="Program.Run"/> would otherwise make
    /// inline: the Windows event source, the creation of the event-log logger that wraps it, and the
    /// SQLite version check of the CVE-2025-6965 mitigation.
    /// </summary>
    /// <remarks>
    /// The production implementation is <see cref="RestarterBootstrapEnvironment"/>. Every member forwards
    /// unchanged to the call it replaced, with the same arguments, on the calling thread, and
    /// <see cref="Program.Run"/> creates that implementation when no seam is supplied, so a restarter
    /// launched the way the Service Control Manager launches it behaves exactly as it did when the calls
    /// were inline.
    /// <para>
    /// The pure logic deliberately stays in <see cref="Program"/>: the argument guards, the best-effort
    /// fallback to file-only logging, the fatal exit on a vulnerable SQLite version and the
    /// scoped-then-root-then-static logger rules of the surrounding <c>catch</c>. Those are what a test
    /// asserts once this seam lets it drive the start-up sequence at all.
    /// </para>
    /// </remarks>
    internal interface IRestarterBootstrapEnvironment
    {
        /// <summary>
        /// Ensures the Windows event source the restarter logs under exists, creating it when it does not.
        /// </summary>
        /// <exception cref="System.Exception">
        /// Any exception the underlying event-log registration raises is allowed to propagate, exactly as
        /// the inline call did, so the best-effort <c>catch</c> in <see cref="Program.Run"/> handles it.
        /// </exception>
        void EnsureEventSourceExists();

        /// <summary>
        /// Creates the event-log logger the restarter uses as its root logger.
        /// </summary>
        /// <param name="isEventLogEnabled">
        /// <see langword="true"/> to let the logger write to the Windows Event Log, which is what the
        /// start-up path asks for once the event source is known to exist; <see langword="false"/> for the
        /// file-only fallback taken when registering the source failed.
        /// </param>
        /// <returns>The logger <see cref="Program.Run"/> keeps as its root logger and disposes.</returns>
        /// <exception cref="System.Exception">
        /// Any exception the logger construction raises is allowed to propagate, exactly as the inline
        /// <c>new</c> did.
        /// </exception>
        IServyLogger CreateEventLogLogger(bool isEventLogEnabled);

        /// <summary>
        /// Determines whether the SQLite version the process would open its database with is at or above
        /// the minimum required by the CVE-2025-6965 mitigation.
        /// </summary>
        /// <param name="detectedVersion">
        /// When this method returns, the version string that was detected, or <see langword="null"/> when
        /// it could not be determined.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the detected version is safe to use; otherwise <see langword="false"/>.
        /// </returns>
        bool IsSqliteVersionSafe(out string detectedVersion);
    }
}
