using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servy.Service
{
    public class CommandLineProvider : ICommandLineProvider
    {
        public string[] GetArgs() => Environment.GetCommandLineArgs();
    }
}
