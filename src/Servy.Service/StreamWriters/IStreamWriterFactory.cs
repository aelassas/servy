namespace Servy.Service.StreamWriters
{
    /// <summary>
    /// Factory interface for creating instances of <see cref="IStreamWriter"/>.
    /// </summary>
    public interface IStreamWriterFactory
    {
        /// <summary>
        /// Creates a new <see cref="IStreamWriter"/> for the specified file path and rotation size.
        /// </summary>
        /// <param name="path">The file path where the stream writer will write.</param>
        /// <param name="rotationSizeInBytes">The maximum size in bytes before rotating the log file.</param>
        /// <param name="maxRotations">The maximum number of rotated log files to keep. Set to 0 for unlimited.</param>
        /// <returns>An <see cref="IStreamWriter"/> instance.</returns>
        IStreamWriter? Create(string path, long rotationSizeInBytes, int maxRotations);
    }
}
