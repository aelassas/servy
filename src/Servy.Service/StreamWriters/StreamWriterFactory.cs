using Servy.Core.Enums;

namespace Servy.Service.StreamWriters
{
    /// <summary>
    /// Default implementation of <see cref="IStreamWriterFactory"/> that creates
    /// <see cref="RotatingStreamWriterAdapter"/> instances.
    /// </summary>
    public class StreamWriterFactory : IStreamWriterFactory
    {
        /// <inheritdoc/>
        public IStreamWriter Create(
            string path,
            bool enableSizeRotation,
            long rotationSizeInBytes,
            bool enableDateRotation,
            DateRotationType dateRotationType,
            int maxRotations,
            bool useLocalTimeForRotation)
        {
            return new RotatingStreamWriterAdapter(path,
                enableSizeRotation, 
                rotationSizeInBytes, 
                enableDateRotation,
                dateRotationType, 
                maxRotations,
                useLocalTimeForRotation);
        }
    }
}
