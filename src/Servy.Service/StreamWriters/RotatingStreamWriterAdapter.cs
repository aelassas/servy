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
        /// <param name="path">The file path to write logs to.</param>
        /// <param name="rotationSize">The maximum file size in bytes before rotation.</param>
        public RotatingStreamWriterAdapter(string path, long rotationSize)
        {
            _inner = new RotatingStreamWriter(path, rotationSize);
        }

        /// <inheritdoc/>
        public void WriteLine(string line)
        {
            ThrowIfDisposed();
            _inner!.WriteLine(line);
        }

        /// <inheritdoc/>
        public void Write(string text)
        {
            ThrowIfDisposed();
            _inner!.Write(text);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed resources
                _inner?.Dispose();
                _inner = null;
            }

            _disposed = true;
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
