using Servy.Core.Services;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System.Security.AccessControl;
using System.Security.Principal;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Covers <see cref="WindowsServiceApi.BuildGrantedDacl"/>, the discretionary access control list (DACL) edit
    /// that <see cref="WindowsServiceApi.GrantServiceControlRights"/> applies to a service's security descriptor.
    /// The NULL DACL case is the #7234 fix: a missing DACL already grants every principal full access, so replacing
    /// it with a one-entry list would lock Administrators and SYSTEM out of Start, Stop and Delete. The populated
    /// case pins that an administrator's Deny entry and other accounts' entries survive, that the account's own
    /// Allow entries are replaced rather than duplicated, and that the inserted entry carries exactly
    /// <c>SERVICE_CONTROL_AND_STATUS_ACCESS</c>.
    /// <para>
    /// Also covers <see cref="WindowsServiceApi.RemoveAllowAces"/> directly, the helper both the grant and the
    /// revocation share: it carries the #7221 rule that only the account's Allow entries are removed, and its
    /// return value is what tells <see cref="WindowsServiceApi.RevokeServiceControlRights"/> whether there was
    /// anything to write back.
    /// </para>
    /// </summary>
    /// <remarks>
    /// In <see cref="LoggerCollection"/> because the NULL DACL case asserts through
    /// <see cref="LogCapture"/>, which shuts the static <c>Logger</c> down and re-initialises it against a
    /// directory of its own. That is process-wide state, so a class asserting on it must not run in parallel
    /// with another class doing the same: the two captures swap the writer under each other and the loser
    /// reads an empty file. Every other class in this assembly that uses <see cref="LogCapture"/> carries the
    /// same attribute for the same reason.
    /// </remarks>
    [Collection(LoggerCollection.Name)] // the NULL DACL warning is asserted through the static Logger
    public class WindowsServiceApiTests
    {
        private const string AccountName = @".\svc-account";

        private static readonly SecurityIdentifier Target =
            new SecurityIdentifier("S-1-5-21-1000-1000-1000-1001");

        private static readonly SecurityIdentifier Other =
            new SecurityIdentifier("S-1-5-21-1000-1000-1000-1002");

        [Fact]
        public void BuildGrantedDacl_NullDacl_ReturnsNullAndWarnsSoNothingIsWrittenBack()
        {
            // Arrange, Act
            var capture = LogCapture.Run(() => WindowsServiceApi.BuildGrantedDacl(null, Target, AccountName));

            // Assert
            Assert.Null(capture.Result);
            Assert.Contains("NULL DACL", capture.Log);
            Assert.Contains(AccountName, capture.Log);
        }

        [Fact]
        public void BuildGrantedDacl_ExistingDacl_KeepsDenyAndOtherAccounts_AndReplacesTheAccountsAllowEntries()
        {
            // Arrange
            var acl = new RawAcl(GenericAcl.AclRevision, 3);
            acl.InsertAce(0, Ace(AceQualifier.AccessDenied, 0x20, Target));
            acl.InsertAce(1, Ace(AceQualifier.AccessAllowed, 0x14, Target));
            acl.InsertAce(2, Ace(AceQualifier.AccessAllowed, 0x1F0, Other));

            // Act
            var result = WindowsServiceApi.BuildGrantedDacl(acl, Target, AccountName);

            // Assert
            Assert.Same(acl, result);
            Assert.Equal(3, result!.Count);

            var deny = Assert.IsType<CommonAce>(result[0]);
            Assert.Equal(AceQualifier.AccessDenied, deny.AceQualifier);
            Assert.Equal(Target, deny.SecurityIdentifier);
            Assert.Equal(0x20, deny.AccessMask);

            var untouched = Assert.IsType<CommonAce>(result[1]);
            Assert.Equal(AceQualifier.AccessAllowed, untouched.AceQualifier);
            Assert.Equal(Other, untouched.SecurityIdentifier);
            Assert.Equal(0x1F0, untouched.AccessMask);

            var granted = Assert.IsType<CommonAce>(result[2]);
            Assert.Equal(AceQualifier.AccessAllowed, granted.AceQualifier);
            Assert.Equal(Target, granted.SecurityIdentifier);
            Assert.Equal((int)SERVICE_CONTROL_AND_STATUS_ACCESS, granted.AccessMask);
        }

        [Fact]
        public void BuildGrantedDacl_AccountHasNoEntryYet_AppendsOneControlEntryAtTheEnd()
        {
            // Arrange
            var acl = new RawAcl(GenericAcl.AclRevision, 1);
            acl.InsertAce(0, Ace(AceQualifier.AccessAllowed, 0x1F0, Other));

            // Act
            var result = WindowsServiceApi.BuildGrantedDacl(acl, Target, AccountName);

            // Assert
            Assert.Same(acl, result);
            Assert.Equal(2, result!.Count);
            Assert.Equal(Other, Assert.IsType<CommonAce>(result[0]).SecurityIdentifier);

            var granted = Assert.IsType<CommonAce>(result[1]);
            Assert.Equal(Target, granted.SecurityIdentifier);
            Assert.Equal((int)SERVICE_CONTROL_AND_STATUS_ACCESS, granted.AccessMask);
        }

        [Fact]
        public void RemoveAllowAces_RemovesOnlyTheAccountsAllowEntries_KeepsItsDenyAndOtherAccounts()
        {
            // Arrange
            var acl = new RawAcl(GenericAcl.AclRevision, 4);
            acl.InsertAce(0, Ace(AceQualifier.AccessDenied, 0x20, Target));
            acl.InsertAce(1, Ace(AceQualifier.AccessAllowed, 0x1F0, Target));
            acl.InsertAce(2, Ace(AceQualifier.AccessAllowed, 0x1F0, Other));
            acl.InsertAce(3, Ace(AceQualifier.AccessAllowed, 0x14, Target));

            // Act
            var removed = WindowsServiceApi.RemoveAllowAces(acl, Target);

            // Assert
            Assert.Equal(2, removed);
            Assert.Equal(2, acl.Count);

            var deny = Assert.IsType<CommonAce>(acl[0]);
            Assert.Equal(AceQualifier.AccessDenied, deny.AceQualifier);
            Assert.Equal(Target, deny.SecurityIdentifier);

            var kept = Assert.IsType<CommonAce>(acl[1]);
            Assert.Equal(AceQualifier.AccessAllowed, kept.AceQualifier);
            Assert.Equal(Other, kept.SecurityIdentifier);
        }

        [Fact]
        public void RemoveAllowAces_AccountHasNoAllowEntry_ReturnsZeroAndLeavesTheListUnchanged()
        {
            // Arrange
            var acl = new RawAcl(GenericAcl.AclRevision, 2);
            acl.InsertAce(0, Ace(AceQualifier.AccessDenied, 0x20, Target));
            acl.InsertAce(1, Ace(AceQualifier.AccessAllowed, 0x1F0, Other));

            // Act
            var removed = WindowsServiceApi.RemoveAllowAces(acl, Target);

            // Assert
            Assert.Equal(0, removed);
            Assert.Equal(2, acl.Count);
            Assert.Equal(Target, Assert.IsType<CommonAce>(acl[0]).SecurityIdentifier);
            Assert.Equal(Other, Assert.IsType<CommonAce>(acl[1]).SecurityIdentifier);
        }

        /// <summary>
        /// Creates an access control entry for one account, with no inheritance flags and no object type.
        /// </summary>
        /// <param name="qualifier">Whether the entry allows or denies the access.</param>
        /// <param name="mask">The access mask the entry carries.</param>
        /// <param name="sid">The account the entry applies to.</param>
        /// <returns>The new entry.</returns>
        private static CommonAce Ace(AceQualifier qualifier, int mask, SecurityIdentifier sid)
            => new CommonAce(AceFlags.None, qualifier, mask, sid, false, null);
    }
}
