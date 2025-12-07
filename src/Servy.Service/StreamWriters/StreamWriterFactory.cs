namespace Servy.Service.StreamWriters
{
    /// <summary>
    /// Default implementation of <see cref="IStreamWriterFactory"/> that creates
    /// <see cref="RotatingStreamWriterAdapter"/> instances.
    /// </summary>
    public class StreamWriterFactory : IStreamWriterFactory
    {
        /// <inheritdoc/>
        public IStreamWriter Create(string path, long rotationSizeInBytes, int maxRotations)
        {
            return new RotatingStreamWriterAdapter(path, rotationSizeInBytes, maxRotations);
        }
    }
}
