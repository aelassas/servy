using Xunit;

namespace Servy.Core.IntegrationTests
{
    [CollectionDefinition("CoreOsIntegration", DisableParallelization = true)]
    public class CoreOsIntegrationCollection
    {
        // Enforces strict sequential isolation across the execution suite
    }
}
