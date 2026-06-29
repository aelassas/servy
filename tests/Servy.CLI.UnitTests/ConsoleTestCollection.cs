namespace Servy.CLI.UnitTests
{
    // This attribute defines the synchronization boundary name.
    // xUnit will NEVER run tests within the same collection concurrently.
    [CollectionDefinition("SequentialConsoleTests", DisableParallelization = true)]
    public class ConsoleTestCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}
