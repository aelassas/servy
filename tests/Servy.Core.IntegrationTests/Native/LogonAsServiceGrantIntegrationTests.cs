using Servy.Core.Native;
using Servy.Testing;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Servy.Core.IntegrationTests.Native
{
    [Collection("LogonAsServiceGrantIntegrationTests")]
    public class LogonAsServiceGrantIntegrationTests : IDisposable
    {
        private readonly string _testAccountName;
        private readonly bool _isAdministrator;
        private readonly bool _canModifyLsaPolicy;

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
                ExecuteNetUserCommand($"/add {_testAccountName} \"T3stP@ssw0rd!\"");

                // Introduce a synchronization window to let the Windows SAM subsystem fully commit
                Thread.Sleep(500);
            }
        }

        #region Account Parsing & SID Resolution Failure Branches

        [Fact]
        public void Ensure_NullOrWhitespaceAccountName_ThrowsArgumentException()
        {
            // This path relies strictly on string validation and runs safely anywhere, including CI.
            Assert.Throws<ArgumentException>("account", () => LogonAsServiceGrant.Ensure(null!));
            Assert.Throws<ArgumentException>("account", () => LogonAsServiceGrant.Ensure("   "));
        }

        [Fact]
        public void Ensure_NonExistentAccountName_ThrowsInvalidOperationExceptionWithDetailedContext()
        {
            // On non-elevated or restricted environments like cloud CI workers, this test can safely execute 
            // because resolving a non-existent fake string to a SID fails inside the framework NTAccount.Translate 
            // mapping loop before it ever touches the native LSA privilege modification routines.
            string fakeAccount = "MachineNameOrDomain\\GhostUser_" + Guid.NewGuid().ToString("N");

            var ex = Assert.Throws<InvalidOperationException>(() => LogonAsServiceGrant.Ensure(fakeAccount));
            Assert.Contains($"Cannot resolve SID for '{fakeAccount}'", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        #endregion

        #region Local Machine Notation Translation Path

        [Fact]
        public void Ensure_ShorthandLocalNotation_CorrectlyTranslatesMachinePrefix()
        {
            if (!_canModifyLsaPolicy) return;

            string shorthandAccount = $".\\{_testAccountName}";

            // Act
            Exception? ex = Record.Exception(() => LogonAsServiceGrant.Ensure(shorthandAccount));

            // Assert
            // Ensure no exceptions occurred during SID mapping or LSA policy lookup
            Assert.Null(ex);
        }

        #endregion

        #region Real LSA Lifecycle Operations

        [Fact]
        public void Ensure_FreshAccountWithoutAnyRights_TriggersNotFoundBranchAndGrantsPrivilege()
        {
            if (!_canModifyLsaPolicy) return;

            string fullAccountName = $"{Environment.MachineName}\\{_testAccountName}";

            // Act
            // First call: Should handle the '0xC0000034' status (Not Found) internally and succeed
            Exception? ex1 = Record.Exception(() => LogonAsServiceGrant.Ensure(fullAccountName));

            // Second call: Should hit the 'found' branch and exit early (no-op)
            Exception? ex2 = Record.Exception(() => LogonAsServiceGrant.Ensure(fullAccountName));

            // Assert
            Assert.Null(ex1);
            Assert.Null(ex2);
        }

        #endregion

        private static void ExecuteNetUserCommand(string args)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "net.exe",
                Arguments = "user " + args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                if (process != null)
                {
                    process.WaitForExit();
                }
            }
        }

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
            if (_canModifyLsaPolicy)
            {
                // ROBUSTNESS: Revoke user permissions inside LSA storage prior to calling net user /delete.
                // This ensures Windows can resolve the name context to a real SID during cleanup blocks.
                RevokeLsaPrivilegeBeforeDeletion(_testAccountName, "SeServiceLogonRight");

                ExecuteNetUserCommand($"/delete {_testAccountName}");
            }
        }
    }
}