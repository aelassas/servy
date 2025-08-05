using System.Diagnostics;

namespace Servy.Service
{
    public interface IProcessFactory
    {
        IProcessWrapper Create(ProcessStartInfo startInfo);
    }
}
