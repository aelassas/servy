using Xunit;

namespace Servy.Restarter.UnitTests
{
    // This attribute defines the synchronization boundary name.
    // xUnit will NEVER run tests within the same collection concurrently.
    [CollectionDefinition("RestarterProgramTests", DisableParallelization = true)]
    public class ProgramTestsCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}
