using Xunit;

namespace Servy.Manager.UnitTests
{
    // This attribute defines the synchronization boundary name.
    // xUnit will NEVER run tests within the same collection concurrently.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class AmbientTestCollection
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "AmbientAppServices";
    }
}