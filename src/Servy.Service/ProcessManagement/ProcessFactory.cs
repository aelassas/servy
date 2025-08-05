using System.Diagnostics;

namespace Servy.Service
{
    public class ProcessFactory : IProcessFactory
    {
        public IProcessWrapper Create(ProcessStartInfo startInfo)
        {
            return new ProcessWrapper(startInfo);
        }
    }
}
