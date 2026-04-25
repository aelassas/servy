using Servy.UI.Services;
using Xunit;

namespace Servy.UI.UnitTests.Services
{
    public class FileDialogServiceTests
    {
        [Fact]
        public void FileDialogService_CanBeInstantiated()
        {
            // Integration tests for UI dialogs (OpenFileDialog, SaveFileDialog)
            // should be handled via UI Automation (e.g., FlaUI or WinAppDriver)
            // rather than unit tests, as they invoke blocking OS-level dialogs.
            // This test ensures the service can be instantiated for DI.
            var service = new FileDialogService();
            Assert.NotNull(service);
        }
    }
}