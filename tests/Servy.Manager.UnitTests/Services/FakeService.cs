using Servy.Core.Common;
using Servy.Core.Services;
using System.Threading;
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

        public override async Task<OperationResult> Stop(CancellationToken cancellationToken = default)
        {
            return _stopResult ? OperationResult.Success() : OperationResult.Failure("Failed to stop service.");
        }
    }


}
