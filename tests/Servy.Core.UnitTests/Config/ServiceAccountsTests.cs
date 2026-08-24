using Servy.Core.Config;
using Xunit;

namespace Servy.Core.UnitTests.Config
{
    public class ServiceAccountsTests
    {
        #region Constants & Collections Tests

        [Fact]
        public void Constants_HaveExpectedValues()
        {
            // Assert
            Assert.Equal("LocalSystem", ServiceAccounts.LocalSystem);
            Assert.Equal(@"NT AUTHORITY\LocalService", ServiceAccounts.LocalService);
            Assert.Equal(@"NT AUTHORITY\NetworkService", ServiceAccounts.NetworkService);
        }

        [Fact]
        public void RunnableServiceAccounts_ContainsAllAliasesFromAllThreeSets()
        {
            // Assert
            Assert.True(ServiceAccounts.RunnableServiceAccounts.IsSupersetOf(ServiceAccounts.LocalSystemAliases));
            Assert.True(ServiceAccounts.RunnableServiceAccounts.IsSupersetOf(ServiceAccounts.LocalServiceAliases));
            Assert.True(ServiceAccounts.RunnableServiceAccounts.IsSupersetOf(ServiceAccounts.NetworkServiceAliases));

            int totalExpectedCount = ServiceAccounts.LocalSystemAliases.Count +
                                     ServiceAccounts.LocalServiceAliases.Count +
                                     ServiceAccounts.NetworkServiceAliases.Count;

            Assert.Equal(totalExpectedCount, ServiceAccounts.RunnableServiceAccounts.Count);
        }

        #endregion

        #region IsBuiltInServiceAccount Tests

        [Theory]
        // Null / Empty / Whitespace
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        // Standard LocalSystem Aliases
        [InlineData("LocalSystem", true)]
        [InlineData("System", true)]
        [InlineData(@".\LocalSystem", true)]
        [InlineData(@".\System", true)]
        [InlineData(@"NT AUTHORITY\LocalSystem", true)]
        [InlineData(@"NT AUTHORITY\Local System", true)]
        [InlineData(@"NT AUTHORITY\SYSTEM", true)]
        [InlineData(@"BUILTIN\LocalSystem", true)]
        [InlineData(@"BUILTIN\System", true)]
        // Standard LocalService Aliases
        [InlineData("LocalService", true)]
        [InlineData(@"NT AUTHORITY\LocalService", true)]
        [InlineData(@".\LocalService", true)]
        [InlineData(@".\Local Service", true)]
        [InlineData(@"NT AUTHORITY\Local Service", true)]
        [InlineData("Local Service", true)]
        [InlineData(@"BUILTIN\LocalService", true)]
        // Standard NetworkService Aliases
        [InlineData("NetworkService", true)]
        [InlineData(@"NT AUTHORITY\NetworkService", true)]
        [InlineData(@".\NetworkService", true)]
        [InlineData(@".\Network Service", true)]
        [InlineData(@"NT AUTHORITY\Network Service", true)]
        [InlineData("Network Service", true)]
        [InlineData(@"BUILTIN\NetworkService", true)]
        // Case-insensitivity & Padding
        [InlineData(@".\networkservice", true)]
        [InlineData(@"  .\NetworkService  ", true)]
        [InlineData(@"nt authority\localsystem", true)]
        // Virtual & AppPool Accounts
        [InlineData(@"NT SERVICE\MyService", true)]
        [InlineData(@"nt service\foobar", true)]
        [InlineData(@"IIS APPPOOL\MyAppPool", true)]
        [InlineData(@"iis apppool\DefaultAppPool", true)]
        // Standard Domain / Local User Accounts (Not Built-In)
        [InlineData(@"DOMAIN\SomeUser", false)]
        [InlineData(@".\CustomUser", false)]
        [InlineData(@"Administrator", false)]
        [InlineData(@"NT AUTHORITY\Authenticated Users", false)]
        public void IsBuiltInServiceAccount_EvaluatesCorrectly(string account, bool expected)
        {
            // Act
            bool result = ServiceAccounts.IsBuiltInServiceAccount(account);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region IsGmsa Tests

        [Theory]
        // Valid gMSA cases
        [InlineData(@"DOMAIN\svc_gmsa$", null, true)]
        [InlineData(@"DOMAIN\svc_gmsa$", "", true)]
        [InlineData(@"svc_gmsa$", null, true)]
        [InlineData(@"  DOMAIN\svc_gmsa$  ", "", true)]
        [InlineData(@"DOMAIN\svc_gmsa$", "   ", true)] // whitespace-only password is still "no password"
        [InlineData(@"DOMAIN\svc_gmsa$", "\t", true)]
        // Invalid gMSA cases (has password)
        [InlineData(@"DOMAIN\svc_gmsa$", "SecretPassword123!", false)]
        // Invalid gMSA cases (does not end with $)
        [InlineData(@"DOMAIN\StandardUser", null, false)]
        [InlineData(@"DOMAIN\StandardUser", "", false)]
        // Invalid gMSA cases (null / empty / whitespace account)
        [InlineData(null, null, false)]
        [InlineData("", null, false)]
        [InlineData("   ", null, false)]
        public void IsGmsa_EvaluatesCorrectly(string account, string password, bool expected)
        {
            // Act
            bool result = ServiceAccounts.IsGmsa(account, password);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
