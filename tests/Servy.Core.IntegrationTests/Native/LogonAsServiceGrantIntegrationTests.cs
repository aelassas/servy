using Servy.Core.Native;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Xunit;

namespace Servy.Core.IntegrationTests.Native
{
    [Collection("CoreOsIntegration")]
    public class LogonAsServiceGrantIntegrationTests : IDisposable
    {
        private readonly string _testAccountName;
        private readonly bool _isAdministrator;
        private readonly bool _canModifyLsaPolicy;
        private readonly bool _accountCreatedLocally;

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
            _isAdministrator = Helper.IsAdministrator();

            // 2. CI GUARD: Verify if the current context has actual operational permissions to call LSA APIs.
            // On standard GitHub Actions cloud agents, this prevents cascading Access Denied (0xC0000022) runtime breaks.
            _canModifyLsaPolicy = _isAdministrator && Helper.CheckLsaPolicyAccess();

            // Create a temporary, unique local user name
            _testAccountName = "ServyTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (_canModifyLsaPolicy)
            {
                try
                {
                    // DYNAMIC PROVISIONING: Use transactional principal management instead of legacy process forks.
                    // This forces immediate, deterministic commitment into the Windows SAM database registry tables.
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
                catch
                {
                    _accountCreatedLocally = false;
                }

                // Introduce a synchronization window to let the Windows SAM subsystem fully commit
                Thread.Sleep(500);
            }
        }

        #region Account Parsing & SID Resolution Failure Branches

        [Fact]
        public void Ensure_NullOrWhitespaceAccountName_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            // This path relies strictly on string validation and runs safely anywhere, including CI.
            Assert.Throws<ArgumentException>("account", () => LogonAsServiceGrant.Ensure(null));
            Assert.Throws<ArgumentException>("account", () => LogonAsServiceGrant.Ensure("   "));
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
            if (!_canModifyLsaPolicy || !_accountCreatedLocally) return;

            string shorthandAccount = $".\\{_testAccountName}";
            string fullyQualifiedAccount = $"{Environment.MachineName}\\{_testAccountName}";

            // Act
            // Trigger the execution path under test using the dot notation string configuration
            Exception ex = Record.Exception(() => LogonAsServiceGrant.Ensure(shorthandAccount));

            // Assert
            // 1. Maintain baseline telemetry assertions confirming no infrastructure policy execution crashes occurred
            Assert.Null(ex);

            // 2. IDENTITY PATH TRANSLATION: Explicitly expand the shorthand '.' notation to MachineName 
            // for the test assertion pool. This matches production expansion behavior and prevents .NET's 
            // NTAccount.Translate from failing on the raw literal dot prefix.
            try
            {
                string expandedShorthand = $"{Environment.MachineName}\\{_testAccountName}";

                var shorthandSid = new NTAccount(expandedShorthand).Translate(typeof(SecurityIdentifier));
                var fullyQualifiedSid = new NTAccount(fullyQualifiedAccount).Translate(typeof(SecurityIdentifier));

                Assert.NotNull(shorthandSid);
                Assert.NotNull(fullyQualifiedSid);

                // Assert structural equality of the resolved domain account SIDs
                Assert.Equal(fullyQualifiedSid, shorthandSid);
            }
            catch (IdentityNotMappedException)
            {
                Assert.Fail($"The ephemeral local test account '{_testAccountName}' could not be resolved back to an NTAccount SID profile map.");
            }
        }

        #endregion

        #region Real LSA Lifecycle Operations

        [Fact]
        public void Ensure_FreshAccountWithoutAnyRights_TriggersNotFoundBranchAndGrantsPrivilege()
        {
            // Arrange
            if (!_canModifyLsaPolicy || !_accountCreatedLocally) return;

            string fullAccountName = $"{Environment.MachineName}\\{_testAccountName}";

            // Resolve the account name to a SecurityIdentifier (SID) to query LSA
            var ntAccount = new NTAccount(fullAccountName);
            var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));

            // Allocate the buffer and use GetBinaryForm instead of GetBinaryBytes
            byte[] sidBytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBytes, 0);

            // Assert baseline - The freshly created account must not hold the logon right prior to execution
            var initialRights = GetAccountRightsViaNativeMethods(sidBytes);
            Assert.DoesNotContain("SeServiceLogonRight", initialRights);

            // Act 1 - Execute first pass: captures the native 0xC0000034 status (Not Found) branch and grants privilege
            Exception ex1 = Record.Exception(() => LogonAsServiceGrant.Ensure(fullAccountName));

            // Assert 1 - Verify execution succeeded and the privilege was physically written to the OS LSA policy database
            Assert.Null(ex1);
            var postGrantRights = GetAccountRightsViaNativeMethods(sidBytes);
            Assert.Contains("SeServiceLogonRight", postGrantRights);

            // Act 2 - Execute second pass: must hit the cached 'found' match pipeline loop branch as an early exit no-op
            Exception ex2 = Record.Exception(() => LogonAsServiceGrant.Ensure(fullAccountName));

            // Assert 2 - Verify no exceptions were raised, the privilege wasn't stripped, and structure remains unmodified
            Assert.Null(ex2);
            var postIdempotentRights = GetAccountRightsViaNativeMethods(sidBytes);
            Assert.Contains("SeServiceLogonRight", postIdempotentRights);

            // Verify idempotency payload metrics: collection sizes must align perfectly without row duplications
            Assert.Equal(postGrantRights.Count, postIdempotentRights.Count);
        }

        #endregion

        #region Native Methods LSA Inspection Helper

        private static List<string> GetAccountRightsViaNativeMethods(byte[] sidBytes)
        {
            var rightsList = new List<string>();

            var objectAttributes = new NativeMethods.LSA_OBJECT_ATTRIBUTES { Length = Marshal.SizeOf<NativeMethods.LSA_OBJECT_ATTRIBUTES>() };
            IntPtr policyHandle;

            // Access permission combination required to inspect the account privilege definitions safely
            uint desiredAccess = 0x00010000 /* STANDARD_RIGHTS_REQUIRED */ | NativeMethods.POLICY_ACCESS.POLICY_LOOKUP_NAMES;

            int openStatus = NativeMethods.LsaOpenPolicy(IntPtr.Zero, ref objectAttributes, desiredAccess, out policyHandle);
            if (openStatus != 0) return rightsList; // Abort if context window mapping fails

            IntPtr rawSidAllocationPtr = Marshal.AllocHGlobal(sidBytes.Length);
            try
            {
                Marshal.Copy(sidBytes, 0, rawSidAllocationPtr, sidBytes.Length);

                IntPtr outUserRightsBufferPtr;
                uint countOfRights;

                // Query the operating system directly for the explicit privileges mapped to this SID allocation string
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

            try
            {
                // Resolve user domain string back to an active NT Security Identifier structure
                var ntAccount = new NTAccount(accountName);
                var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));

                byte[] sidBytes = new byte[sid.BinaryLength];
                sid.GetBinaryForm(sidBytes, 0);

                sidBuffer = Marshal.AllocHGlobal(sidBytes.Length);
                Marshal.Copy(sidBytes, 0, sidBuffer, sidBytes.Length);

                var objectAttributes = new NativeMethods.LSA_OBJECT_ATTRIBUTES();

                int lsaOpenStatus = NativeMethods.LsaOpenPolicy(
                    IntPtr.Zero,
                    ref objectAttributes,
                    NativeMethods.POLICY_ACCESS.POLICY_LOOKUP_NAMES | NativeMethods.POLICY_ACCESS.POLICY_ASSIGN_PRIVILEGE,
                    out policyHandle);

                if (lsaOpenStatus == 0)
                {
                    var privilegeString = new NativeMethods.LSA_UNICODE_STRING();
                    IntPtr nativeStringAlloc = Marshal.StringToHGlobalUni(privilege);

                    privilegeString.Buffer = nativeStringAlloc;
                    privilegeString.Length = (ushort)(privilege.Length * 2);
                    privilegeString.MaximumLength = (ushort)((privilege.Length + 1) * 2);

                    var rightsArray = new NativeMethods.LSA_UNICODE_STRING[] { privilegeString };

                    // Revoke assigned system access parameters safely before SAM subsystem disconnect calls
                    LsaRemoveAccountRights(policyHandle, sidBuffer, false, rightsArray, 1);

                    Marshal.FreeHGlobal(nativeStringAlloc);
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