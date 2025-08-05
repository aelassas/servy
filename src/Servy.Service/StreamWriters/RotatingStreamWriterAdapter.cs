using Servy.Core;

namespace Servy.Service
{
    /// <summary>
    /// Adapter class that wraps a <see cref="RotatingStreamWriter"/> to implement <see cref="IStreamWriter"/>.
    /// </summary>
    public class RotatingStreamWriterAdapter : IStreamWriter
    {
        private readonly RotatingStreamWriter _inner;

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
        public void WriteLine(string line) => _inner.WriteLine(line);

        /// <inheritdoc/>
        public void Dispose() => _inner.Dispose();
    }
}
