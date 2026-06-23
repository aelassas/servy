namespace Servy.Infrastructure.IntegrationTests
{
    // This attribute defines the synchronization boundary name.
    // xUnit will NEVER run tests within the same collection concurrently.
    [CollectionDefinition("SequentialDatabaseTests", DisableParallelization = true)]
    public class DatabaseTestCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}