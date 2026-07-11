namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// ProcessIntegrationTestsCollection is a collection definition for integration tests that involve process management.
    /// </summary>
    [CollectionDefinition("ProcessIntegrationTests", DisableParallelization = true)]
    public class ProcessIntegrationTestsCollection
    {
        // Enforces strict sequential isolation across the execution suite
    }
}
