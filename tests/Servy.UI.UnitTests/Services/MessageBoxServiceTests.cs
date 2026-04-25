using Servy.UI.Services;
using Xunit;

namespace Servy.UI.UnitTests.Services
{
    public class MessageBoxServiceTests
    {
        [Fact]
        public void MessageBoxService_CanBeInstantiated()
        {
            // Integration tests for blocking UI components like MessageBox 
            // should be handled via UI Automation frameworks.
            // This test simply verifies the class can be instantiated for the IoC container.
            var service = new MessageBoxService();
            Assert.NotNull(service);
        }
    }
}