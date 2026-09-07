namespace Servy.UI.IntegrationTests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class UiStaCollection : ICollectionFixture<UiHeadlessFixture>
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "UiSta";

        // Enforces strict sequential isolation across the execution suite
    }
}
