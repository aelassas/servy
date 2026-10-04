namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Groups the test classes that write the process-global <see cref="System.Environment.ExitCode"/>
    /// and assert on a termination code production derives from it, so they never run concurrently.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class ExitCodeTestsCollection
    {
        /// <summary>The collection name used by the <c>[Collection]</c> attribute on each member class.</summary>
        public const string Name = "Environment.ExitCode writers";
    }
}
