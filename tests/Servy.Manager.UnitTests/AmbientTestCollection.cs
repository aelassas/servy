using Xunit;

namespace Servy.Manager.UnitTests
{
    // Defines the collection. DisableParallelization = true stops xUnit from running
    // this collection in parallel with any OTHER collection, protecting the shared
    // process-global state its members mutate. (Within a collection, tests are
    // always sequential; the flag adds the cross-collection guarantee.)
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class AmbientTestCollection
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "AmbientAppServices";
    }
}
