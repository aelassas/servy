using Servy.Core.Enums;

namespace Servy.Service.StreamWriters
{
    /// <summary>
    /// Factory interface for creating instances of <see cref="IStreamWriter"/>.
    /// </summary>
    public interface IStreamWriterFactory
    {
        /// <summary>
        /// Creates a new <see cref="IStreamWriter"/> for the specified file path and rotation settings.
        /// </summary>
        /// <inheritdoc cref="RotatingStreamWriter(string, bool, long, bool, DateRotationType, int, bool, Func{DateTime})"/>
        /// <returns>An <see cref="IStreamWriter"/> instance.</returns>
        IStreamWriter Create(
            string path,
            bool enableSizeRotation,
            long rotationSizeInBytes,
            bool enableDateRotation,
            DateRotationType dateRotationType,
            int maxRotations,
            bool useLocalTimeForRotation);
    }
}
