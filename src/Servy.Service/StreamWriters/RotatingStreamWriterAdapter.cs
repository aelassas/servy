using Servy.Core;

namespace Servy.Service
{
    public class RotatingStreamWriterAdapter : IStreamWriter
    {
        private readonly RotatingStreamWriter _inner;

        public RotatingStreamWriterAdapter(string path, long rotationSize)
        {
            _inner = new RotatingStreamWriter(path, rotationSize);
        }

        public void WriteLine(string line) => _inner.WriteLine(line);

        public void Dispose() => _inner.Dispose();
    }

}
