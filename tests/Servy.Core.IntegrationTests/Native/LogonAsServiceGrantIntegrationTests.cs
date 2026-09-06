using Servy.Core.Native;
using Servy.Testing;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Servy.Core.IntegrationTests.Native
{
    [Collection("CoreOsIntegration")]
    public class LogonAsServiceGrantIntegrationTests : IDisposable
    {
        private const string NoLsaAccessSkipReason =
            "Skipping test: the run is not elevated or has no LSA policy access.";

        private readonly string _testAccountName;
        private readonly bool _canModifyLsaPolicy;
        private bool _provisioningAttempted;
        private bool _accountCreatedLocally;
        private string? _accountProvisioningError;

        // P/Invoke definition necessary to tear down matching LSA security descriptors before user purging
        [DllImport("advapi32.dll", PreserveSig = true)]
        private static extern int LsaRemoveAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            [MarshalAs(UnmanagedType.Bool)] bool AllRights,
            NativeMethods.LSA_UNICODE_STRING[] UserRights,
            uint Count);

        public LogonAsServiceGrantIntegrationTests()
        {
            // 1. Verify administrative execution level
            bool isAdministrator = Helper.IsAdministrator();

            // 2. CI GUARD: Verify if the current context has actual operational permissions to call LSA APIs.
            // On standard GitHub Actions cloud agents, this prevents cascading Access Denied (0xC0000022) runtime breaks.
            _canModifyLsaPolicy = isAdministrator && Helper.CheckLsaPolicyAccess();

            // Create a temporary, unique local user name. The account itself is provisioned lazily by
            // TryEnsureTestAccount, so the tests that never touch it pay neither the SAM round-trip nor
            // the commit wait - xUnit builds a fresh instance of this class for every test method.
            _testAccountName = "ServyTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Creates the transient local account on first use and waits for the SAM subsystem to commit it.
        /// </summary>
        /// <returns><c>true</c> when the account exists and the test may proceed.</returns>
        private bool TryEnsureTestAccount()
        {
            if (!_canModifyLsaPolicy || _provisioningAttempted) return _accountCreatedLocally;

            _provisioningAttempted = true;

            try
            {
                using (var context = new PrincipalContext(ContextType.Machine))
                using (var user = new UserPrincipal(context))
                {
                    user.Name = _testAccountName;
                    user.SetPassword(Guid.NewGuid().ToString("P") + "A1!");
                    user.Description = "Transient account for Servy LSA integration unit testing.";
                    user.Save();
                    _accountCreatedLocally = true;
                }
            }
            catch (Exception ex)
            {
                // Keep the reason: a skip that only says "access or creation failure" cannot tell a
                // permissions problem from a provisioning one.
                _accountProvisioningError = ex.Message;
                _accountCreatedLocally = false;
            }

            if (_accountCreatedLocally)
            {
                // Introduce a synchronization window to let the Windows SAM subsystem fully commit
                Thread.Sleep(500);
            }

            return _accountCreatedLocally;
        }

        /// <summary>
        /// The skip reason for a run that may modify the LSA policy but could not provision the account.
        /// </summary>
        private string AccountProvisioningSkipReason() =>
            "Skipping test: the transient local account could not be created " +
            $"({_accountProvisioningError ?? "no exception reported"}).";

        #region Account Parsing & SID Resolution Failure Branches

        [Fact]
        public void Ensure_NullOrWhitespaceAccountName_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            // This path relies strictly on string validation and runs safely anywhere, including CI.
            Assert.Throws<ArgumentException>("account", () => LogonAsServiceGrant.Ensure(null!));
            Assert.Throws<ArgumentException>("account", () => LogonAsServiceGrant.Ensure("    "));
        }

        [Fact]
        public void Ensure_NonExistentAccountName_ThrowsInvalidOperationExceptionWithDetailedContext()
        {
            // Arrange
            // On non-elevated or restricted environments like cloud CI workers, this test can safely execute
            // because resolving a non-existent fake string to a SID fails inside the framework NTAccount.Translate
            // mapping loop before it ever touches the native LSA privilege modification routines.
            string fakeAccount = "MachineNameOrDomain\\GhostUser_" + Guid.NewGuid().ToString("N");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => LogonAsServiceGrant.Ensure(fakeAccount));

            // Assert
            Assert.Contains($"Cannot resolve SID for '{fakeAccount}'", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        #endregion

        #region Local Machine Notation Translation Path

        [Fact]
        public void Ensure_ShorthandLocalNotation_CorrectlyTranslatesMachinePrefix()
        {
            // Arrange
            Assert.SkipUnless(_canModifyLsaPolicy, NoLsaAccessSkipReason);
            Assert.SkipUnless(TryEnsureTestAccount(), AccountProvisioningSkipReason());

            string shorthandAccount = $".\\{_testAccountName}";

            // Resolve the SID via the fully-qualified name - this is the target identity the shorthand must expand to.
            byte[] sidBytes = ResolveSidBytes(FullAccountName);

            Assert.DoesNotContain("SeServiceLogonRight", GetAccountRightsViaNativeMethods(sidBytes));

            // Act - drive the grant purely through the '.\' shorthand notation.
            Assert.Null(Record.Exception(() => LogonAsServiceGrant.Ensure(shorthandAccount)));

            // Assert - verify that the logon privilege was granted to the target account resolved by the shorthand.
            Assert.Contains("SeServiceLogonRight", GetAccountRightsViaNativeMethods(sidBytes));
        }

        #endregion

        #region Real LSA Lifecycle Operations

        [Fact]
        public void Ensure_FreshAccountWithoutAnyRights_TriggersNotFoundBranchAndGrantsPrivilege()
        {
            // Arrange
            Assert.SkipUnless(_canModifyLsaPolicy, NoLsaAccessSkipReason);
            Assert.SkipUnless(TryEnsureTestAccount(), AccountProvisioningSkipReason());

            string fullAccountName = FullAccountName;

            // Resolve the account name to a SecurityIdentifier (SID) to query LSA
            byte[] sidBytes = ResolveSidBytes(fullAccountName);

            // Assert baseline - The freshly created account must not hold the logon right prior to execution
            var initialRights = GetAccountRightsViaNativeMethods(sidBytes);
            Assert.DoesNotContain("SeServiceLogonRight", initialRights);

            // Act 1 - Execute first pass: captures the native 0xC0000034 status (Not Found) branch and grants privilege
            Exception? ex1 = Record.Exception(() => LogonAsServiceGrant.Ensure(fullAccountName));

            // Assert 1 - Verify execution succeeded and the privilege was physically written to the OS LSA policy database
            Assert.Null(ex1);
            var postGrantRights = GetAccountRightsViaNativeMethods(sidBytes);
            Assert.Contains("SeServiceLogonRight", postGrantRights);

            // Act 2 - Second call: the right is already present, so Ensure must return without granting again
            Exception? ex2 = Record.Exception(() => LogonAsServiceGrant.Ensure(fullAccountName));

            // Assert 2 - Verify no exceptions were raised, the privilege wasn't stripped, and structure remains unmodified
            Assert.Null(ex2);
            var postIdempotentRights = GetAccountRightsViaNativeMethods(sidBytes);
            Assert.Contains("SeServiceLogonRight", postIdempotentRights);

            // A second Ensure must not add a duplicate entry
            Assert.Equal(postGrantRights.Count, postIdempotentRights.Count);
        }

        [Fact]
        public void RevokeLsaPrivilegeBeforeDeletion_RemovesTheGrantItWasGiven()
        {
            // Arrange
            Assert.SkipUnless(_canModifyLsaPolicy, NoLsaAccessSkipReason);
            Assert.SkipUnless(TryEnsureTestAccount(), AccountProvisioningSkipReason());

            byte[] sidBytes = ResolveSidBytes(FullAccountName);

            // Act 1 - grant the right, so there is something to revoke
            LogonAsServiceGrant.Ensure(FullAccountName);

            // Assert 1 - baseline: the grant the teardown is responsible for removing is present
            Assert.Contains("SeServiceLogonRight", GetAccountRightsViaNativeMethods(sidBytes));

            // Act 2 - run the teardown step Dispose relies on
            RevokeLsaPrivilegeBeforeDeletion(_testAccountName, "SeServiceLogonRight");

            // Assert 2 - the grant is gone from the host LSA policy. Without this the revocation could be
            // reduced to an empty body and the suite would stay green, while the orphaned SID grants this
            // teardown was added to prevent would silently accumulate on whichever machine runs elevated.
            Assert.DoesNotContain("SeServiceLogonRight", GetAccountRightsViaNativeMethods(sidBytes));
        }

        #endregion

        #region Native Methods LSA Inspection Helper

        /// <summary>
        /// The machine-qualified name of the transient test account.
        /// </summary>
        private string FullAccountName => $"{Environment.MachineName}\\{_testAccountName}";

        /// <summary>
        /// Resolves an account name to the binary SID form the LSA APIs take.
        /// </summary>
        private static byte[] ResolveSidBytes(string accountName)
        {
            var sid = (SecurityIdentifier)new NTAccount(accountName).Translate(typeof(SecurityIdentifier));
            var sidBytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBytes, 0);
            return sidBytes;
        }

        /// <summary>
        /// Copies a binary SID into a freshly allocated unmanaged buffer. The caller owns the buffer.
        /// </summary>
        private static IntPtr AllocSidBuffer(byte[] sidBytes)
        {
            IntPtr buffer = Marshal.AllocHGlobal(sidBytes.Length);
            Marshal.Copy(sidBytes, 0, buffer, sidBytes.Length);
            return buffer;
        }

        private static List<string> GetAccountRightsViaNativeMethods(byte[] sidBytes)
        {
            var rightsList = new List<string>();

            var objectAttributes = new NativeMethods.LSA_OBJECT_ATTRIBUTES { Length = Marshal.SizeOf<NativeMethods.LSA_OBJECT_ATTRIBUTES>() };
            IntPtr policyHandle;

            // POLICY_LOOKUP_NAMES is the only access LsaEnumerateAccountRights needs. Requesting more
            // than that on the policy handle is what a hardened host is most likely to refuse, and a
            // refused LsaOpenPolicy is reported below as an empty rights list.
            uint desiredAccess = NativeMethods.POLICY_ACCESS.POLICY_LOOKUP_NAMES;

            int openStatus = NativeMethods.LsaOpenPolicy(IntPtr.Zero, ref objectAttributes, desiredAccess, out policyHandle);
            if (openStatus != 0) return rightsList; // LsaOpenPolicy failed; report no rights

            IntPtr rawSidAllocationPtr = AllocSidBuffer(sidBytes);
            try
            {
                IntPtr outUserRightsBufferPtr;
                uint countOfRights;

                // Enumerate the privileges assigned to this SID
                int enumStatus = NativeMethods.LsaEnumerateAccountRights(policyHandle, rawSidAllocationPtr, out outUserRightsBufferPtr, out countOfRights);

                // If status is 0 (STATUS_SUCCESS) and buffer is allocated, unmarshal the string values sequentially
                if (enumStatus == 0 && outUserRightsBufferPtr != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr currentElementOffsetPtr = outUserRightsBufferPtr;
                        int structSize = Marshal.SizeOf<NativeMethods.LSA_UNICODE_STRING>();

                        for (int i = 0; i < countOfRights; i++)
                        {
                            var unicodeString = Marshal.PtrToStructure<NativeMethods.LSA_UNICODE_STRING>(currentElementOffsetPtr);
                            if (unicodeString.Buffer != IntPtr.Zero && unicodeString.Length > 0)
                            {
                                // Length field contains bytes size. Divide by 2 to compute character boundary size
                                string rightName = Marshal.PtrToStringUni(unicodeString.Buffer, unicodeString.Length / 2);
                                rightsList.Add(rightName);
                            }
                            currentElementOffsetPtr = IntPtr.Add(currentElementOffsetPtr, structSize);
                        }
                    }
                    finally
                    {
                        NativeMethods.LsaFreeMemory(outUserRightsBufferPtr);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rawSidAllocationPtr);
                if (policyHandle != IntPtr.Zero) NativeMethods.LsaClose(policyHandle);
            }

            return rightsList;
        }

        #endregion

        /// <summary>
        /// Explicitly revokes rights from the target LSA account scope before account deletion to avoid host leakage.
        /// </summary>
        private void RevokeLsaPrivilegeBeforeDeletion(string accountName, string privilege)
        {
            IntPtr policyHandle = IntPtr.Zero;
            IntPtr sidBuffer = IntPtr.Zero;
            IntPtr nativeStringAlloc = IntPtr.Zero;

            try
            {
                // Resolve user domain string back to an active NT Security Identifier structure
                sidBuffer = AllocSidBuffer(ResolveSidBytes(accountName));

                var objectAttributes = new NativeMethods.LSA_OBJECT_ATTRIBUTES
                {
                    Length = Marshal.SizeOf<NativeMethods.LSA_OBJECT_ATTRIBUTES>()
                };

                int lsaOpenStatus = NativeMethods.LsaOpenPolicy(
                    IntPtr.Zero,
                    ref objectAttributes,
                    NativeMethods.POLICY_ACCESS.POLICY_LOOKUP_NAMES | NativeMethods.POLICY_ACCESS.POLICY_ASSIGN_PRIVILEGE,
                    out policyHandle);

                if (lsaOpenStatus == 0)
                {
                    var privilegeString = new NativeMethods.LSA_UNICODE_STRING();
                    nativeStringAlloc = Marshal.StringToHGlobalUni(privilege);

                    privilegeString.Buffer = nativeStringAlloc;
                    privilegeString.Length = (ushort)(privilege.Length * 2);
                    privilegeString.MaximumLength = (ushort)((privilege.Length + 1) * 2);

                    var rightsArray = new NativeMethods.LSA_UNICODE_STRING[] { privilegeString };

                    // Remove the privilege before the account is deleted, so no orphaned SID grant is left in LSA
                    int removeStatus = LsaRemoveAccountRights(policyHandle, sidBuffer, false, rightsArray, 1);

                    // Every other LSA call in this file branches on its status. Report a failure here too:
                    // a silent one deletes the account anyway and leaves the grant attached to a SID that no
                    // longer resolves to a name, which is the host leakage this method exists to prevent.
                    if (removeStatus != 0)
                    {
                        Trace.WriteLine($"Warning: failed to revoke {privilege} from {accountName} " +
                                        $"(NTSTATUS 0x{removeStatus:X8}); the grant may be left orphaned in the LSA policy.");
                    }
                }
                else
                {
                    Trace.WriteLine($"Warning: could not open the LSA policy to revoke {privilege} from {accountName} " +
                                    $"(NTSTATUS 0x{lsaOpenStatus:X8}); the grant may be left orphaned in the LSA policy.");
                }
            }
            catch
            {
                // Suppress revocation exceptions within test cleanup blocks to ensure baseline execution proceeds
            }
            finally
            {
                if (policyHandle != IntPtr.Zero) NativeMethods.LsaClose(policyHandle);
                if (sidBuffer != IntPtr.Zero) Marshal.FreeHGlobal(sidBuffer);
                if (nativeStringAlloc != IntPtr.Zero) Marshal.FreeHGlobal(nativeStringAlloc);
            }
        }

        public void Dispose()
        {
            if (_canModifyLsaPolicy && _accountCreatedLocally)
            {
                // ROBUSTNESS: Revoke user permissions inside LSA storage prior to calling account deletion.
                // This ensures Windows can resolve the name context to a real SID during cleanup blocks.
                RevokeLsaPrivilegeBeforeDeletion(_testAccountName, "SeServiceLogonRight");

                try
                {
                    // Clean up the local transient user account using AccountManagement
                    using (var context = new PrincipalContext(ContextType.Machine))
                    using (var user = UserPrincipal.FindByIdentity(context, IdentityType.Name, _testAccountName))
                    {
                        user?.Delete();
                    }
                }
                catch
                {
                    // Suppress teardown exceptions within cleanup context blocks
                }
            }
        }
    }
}
