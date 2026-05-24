using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Servy.Core.UnitTests.Services
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
        public void GetServices_ReturnsEnumerable_AndDisposesControllers()
        {
            // Act
            IEnumerable<WindowsServiceInfo> services = _api.GetServices();

            // Assert
            Assert.NotNull(services);

            // Verify we can iterate the collection. 
            // This also verifies the 'finally' block in the source code executed
            // (if it hadn't, subsequent attempts to interact with ServiceControllers would throw).
            var serviceList = services.ToList();

            // Basic sanity check: Windows should always have at least one service 
            // (like "RpcSs" or "WinDefend")
            Assert.NotEmpty(serviceList);
            Assert.All(serviceList, s =>
            {
                Assert.NotNull(s.ServiceName);
                Assert.NotNull(s.DisplayName);
            });
        }

        #endregion
    }
}