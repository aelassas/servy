using Servy.Core.Enums;
using Servy.Core.IO;

namespace Servy.Service.StreamWriters
{
    /// <summary>
    /// Adapter class that wraps a <see cref="RotatingStreamWriter"/> to implement <see cref="IStreamWriter"/>.
    /// Implements the full Dispose pattern.
    /// </summary>
    public class RotatingStreamWriterAdapter : IStreamWriter
    {
        private RotatingStreamWriter? _inner;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RotatingStreamWriterAdapter"/> class.
        /// </summary>
        /// <inheritdoc cref="IStreamWriterFactory.Create"/>
        /// <remarks>
        /// The inner writer refuses reparse points: a log directory, ancestor directory or log file that is a junction,
        /// symbolic link or mount point makes this constructor throw <see cref="IOException"/>, and a refusal after
        /// construction logs an error and disables capture to the file (#7527).
        /// </remarks>
        public RotatingStreamWriterAdapter(
            string path,
            bool enableSizeRotation,
            long rotationSizeInBytes,
            bool enableDateRotation,
            DateRotationType dateRotationType,
            int maxRotations,
            bool useLocalTimeForRotation)
        {
            _inner = new RotatingStreamWriter(
                path,
                enableSizeRotation,
                rotationSizeInBytes,
                enableDateRotation,
                dateRotationType,
                maxRotations,
                useLocalTimeForRotation,
                refuseReparsePoints: true);
        }

        /// <inheritdoc />
        public void WriteLine(string line)
        {
            ThrowIfDisposed();
            _inner!.WriteLine(line);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> when called from <see cref="Dispose()"/>. This type has no finalizer,
        /// so it is never <see langword="false"/>; the parameter exists for derived types to override.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                // Dispose managed resources
                _inner?.Dispose();
                _inner = null;
            }
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this instance has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RotatingStreamWriterAdapter));
        }
    }
}
