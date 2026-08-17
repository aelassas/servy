using Xunit;

namespace Servy.Core.IntegrationTests
{
    /// <summary>
    /// Collection definition serializing OS-level integration tests (SCM, native APIs, LSA policy, and event log)
    /// against each other and the rest of the execution suite.
    /// </summary>
    [CollectionDefinition("CoreOsIntegration", DisableParallelization = true)]
    public class CoreOsIntegrationCollection
    {
        // Marker class, no code
    }
}
