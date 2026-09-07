namespace Servy.Core.IntegrationTests
{
    /// <summary>
    /// Collection definition serializing OS-level integration tests (SCM, native APIs, LSA policy, and event log)
    /// against each other and the rest of the execution suite.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class CoreOsIntegrationCollection
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "CoreOsIntegration";
    }
}
