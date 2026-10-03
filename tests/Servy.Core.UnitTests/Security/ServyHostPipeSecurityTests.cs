using Servy.Core.Config;
using Servy.Core.Security;
using Servy.Testing;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.UnitTests.Security
{
    /// <summary>
    /// Unit tests for the DACL of the Servy host named pipe.
    /// </summary>
    [Collection(Logging.LoggerCollection.Name)]
    public class ServyHostPipeSecurityTests
    {
        private static readonly SecurityIdentifier LocalSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier Administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier Network = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
        private static readonly SecurityIdentifier LocalService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
        private static readonly SecurityIdentifier NetworkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
        private static readonly SecurityIdentifier Everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        private static readonly SecurityIdentifier Users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        private static List<PipeAccessRule> Rules(PipeSecurity security)
            => security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();

        [Fact]
        public void Create_NoGrantee_IsProtectedAndOnlyLetsSystemAndAdministratorsIn()
        {
            // Act
            var security = ServyHostPipeSecurity.Create((IEnumerable<SecurityIdentifier>?)null);

            // Assert
            Assert.True(security.AreAccessRulesProtected);
            Assert.Null(security.GetOwner(typeof(SecurityIdentifier)));
            var rules = Rules(security);
            Assert.Equal(2, rules.Count);
            Assert.Contains(rules, r => LocalSystem.Equals(r.IdentityReference) && r.AccessControlType == AccessControlType.Allow && r.PipeAccessRights == PipeAccessRights.FullControl);
            Assert.Contains(rules, r => Administrators.Equals(r.IdentityReference) && r.AccessControlType == AccessControlType.Allow && r.PipeAccessRights == PipeAccessRights.FullControl);
            // Network logons are allowed (#7330): nothing denies NT AUTHORITY\NETWORK, and no rule denies anything
            Assert.DoesNotContain(rules, r => Network.Equals(r.IdentityReference));
            Assert.DoesNotContain(rules, r => r.AccessControlType == AccessControlType.Deny);
        }

        [Fact]
        public void Create_ServiceAccount_GetsReadWriteButCannotCreateAnInstanceOrChangeTheAcl()
        {
            // Act
            var security = ServyHostPipeSecurity.Create(new[] { LocalService });

            // Assert
            var grant = Assert.Single(Rules(security), r => LocalService.Equals(r.IdentityReference));
            Assert.Equal(AccessControlType.Allow, grant.AccessControlType);
            Assert.Equal(PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, grant.PipeAccessRights);
            Assert.Equal(0, (int)(grant.PipeAccessRights & PipeAccessRights.CreateNewInstance));
            Assert.Equal(0, (int)(grant.PipeAccessRights & PipeAccessRights.ChangePermissions));
            Assert.Equal(0, (int)(grant.PipeAccessRights & PipeAccessRights.TakeOwnership));
            Assert.Equal(ServyHostPipeSecurity.ClientRights, grant.PipeAccessRights);
        }

        [Fact]
        public void Create_PrivilegedOrBroadGrantees_AreNotAddedAgain()
        {
            // Act
            var security = ServyHostPipeSecurity.Create(new[] { LocalSystem, Administrators, Everyone, Users, NetworkService, null! });

            // Assert: only NetworkService gets a client grant
            var rules = Rules(security);
            Assert.Equal(3, rules.Count);
            Assert.DoesNotContain(rules, r => Everyone.Equals(r.IdentityReference) || Users.Equals(r.IdentityReference));
            Assert.Single(rules, r => NetworkService.Equals(r.IdentityReference));
        }

        [Fact]
        public void Create_Accounts_ResolvesTheServiceAccountsOnly()
        {
            // Arrange
            var resolver = new Dictionary<string, SecurityIdentifier?>(StringComparer.OrdinalIgnoreCase)
            {
                [@"NT AUTHORITY\LocalService"] = LocalService,
                [@".\svc"] = NetworkService,
            };

            // Act
            var security = ServyHostPipeSecurity.Create(
                new[] { "LocalSystem", @"NT AUTHORITY\LocalService", @" .\svc ", "  ", null! },
                a => resolver.TryGetValue(a, out var sid) ? sid : null);

            // Assert
            var rules = Rules(security);
            Assert.Single(rules, r => LocalService.Equals(r.IdentityReference));
            Assert.Single(rules, r => NetworkService.Equals(r.IdentityReference));
            Assert.Equal(4, rules.Count);
        }

        [Fact]
        public void ResolveGrantees_UnresolvableAndBroadAccounts_AreLoggedAndSkipped()
        {
            // Act
            var (grantees, log) = LogCapture.Run(() => ServyHostPipeSecurity.ResolveGrantees(
                new[] { "ghost", "Everyone", "dup-a", "dup-b" },
                a => a == "ghost" ? null : a == "Everyone" ? Everyone : LocalService));

            // Assert: two spellings of one account give one grant
            Assert.Equal(new[] { LocalService }, grantees);
            Assert.Contains("The account 'ghost' could not be resolved", log);
            Assert.Contains("The account 'Everyone' is a broad group", log);
        }

        [Fact]
        public void ResolveGrantees_AccountResolvingToSystemOrAdministrators_IsSkipped()
        {
            var grantees = ServyHostPipeSecurity.ResolveGrantees(new[] { "a", "b" }, a => a == "a" ? LocalSystem : Administrators);

            Assert.Empty(grantees);
        }

        [Fact]
        public void CreateAndResolveGrantees_NullResolver_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => ServyHostPipeSecurity.Create(new[] { "a" }, null!));
            Assert.Throws<ArgumentNullException>(() => ServyHostPipeSecurity.ResolveGrantees(new[] { "a" }, null!));
        }

        [Fact]
        public void AccountSidResolver_BlankOrUnknown_ReturnsNull()
        {
            Assert.Null(AccountSidResolver.Resolve(null));
            Assert.Null(AccountSidResolver.Resolve("   "));
            Assert.Null(AccountSidResolver.Resolve("definitely-not-an-account-7c1f"));
        }

        public static TheoryData<string, string> BuiltInServiceAccountSpellings()
        {
            var data = new TheoryData<string, string>();
            foreach (var alias in ServiceAccounts.LocalSystemAliases) data.Add(alias, "S-1-5-18");
            foreach (var alias in ServiceAccounts.LocalServiceAliases) data.Add(alias, "S-1-5-19");
            foreach (var alias in ServiceAccounts.NetworkServiceAliases) data.Add(alias, "S-1-5-20");
            return data;
        }

        [Theory]
        [MemberData(nameof(BuiltInServiceAccountSpellings))]
        public void AccountSidResolver_EveryBuiltInServiceAccountSpelling_ResolvesToItsWellKnownSid(string account, string expectedSid)
        {
            // Act
            var sid = AccountSidResolver.Resolve(account);

            // Assert
            Assert.Equal(new SecurityIdentifier(expectedSid), sid);
        }

        [Fact]
        public void AccountSidResolver_WellKnownAccount_ResolvesToItsSid()
        {
            Assert.Equal(LocalService, AccountSidResolver.Resolve(@" NT AUTHORITY\LocalService "));
        }
    }
}
