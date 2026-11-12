using Servy.Core.Services;

namespace Servy.Core.IntegrationTests.Services
{
    // Reuse the sequential collection to ensure OS-level SCM/LSA interactions don't conflict
    [Collection(CoreOsIntegrationCollection.Name)]
    public class WindowsServiceApiIntegrationTests
    {
        private readonly WindowsServiceApi _api;

        public WindowsServiceApiIntegrationTests()
        {
            _api = new WindowsServiceApi();
        }

        #region EnsureLogOnAsServiceRight Tests

        [Fact]
        public void EnsureLogOnAsServiceRight_InvalidAccountName_ThrowsInvalidOperationException()
        {
            // Arrange
            // This tests the branch that flows through to LogonAsServiceGrant.Ensure.
            // We pass a dummy invalid name to trigger the underlying SID resolution failure.
            string invalidAccount = "NonExistentAccount_" + Guid.NewGuid().ToString("N");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _api.EnsureLogOnAsServiceRight(invalidAccount));

            Assert.Contains("Cannot resolve SID", ex.Message);
        }

        #endregion

        #region GetServices Tests

        [Fact]
        public void GetServices_ReturnsEnumerableOfWindowsServiceInfo()
        {
            // Act
            var serviceList = _api.GetServices().ToList();

            // Assert
            // Basic sanity check: Windows should always have at least one service
            Assert.NotEmpty(serviceList);
            Assert.All(serviceList, s =>
            {
                Assert.False(string.IsNullOrWhiteSpace(s.ServiceName), "A service was enumerated with an omitted or whitespace ServiceName.");
                Assert.False(string.IsNullOrWhiteSpace(s.DisplayName), $"Service '{s.ServiceName}' was enumerated with an omitted or whitespace DisplayName.");
            });
        }

        #endregion

        #region GetServiceProcessId Tests

        [Fact]
        public void GetServiceProcessId_RunningService_ReturnsTheIdOfItsHostProcess()
        {
            // Arrange
            // The Windows Event Log service runs on every supported Windows and cannot be stopped
            // while the machine is up.
            const string runningService = "EventLog";

            // Act
            int pid = _api.GetServiceProcessId(runningService);

            // Assert
            // A wrong struct layout, access right or field makes this 0 or the id of no svchost.
            Assert.True(pid > 0, $"Expected a process id for '{runningService}', got {pid}.");
            using (var process = System.Diagnostics.Process.GetProcessById(pid))
            {
                Assert.Equal("svchost", process.ProcessName, ignoreCase: true);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetServiceProcessId_BlankName_ReturnsZero(string? serviceName)
        {
            // Arrange, Act & Assert
            Assert.Equal(0, _api.GetServiceProcessId(serviceName!));
        }

        [Fact]
        public void GetServiceProcessId_UnknownService_ReturnsZero()
        {
            // Arrange
            string unknown = "ServyNoSuchService_" + Guid.NewGuid().ToString("N");

            // Act
            int pid = _api.GetServiceProcessId(unknown);

            // Assert
            Assert.Equal(0, pid);
        }

        #endregion
    }
}
