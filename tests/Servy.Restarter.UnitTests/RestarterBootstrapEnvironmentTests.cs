using Servy.Restarter.Bootstrap;
using System;
using Xunit;

namespace Servy.Restarter.UnitTests
{
    /// <summary>
    /// Tests <see cref="RestarterBootstrapEnvironment.GetServiceImagePath"/> against the real registry, the one
    /// member of the bootstrap seam that decides something instead of forwarding.
    /// </summary>
    public class RestarterBootstrapEnvironmentTests
    {
        [Fact]
        public void GetServiceImagePath_InstalledService_ReturnsItsCommandLine()
        {
            // Arrange
            var environment = new RestarterBootstrapEnvironment();

            // Act
            // "EventLog" is installed on every Windows machine and its key is readable by every account.
            var imagePath = environment.GetServiceImagePath("EventLog");

            // Assert
            Assert.NotNull(imagePath);
            Assert.Contains("svchost.exe", imagePath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetServiceImagePath_ServiceThatIsNotInstalled_ReturnsNull()
        {
            // Arrange
            var environment = new RestarterBootstrapEnvironment();
            var missing = "Servy_NoSuchService_" + Guid.NewGuid().ToString("N");

            // Act
            var imagePath = environment.GetServiceImagePath(missing);

            // Assert
            Assert.Null(imagePath);
        }
    }
}
