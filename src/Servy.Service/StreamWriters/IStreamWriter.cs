namespace Servy.Service.StreamWriters
{
    /// <summary>
    /// Represents a writable stream interface that supports writing lines and disposal.
    /// </summary>
    public interface IStreamWriter : IDisposable
    {
        /// <summary>
        /// Writes a line of text to the stream.
        /// </summary>
        /// <param name="line">The text line to write.</param>
        void WriteLine(string line);
    }
}
