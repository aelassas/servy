using Servy.Core.Services;

namespace Servy.Manager.UnitTests.Services
{
    public class FakeService : Core.Domain.Service
    {
        public FakeService(IServiceManager serviceManager, string name, bool stopResult = true)
            : base(serviceManager)
        {
            Name = name;
        }
    }


}
