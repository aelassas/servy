using Xunit;

namespace Servy.UI.IntegrationTests
{
    [CollectionDefinition("UiSta", DisableParallelization = true)]
    public class UiStaCollection
    {
        // Enforces strict sequential isolation across the execution suite
    }
}
