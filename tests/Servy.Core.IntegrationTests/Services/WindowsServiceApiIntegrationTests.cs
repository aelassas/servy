using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Servy.Core.IntegrationTests.Services
{
    // Reuse the sequential collection to ensure OS-level SCM/LSA interactions don't conflict
    [Collection("CoreOsIntegration")]
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
    }
}
