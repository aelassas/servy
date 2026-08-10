namespace Servy.Infrastructure.IntegrationTests
{
    // Defines the "SequentialDatabaseTests" collection. DisableParallelization = true
    // stops xUnit from running this collection in parallel with any OTHER collection,
    // so the DB test classes that join it get exclusive access to the shared SQLite file.
    // (Tests within a single collection are always sequential; the flag adds the
    // cross-collection guarantee.)
    [CollectionDefinition("SequentialDatabaseTests", DisableParallelization = true)]
    public class DatabaseTestCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}
