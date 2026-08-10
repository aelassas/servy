using Xunit;

namespace Servy.Restarter.UnitTests
{
    // Defines the collection. DisableParallelization = true stops xUnit from running
    // this collection in parallel with any OTHER collection, protecting the shared
    // process-global state its members mutate. (Within a collection, tests are
    // always sequential; the flag adds the cross-collection guarantee.)
    [CollectionDefinition("RestarterProgramTests", DisableParallelization = true)]
    public class ProgramTestsCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}
