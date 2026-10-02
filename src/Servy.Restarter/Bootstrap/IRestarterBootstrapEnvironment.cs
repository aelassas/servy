using Servy.Core.Logging;

namespace Servy.Restarter.Bootstrap
{
    /// <summary>
    /// Seam over the machine-touching start-up calls that <see cref="Program.Run"/> would otherwise make
    /// inline: the Windows event source, the creation of the event-log logger that wraps it, and the
    /// Service Control Manager lookup of the executable a service runs.
    /// </summary>
    /// <remarks>
    /// The production implementation is <see cref="RestarterBootstrapEnvironment"/>. Every member forwards
    /// unchanged to the call it replaced, with the same arguments, on the calling thread, and
    /// <see cref="Program.Run"/> creates that implementation when no seam is supplied, so a restarter
    /// launched the way the Service Control Manager launches it behaves exactly as it did when the calls
    /// were inline.
    /// <para>
    /// The pure logic deliberately stays in <see cref="Program"/>: the argument guards, the best-effort
    /// fallback to file-only logging, the refusal of a service that does not run a Servy wrapper and the
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
        /// Reads the command line the Service Control Manager runs for a service (its <c>ImagePath</c>).
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The command line, or <see langword="null"/> when the service is not installed or it cannot be read.</returns>
        /// <remarks>
        /// The restarter runs under the service account, which has no access to <c>Servy.db</c>; the service's
        /// executable is what tells it the service is managed by Servy.
        /// </remarks>
        string? GetServiceImagePath(string serviceName);
    }
}
