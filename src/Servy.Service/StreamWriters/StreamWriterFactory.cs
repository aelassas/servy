namespace Servy.Service
{
    public class StreamWriterFactory : IStreamWriterFactory
    {
        public IStreamWriter Create(string path, long rotationSizeInBytes)
        {
            return new RotatingStreamWriterAdapter(path, rotationSizeInBytes);
        }
    }
}
