using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    // Defines the collection for the CLI command tests that borrow
    // BaseCommand.BypassElevationCheck, a process-global flag. DisableParallelization = true
    // stops xUnit from running this collection in parallel with any OTHER collection, and
    // within the collection its members run sequentially, so no class can reset the flag
    // while another is between its constructor and the elevation check it guards.
    // Kept separate from SequentialConsoleTests because the state protected is unrelated.
    [CollectionDefinition("SequentialElevationTests", DisableParallelization = true)]
    public class ElevationTestCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}
