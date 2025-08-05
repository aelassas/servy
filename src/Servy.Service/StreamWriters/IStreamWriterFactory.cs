namespace Servy.Service
{
    public interface IStreamWriterFactory
    {
        IStreamWriter Create(string path, long rotationSizeInBytes);
    }
}
