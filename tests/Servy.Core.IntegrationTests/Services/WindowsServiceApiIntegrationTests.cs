using Servy.Core.Services;

namespace Servy.Core.IntegrationTests.Services
{
    // Reuse the sequential collection to ensure OS-level SCM/LSA interactions don't conflict
    [Collection("WindowsServiceApiIntegrationTests")]
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
            IEnumerable<WindowsServiceInfo> services = _api.GetServices();

            // Assert
            Assert.NotNull(services);

            // Materialize the collection to assert basic hydration
            var serviceList = services.ToList();

            // Basic sanity check: Windows should always have at least one service 
            Assert.NotEmpty(serviceList);
            Assert.All(serviceList, s =>
            {
                // Asserting against string properties only validates mapping accuracy, not handle tracking
                Assert.False(string.IsNullOrWhiteSpace(s.ServiceName), "Service name was omitted or whitespace.");
                Assert.False(string.IsNullOrWhiteSpace(s.DisplayName), "Display name was omitted or whitespace.");
            });
        }

        #endregion
    }
}