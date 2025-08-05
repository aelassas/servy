using System;

namespace Servy.Service
{
    public interface IStreamWriter : IDisposable
    {
        void WriteLine(string line);
    }
}
