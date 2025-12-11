using Servy.Core.Enums;
using Servy.Core.IO;
using System;

namespace Servy.Service.StreamWriters
{
    /// <summary>
    /// Adapter class that wraps a <see cref="RotatingStreamWriter"/> to implement <see cref="IStreamWriter"/>.
    /// Implements the full Dispose pattern.
    /// </summary>
    public class RotatingStreamWriterAdapter : IStreamWriter
    {
        private RotatingStreamWriter _inner;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RotatingStreamWriterAdapter"/> class.
        /// </summary>
        /// <param name="path">The path to the log file.</param>
        /// <param name="enableSizeRotation">
        /// Enables rotation when the log file exceeds the size specified
        /// in <paramref name="rotationSizeInBytes"/>.
        /// </param>
        /// <param name="rotationSizeInBytes">The maximum file size in bytes before rotating.</param>
        /// <param name="enableDateRotation">
        /// Enables rotation based on the date interval specified by <paramref name="dateRotationType"/>.
        /// </param>
        /// <param name="dateRotationType">
        /// Defines the date-based rotation schedule (daily, weekly, or monthly).
        /// Ignored when <paramref name="enableDateRotation"/> is <c>false</c>.
        /// </param>
        /// <param name="maxRotations">The maximum number of rotated log files to keep. Set to 0 for unlimited.</param>
        /// <remarks>
        /// When both size-based and date-based rotation are enabled,
        /// size rotation takes precedence. If a size-based rotation occurs,
        /// date-based rotation is skipped for that write.
        /// </remarks>
        public RotatingStreamWriterAdapter(
            string path,
            bool enableSizeRotation,
            long rotationSizeInBytes,
            bool enableDateRotation,
            DateRotationType dateRotationType,
            int maxRotations)
        {
            _inner = new RotatingStreamWriter(path, enableSizeRotation, rotationSizeInBytes, enableDateRotation, dateRotationType, maxRotations);
        }

        /// <inheritdoc/>
        public void WriteLine(string line)
        {
            ThrowIfDisposed();
            _inner.WriteLine(line);
        }

        /// <inheritdoc/>
        public void Write(string text)
        {
            ThrowIfDisposed();
            _inner.Write(text);
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
