using Servy.Core.Services;
using System.Threading.Tasks;

namespace Servy.Manager.UnitTests.Services
{
    public class FakeService : Core.Domain.Service
    {
        private readonly bool _stopResult;

        public FakeService(IServiceManager serviceManager, string name, bool stopResult = true)
            : base(serviceManager)
        {
            Name = name;
            _stopResult = stopResult;
        }

        public override async Task<bool> Stop()
        {
            return _stopResult;
        }
    }


}
