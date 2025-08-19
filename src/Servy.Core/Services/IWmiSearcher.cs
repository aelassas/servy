using System.Management;

namespace Servy.Core.Services
{
    public interface IWmiSearcher
    {
        IEnumerable<ManagementObject> Get(string query);
    }
}
