namespace Servy.Manager.UnitTests
{
    // This attribute defines the synchronization boundary name.
    // xUnit will NEVER run tests within the same collection concurrently.
    [CollectionDefinition("Ambient AppServices Dependent Tests", DisableParallelization = true)]
    public class AmbientTestCollection
    {
        // This class has no code; it is solely a marker decoration for the attribute.
    }
}