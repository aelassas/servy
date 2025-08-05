using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servy.Service
{
    public interface ICommandLineProvider
    {
        string[] GetArgs();
    }
}
