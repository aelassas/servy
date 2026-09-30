using Servy.Core.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Servy.Core.UnitTests.Config
{
    public class ServiceAccountsTests
    {
        #region Parity Tests

        [Fact]
        public void RunnableServiceAccounts_ParityWithServyDumpScript_MatchesCanonicalSet()
        {
            // Arrange
            string scriptPath = Testing.Helper.GetServyDumpPs1Path();
            Assert.True(File.Exists(scriptPath), $"Target script missing at path: {scriptPath}");

            string scriptText = File.ReadAllText(scriptPath);

            // Extract the foreach ($a in @( ... )) alias list feeding $script:runnableServiceAccounts
            var match = Regex.Match(
                scriptText,
                @"\$script:runnableServiceAccounts\s*=.*?foreach\s*\(\$a\s+in\s+@\((?<content>.*?)\)\)",
                RegexOptions.Singleline);

            Assert.True(match.Success, "Failed to locate the $script:runnableServiceAccounts alias list in Servy-Dump.ps1");

            string arrayContent = match.Groups["content"].Value;

            // The alias list is single-quoted throughout, so a single-quote capture keeps values
            // such as '.\Local System' intact where a whitespace-excluding pattern would not.
            var scriptAliases = Regex.Matches(arrayContent, @"'(?<name>[^']+)'")
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var canonicalAliases = ServiceAccounts.RunnableServiceAccounts;

            // Act
            var missingInScript = canonicalAliases.Except(scriptAliases, StringComparer.OrdinalIgnoreCase).ToList();
            var extraInScript = scriptAliases.Except(canonicalAliases, StringComparer.OrdinalIgnoreCase).ToList();

            // Assert
            Assert.True(
                missingInScript.Count == 0,
                $"Servy-Dump.ps1 is missing aliases defined in ServiceAccounts.cs: {string.Join(", ", missingInScript)}");

            Assert.True(
                extraInScript.Count == 0,
                $"Servy-Dump.ps1 contains aliases not present in ServiceAccounts.cs: {string.Join(", ", extraInScript)}");
        }

        #endregion

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

            // Completeness: nothing in the union comes from anywhere but the three sets. The count
            // assertion this replaces compared Count against the sum of the three counts, which is a
            // disjointness check under a completeness name (see the dedicated test below).
            var expected = ServiceAccounts.LocalSystemAliases
                .Union(ServiceAccounts.LocalServiceAliases)
                .Union(ServiceAccounts.NetworkServiceAliases);

            Assert.True(ServiceAccounts.RunnableServiceAccounts.SetEquals(expected));
        }

        [Fact]
        public void AliasSets_ArePairwiseDisjoint()
        {
            // Assert
            // RunnableServiceAccounts.Count == the sum of the three source counts only while the sets
            // are pairwise disjoint. Asserting it here names the duplicated alias when it breaks,
            // where the count comparison reported only "Expected 25, Actual 24".
            AssertDisjoint(
                nameof(ServiceAccounts.LocalSystemAliases), ServiceAccounts.LocalSystemAliases,
                nameof(ServiceAccounts.LocalServiceAliases), ServiceAccounts.LocalServiceAliases);
            AssertDisjoint(
                nameof(ServiceAccounts.LocalSystemAliases), ServiceAccounts.LocalSystemAliases,
                nameof(ServiceAccounts.NetworkServiceAliases), ServiceAccounts.NetworkServiceAliases);
            AssertDisjoint(
                nameof(ServiceAccounts.LocalServiceAliases), ServiceAccounts.LocalServiceAliases,
                nameof(ServiceAccounts.NetworkServiceAliases), ServiceAccounts.NetworkServiceAliases);
        }

        private static void AssertDisjoint(
            string leftName, IEnumerable<string> left,
            string rightName, IEnumerable<string> right)
        {
            var shared = left.Intersect(right, StringComparer.OrdinalIgnoreCase).ToList();

            Assert.True(shared.Count == 0,
                $"{leftName} and {rightName} must not share an alias; shared: {string.Join(", ", shared)}.");
        }

        /// <summary>
        /// Every alias in the union, so a newly added one is covered without editing a hand-listed theory.
        /// </summary>
        public static TheoryData<string> RunnableAliases()
        {
            var data = new TheoryData<string>();

            foreach (var alias in ServiceAccounts.RunnableServiceAccounts)
            {
                data.Add(alias);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(RunnableAliases))]
        public void IsBuiltInServiceAccount_EveryRunnableAlias_ReturnsTrue(string alias)
        {
            // Act
            bool result = ServiceAccounts.IsBuiltInServiceAccount(alias);

            // Assert
            Assert.True(result, $"'{alias}' is in RunnableServiceAccounts but is not recognised as built-in.");
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
        [InlineData("Local System", true)]
        [InlineData(@".\LocalSystem", true)]
        [InlineData(@".\System", true)]
        [InlineData(@".\Local System", true)]
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
        [InlineData(@"  Local System  ", true)]
        // Virtual & AppPool Accounts
        [InlineData(@"NT SERVICE\MyService", true)]
        [InlineData(@"nt service\foobar", true)]
        [InlineData(@"IIS APPPOOL\MyAppPool", true)]
        [InlineData(@"iis apppool\DefaultAppPool", true)]
        // Bare virtual-account prefixes: no account name after the backslash, the shape a trailing-
        // backslash paste produces. StartsWith accepts them today, so these rows record the current
        // classification rather than endorse it.
        [InlineData(@"NT SERVICE\", true)]
        [InlineData(@"IIS APPPOOL\", true)]
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

        #region IsEligibleForServiceControlGrant Tests

        [Theory]
        // Null / Empty / Whitespace
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        // Virtual accounts: the per-service SID belongs to this service alone, so the grant is safe.
        [InlineData(@"NT SERVICE\MyService", true)]
        [InlineData(@"nt service\foobar", true)]
        [InlineData(@"  NT SERVICE\MyService  ", true)]
        // Nothing after the prefix is a paste artefact, not an account with a SID to grant to.
        [InlineData(@"NT SERVICE\", false)]
        // Shared built-in runners: a grant would let every service using them control this one.
        [InlineData(@"NT AUTHORITY\LocalService", false)]
        [InlineData(@"NT AUTHORITY\NetworkService", false)]
        [InlineData("LocalService", false)]
        [InlineData("NetworkService", false)]
        // LocalSystem already holds full control over its own service.
        [InlineData("LocalSystem", false)]
        [InlineData(@"NT AUTHORITY\SYSTEM", false)]
        // An IIS AppPool identity is accepted as a log-on account, but its SID is shared by the pool's worker processes.
        [InlineData(@"IIS APPPOOL\MyAppPool", false)]
        [InlineData(@"IIS APPPOOL\", false)]
        // Custom accounts were already eligible and stay so.
        [InlineData(@"DOMAIN\SomeUser", true)]
        [InlineData(@".\CustomUser", true)]
        [InlineData("Administrator", true)]
        public void IsEligibleForServiceControlGrant_EvaluatesCorrectly(string account, bool expected)
        {
            // Act
            bool result = ServiceAccounts.IsEligibleForServiceControlGrant(account);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsEligibleForServiceControlGrant_VirtualAccount_IsTheOneBuiltInFormThatIsEligible()
        {
            // Arrange
            // A virtual account is a built-in OS-managed identity, so the two predicates deliberately
            // disagree about it: SID translation and the LSA privilege path still skip it, while the
            // service control grant is written for it. This pins that difference, which is the whole
            // fix for #7225 - without it RestartService fails with Access Denied under NT SERVICE\.
            var virtualAccount = @"NT SERVICE\MyService";
            var sharedAccount = ServiceAccounts.LocalService;

            // Act
            var virtualIsBuiltIn = ServiceAccounts.IsBuiltInServiceAccount(virtualAccount);
            var virtualIsEligible = ServiceAccounts.IsEligibleForServiceControlGrant(virtualAccount);
            var sharedIsBuiltIn = ServiceAccounts.IsBuiltInServiceAccount(sharedAccount);
            var sharedIsEligible = ServiceAccounts.IsEligibleForServiceControlGrant(sharedAccount);

            // Assert
            Assert.True(virtualIsBuiltIn);
            Assert.True(virtualIsEligible);
            Assert.True(sharedIsBuiltIn);
            Assert.False(sharedIsEligible);
        }

        [Theory]
        [InlineData(@"NT AUTHORITY\LocalService")]
        [InlineData(@"NT AUTHORITY\NetworkService")]
        [InlineData("LocalService")]
        [InlineData("NetworkService")]
        [InlineData(@"BUILTIN\LocalService")]
        [InlineData(@"BUILTIN\NetworkService")]
        [InlineData(@".\Local Service")]
        [InlineData(@".\Network Service")]
        public void IsEligibleForServiceControlGrant_EverySharedRunnerAlias_ReturnsFalse(string alias)
        {
            // Act
            // Every documented spelling of the two shared runners has to be refused, not just the
            // canonical one: a grant reached through an alias is as shared as one reached directly.
            bool result = ServiceAccounts.IsEligibleForServiceControlGrant(alias);

            // Assert
            Assert.False(result);
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

        #region ExpandLocalShorthand Tests

        [Fact]
        public void ExpandLocalShorthand_LeadingDotBackslash_ReplacesPrefixWithMachineName()
        {
            // Arrange
            var account = @".\svc_user";

            // Act
            var result = ServiceAccounts.ExpandLocalShorthand(account);

            // Assert
            Assert.Equal(Environment.MachineName + @"\svc_user", result);
        }

        [Theory]
        [InlineData(@"DOMAIN\svc_user")]          // already qualified
        [InlineData("svc_user")]                  // bare name
        [InlineData(@"NT AUTHORITY\LocalService")] // built-in alias
        [InlineData(@"..\svc_user")]              // not the shorthand
        [InlineData(@"x.\svc_user")]              // prefix is not leading
        [InlineData("")]                          // nothing to expand
        public void ExpandLocalShorthand_WithoutLeadingShorthand_ReturnsInputUnchanged(string account)
        {
            // Act
            var result = ServiceAccounts.ExpandLocalShorthand(account);

            // Assert
            Assert.Equal(account, result);
        }

        [Fact]
        public void ExpandLocalShorthand_IsTheSingleRuleSharedByValidationAndGrant()
        {
            // Arrange
            // The credential validation gate and the "Log on as a service" grant path both expand
            // the shorthand through this helper. Their two former hand-written spellings are pinned
            // here so a change to the rule cannot silently agree with only one of them.
            var account = @".\svc_user";
            var validationGateShape = Environment.MachineName + account.Substring(1);
            var grantPathShape = $"{Environment.MachineName}\\{account.Substring(2)}";

            // Act
            var result = ServiceAccounts.ExpandLocalShorthand(account);

            // Assert
            Assert.Equal(validationGateShape, result);
            Assert.Equal(grantPathShape, result);
        }

        #endregion
    }
}
