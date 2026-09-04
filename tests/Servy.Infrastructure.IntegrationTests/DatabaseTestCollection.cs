namespace Servy.Infrastructure.IntegrationTests
{
    // Defines the "SequentialDatabaseTests" collection. DisableParallelization = true
    // stops xUnit from running this collection in parallel with any OTHER collection.
    // Each member already gets its own database (a GUID temp file or a GUID-named
    // in-memory one), so the flag is not about the data: it guards process-wide SQLite
    // state these classes touch - SQLiteConnection.ClearAllPools() and the
    // SQLiteFunction.RegisterFunction registry for UNICODE_NOCASE.
    // (Tests within a single collection are always sequential; the flag adds the
    // cross-collection guarantee.)
    [CollectionDefinition("SequentialDatabaseTests", DisableParallelization = true)]
    public class DatabaseTestCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}
