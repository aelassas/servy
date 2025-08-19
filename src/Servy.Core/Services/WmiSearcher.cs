using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management;

namespace Servy.Core.Services
{
    [ExcludeFromCodeCoverage]
    public class WmiSearcher : IWmiSearcher
    {
        public IEnumerable<ManagementObject> Get(string query)
        {
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    yield return obj;
                }
            }
        }
    }
}
