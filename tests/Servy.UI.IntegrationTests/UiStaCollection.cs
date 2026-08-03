using Xunit;

namespace Servy.UI.IntegrationTests
{
    [CollectionDefinition("UiSta", DisableParallelization = true)]
    public class UiStaCollection : ICollectionFixture<UiHeadlessFixture>
    {
        // Enforces strict sequential isolation across the execution suite
    }
}
