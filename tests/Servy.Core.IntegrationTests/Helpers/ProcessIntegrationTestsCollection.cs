namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// ProcessIntegrationTestsCollection is a collection definition for integration tests that involve process management.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ProcessIntegrationTestsCollection
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "ProcessIntegrationTests";

        // Enforces strict sequential isolation across the execution suite
    }
}
