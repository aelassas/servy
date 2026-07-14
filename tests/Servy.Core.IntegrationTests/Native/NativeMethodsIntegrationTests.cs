using Servy.Core.Native;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Servy.Core.IntegrationTests.Native
{
    public class NativeMethodsIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly List<string> _tempFiles = new List<string>();

        public NativeMethodsIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { /* Clean fallback */ }
                }
            }
        }

        #region Constants Verification

        [Fact]
        public void Constants_ValueMatching_VerifyCorrectValues()
        {
            Assert.Equal(0x0004, NativeMethods.SERVICE_QUERY_STATUS);
            Assert.Equal(3, NativeMethods.SERVICE_DEMAND_START);
            Assert.Equal(0xFFFFFFFFu, NativeMethods.SERVICE_NO_CHANGE);
            Assert.Equal(1, NativeMethods.SERVICE_CONTROL_STOP);
            Assert.Equal(3, NativeMethods.SERVICE_CONFIG_DELAYED_AUTO_START_INFO);
            Assert.Equal(1, NativeMethods.SERVICE_CONFIG_DESCRIPTION);
            Assert.Equal(0x0004u, NativeMethods.SC_MANAGER_ENUMERATE_SERVICE);
            Assert.Equal(0x0001u, NativeMethods.SERVICE_QUERY_CONFIG);
            Assert.Equal(2, NativeMethods.LOGON32_LOGON_INTERACTIVE);
            Assert.Equal(3, NativeMethods.LOGON32_LOGON_NETWORK);
            Assert.Equal(0, NativeMethods.LOGON32_PROVIDER_DEFAULT);
            Assert.Equal(5, NativeMethods.LOGON32_LOGON_SERVICE);
            Assert.Equal(0x00000002u, NativeMethods.TH32CS_SNAPPROCESS);
            Assert.Equal(new IntPtr(-1), NativeMethods.INVALID_HANDLE_VALUE);
            Assert.Equal(-1, NativeMethods.ATTACH_PARENT_PROCESS);
            Assert.Equal(65001u, NativeMethods.CP_UTF8);
            Assert.Equal(0x00000100, NativeMethods.SERVICE_ACCEPT_PRESHUTDOWN);
            Assert.Equal(0x00000004, NativeMethods.SERVICE_RUNNING);
            Assert.Equal(0x0000000F, NativeMethods.SERVICE_CONTROL_PRESHUTDOWN);
            Assert.Equal(0x00000003, NativeMethods.SERVICE_STOP_PENDING);
            Assert.Equal(0x00000001, NativeMethods.SERVICE_STOPPED);
            Assert.Equal(0x00000001, NativeMethods.SERVICE_ACCEPT_STOP);
            Assert.Equal(0x00000010, NativeMethods.SERVICE_WIN32_OWN_PROCESS);
            Assert.Equal(0x00000001u, NativeMethods.FILE_SHARE_READ);
            Assert.Equal(0x00000002u, NativeMethods.FILE_SHARE_WRITE);
            Assert.Equal(0x00000004u, NativeMethods.FILE_SHARE_DELETE);
            Assert.Equal(3u, NativeMethods.OPEN_EXISTING);
            Assert.Equal(0x02000000u, NativeMethods.FILE_FLAG_BACKUP_SEMANTICS);
            Assert.Equal(0x0u, NativeMethods.VOLUME_NAME_DOS);
            Assert.Equal(0x01u, NativeMethods.MOVEFILE_REPLACE_EXISTING);
            Assert.Equal(0x08u, NativeMethods.MOVEFILE_WRITE_THROUGH);
            Assert.Equal(122, NativeMethods.ERROR_INSUFFICIENT_BUFFER);
            Assert.Equal(0x0001u, NativeMethods.SC_MANAGER_CONNECT);
            Assert.Equal(0x0002u, NativeMethods.SC_MANAGER_CREATE_SERVICE);
            Assert.Equal(0x0002u, NativeMethods.SERVICE_CHANGE_CONFIG);
            Assert.Equal(0x0010u, NativeMethods.SERVICE_START);
            Assert.Equal(0x0020u, NativeMethods.SERVICE_STOP);
            Assert.Equal(0x00010000u, NativeMethods.SERVICE_DELETE);
            Assert.Equal(0x00000001u, NativeMethods.SERVICE_ERROR_NORMAL);
            Assert.Equal(7, NativeMethods.SERVICE_CONFIG_PRESHUTDOWN_INFO);
        }

        #endregion

        #region Regression Test (Process Snapshot Name Validity)

        [Fact]
        public void ProcessSnapshots_CurrentProcess_ReturnsValidNonMojibakeName()
        {
            // Act
            IntPtr hSnapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
            Assert.NotEqual(NativeMethods.INVALID_HANDLE_VALUE, hSnapshot);

            try
            {
                var pe32 = new NativeMethods.PROCESSENTRY32
                {
                    dwSize = (uint)Marshal.SizeOf<NativeMethods.PROCESSENTRY32>()
                };

                using (var currentProcess = Process.GetCurrentProcess())
                {
                    uint currentPid = (uint)currentProcess.Id;
                    bool foundCurrentProcess = false;
                    string expectedProcessName = Path.GetFileName(currentProcess.MainModule?.FileName ?? "testhost.exe");

                    if (NativeMethods.Process32First(hSnapshot, ref pe32))
                    {
                        do
                        {
                            if (pe32.th32ProcessID == currentPid)
                            {
                                foundCurrentProcess = true;
                                _output.WriteLine($"Found current process name: {pe32.szExeFile}");

                                // Assert
                                Assert.False(string.IsNullOrWhiteSpace(pe32.szExeFile));

                                // 1. Verify it has a clean executable extension
                                Assert.True(pe32.szExeFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
                                    $"The captured snapshot name '{pe32.szExeFile}' was corrupted into mojibake.");

                                // 2. Dynamically check against the actual host process name running this test frame
                                // This handles local test execution AND arbitrary CI test host wrappers flawlessly.
                                Assert.Contains(expectedProcessName, pe32.szExeFile, StringComparison.OrdinalIgnoreCase);
                                break;
                            }
                        } while (NativeMethods.Process32Next(hSnapshot, ref pe32));
                    }

                    NativeMethods.CloseHandle(hSnapshot);
                    Assert.True(foundCurrentProcess, "Process lookup snapshot tracking loop should isolate host runtime PID context.");
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hSnapshot);
            }
        }

        #endregion

        #region Process & System Utilities

        [Fact]
        public void CommandLineToArgvW_ValidStringInput_ParsesArgumentsCorrectly()
        {
            // Arrange
            string cmdLine = "servy-service.exe -c \"C:\\Program Files\\Config.json\" --verbose";

            // Act
            IntPtr argArrayPointer = NativeMethods.CommandLineToArgvW(cmdLine, out int numArgs);

            // Assert & Marshalling Pipeline
            Assert.NotEqual(IntPtr.Zero, argArrayPointer);

            string[] items;
            try
            {
                Assert.Equal(4, numArgs);

                items = new string[numArgs];
                for (int i = 0; i < numArgs; i++)
                {
                    IntPtr itemPtr = Marshal.ReadIntPtr(argArrayPointer, i * IntPtr.Size);
                    items[i] = Marshal.PtrToStringUni(itemPtr)!;
                }
            }
            finally
            {
                // Guarantee the allocated Win32 memory block is released back to the OS pool
                if (argArrayPointer != IntPtr.Zero)
                {
                    NativeMethods.LocalFree(argArrayPointer);
                }
            }

            // Assert Parsed Property Content Symmetrically
            Assert.Equal("servy-service.exe", items[0]);
            Assert.Equal("-c", items[1]);
            Assert.Equal("C:\\Program Files\\Config.json", items[2]);
            Assert.Equal("--verbose", items[3]);
        }

        [Fact]
        public void NtQueryInformationProcess_CurrentProcessHandle_ReturnsBasicInformation()
        {
            // Arrange
            using (var currentProcess = Process.GetCurrentProcess())
            {
                IntPtr processHandle = currentProcess.Handle;

                var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION();
                int size = Marshal.SizeOf<NativeMethods.PROCESS_BASIC_INFORMATION>();

                // Act & Assert
                int status = NativeMethods.NtQueryInformationProcess(
                    processHandle,
                    (int)NativeMethods.ProcessInfoClass.ProcessBasicInformation,
                    ref pbi,
                    (uint)size,
                    out uint returnLength);

                // NTSTATUS 0 == STATUS_SUCCESS
                Assert.Equal(0, status);
                Assert.Equal((uint)size, returnLength);
                Assert.NotEqual(IntPtr.Zero, pbi.PebBaseAddress);
                using (var h = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.QueryInformation, false, currentProcess.Id))
                {
                    Assert.False(h.IsInvalid);
                }
            }
        }

        [Fact]
        public void OpenProcess_InvalidProcessId_ReturnsInvalidHandle()
        {
            // Act
            using (var handle = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.QueryLimitedInformation, false, 999999))
            {
                // Assert
                Assert.True(handle.IsInvalid);
            }
        }

        #endregion

        #region File Tracking & Security Utilities

        [Fact]
        public void FileIdentity_FileTrackingStructures_ValidatesEqualityAndRotationDifferences()
        {
            // Arrange
            var idA = new NativeMethods.FILE_IDENTITY
            {
                FileIndex = 12345,
                VolumeSerialNumber = 98765,
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            var idB = new NativeMethods.FILE_IDENTITY
            {
                FileIndex = 12345,
                VolumeSerialNumber = 98765,
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            var idC = new NativeMethods.FILE_IDENTITY
            {
                FileIndex = 54321, // Differs by index path
                VolumeSerialNumber = 98765,
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            // Assert behavior when VolumeSerialNumber differs independently
            var idVolumeMismatch = new NativeMethods.FILE_IDENTITY
            {
                FileIndex = 12345,
                VolumeSerialNumber = 11111, // Differs by serial path
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            // Act & Assert
            Assert.False(idA.IsDifferentFrom(idB), "Identical primary handles must evaluate as the same file object.");
            Assert.True(idA.IsDifferentFrom(idC), "Varying FileIndex should indicate a rotation event.");
            Assert.True(idA.IsDifferentFrom(idVolumeMismatch), "Varying VolumeSerialNumber should indicate a volume/rotation move event.");
        }

        [Fact]
        public void FileIdentity_HandleValidityMismatch_ReturnsTrue()
        {
            // Arrange
            // Branch (1) Probe: Validation status asymmetry must trigger immediate difference flag
            var idValid = new NativeMethods.FILE_IDENTITY { IsValidHandleInfo = true, PrefixDigest = "SAME" };
            var idInvalid = new NativeMethods.FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "SAME" };

            // Act & Assert
            Assert.True(idValid.IsDifferentFrom(idInvalid), "Handle info status inequality must evaluate as structurally different file tracks.");
        }

        [Fact]
        public void FileIdentity_SecondaryProbeFAT32Fallback_ValidatesDigestEquality()
        {
            // Arrange
            // Branch (3) Probe: Both handle checks fail (e.g. FAT32 volume layers). Compare contents using PrefixDigest
            var baseId = new NativeMethods.FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "MD5_HASH_A" };
            var matchingId = new NativeMethods.FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "MD5_HASH_A" };
            var differingId = new NativeMethods.FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "MD5_HASH_B" };

            // Act & Assert
            Assert.False(baseId.IsDifferentFrom(matchingId), "Identical content hashes on invalid handle states must evaluate as unchanged.");
            Assert.True(baseId.IsDifferentFrom(differingId), "Differing content hashes on invalid handle states must trigger a rotation switch signal.");
        }

        [Fact]
        public void FileIdentity_UndeterminableStateFallback_DefaultsToTrue()
        {
            // Arrange
            // Branch (3) Fallback: No robust identifiers available on either side. Should report 'true' as a safe default.
            var blindIdA = new NativeMethods.FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = null! };
            var blindIdB = new NativeMethods.FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = null! };

            // Act & Assert
            Assert.True(blindIdA.IsDifferentFrom(blindIdB), "Undeterminable file identities must fall back to 'true' to safely force metadata loop updates.");
        }

        [Fact]
        public void MoveFileEx_ValidPaths_ExecutesAtomicFileRelocation()
        {
            // Arrange
            string src = Path.GetTempFileName();
            string dst = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".moved");
            _tempFiles.Add(src);
            _tempFiles.Add(dst);

            File.WriteAllText(src, "Payload Data");

            // Act
            bool success = NativeMethods.MoveFileEx(src, dst, NativeMethods.MOVEFILE_REPLACE_EXISTING | NativeMethods.MOVEFILE_WRITE_THROUGH);

            // Assert
            Assert.True(success);
            Assert.False(File.Exists(src));
            Assert.True(File.Exists(dst));
        }

        [Fact]
        public void LogonUser_InvalidCredentials_ReturnsFalseAndCapturesWin32Error()
        {
            // Act
            bool loggedOn = NativeMethods.LogonUser(
                "FakeAccountName",
                "FakeDomain",
                "WrongPassword",
                NativeMethods.LOGON32_LOGON_SERVICE,
                NativeMethods.LOGON32_PROVIDER_DEFAULT,
                out IntPtr token);
            int lastError = Marshal.GetLastWin32Error();   // must be read first

            // Assert
            Assert.False(loggedOn);
            Assert.Equal(IntPtr.Zero, token);
            Assert.NotEqual(0, lastError);                 // e.g. ERROR_LOGON_FAILURE (1326)
        }

        #endregion

        #region SCM & Service Control Management Verification

        [Fact]
        public void OpenSCManager_LocalMachineScope_ReturnsValidHandle()
        {
            // Act
            using (var scmHandle = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT | NativeMethods.SC_MANAGER_ENUMERATE_SERVICE))
            {
                // Assert
                Assert.False(scmHandle.IsInvalid);
            }
        }

        [Fact]
        public void OpenService_NonExistentService_ReturnsInvalidHandleAndSetsLastError()
        {
            // Arrange & Act
            using (var scmHandle = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT))
            {
                SafeServiceHandle? serviceHandle = null;
                try
                {
                    serviceHandle = NativeMethods.OpenService(scmHandle, "NonExistentService_Phantom_Verification", NativeMethods.SERVICE_QUERY_STATUS);
                    int lastError = Marshal.GetLastWin32Error();   // capture first

                    // Assert
                    Assert.True(serviceHandle.IsInvalid);

                    // 1060 == ERROR_SERVICE_DOES_NOT_EXIST
                    Assert.Equal(1060, lastError);
                }
                finally
                {
                    serviceHandle?.Dispose();
                }
            }
        }

        [Fact]
        public void ServiceStructuralProperties_Marshaling_ChecksMemorySizes()
        {
            // Act
            int sizeDescription = Marshal.SizeOf<NativeMethods.SERVICE_DESCRIPTION>();
            int sizeAutoStart = Marshal.SizeOf<NativeMethods.SERVICE_DELAYED_AUTO_START_INFO>();
            int sizePreshutdown = Marshal.SizeOf<NativeMethods.SERVICE_PRE_SHUTDOWN_INFO>();
            int sizeStatus = Marshal.SizeOf<NativeMethods.SERVICE_STATUS>();

            // Assert
            // WIN32 ABI MARSHALING: Pin exact structure byte lengths required by the OS kernel.

            // SERVICE_DESCRIPTION contains a single lpDescription pointer (4 bytes on x86, 8 bytes on x64)
            int expectedDescriptionSize = IntPtr.Size;
            Assert.Equal(expectedDescriptionSize, sizeDescription);

            // SERVICE_DELAYED_AUTO_START_INFO contains a single BOOL (System.Int32) -> 4 bytes
            const int ExpectedAutoStartSize = 4;
            Assert.Equal(ExpectedAutoStartSize, sizeAutoStart);

            // SERVICE_PRE_SHUTDOWN_INFO contains a single DWORD dwPreshutdownTimeout (System.UInt32) -> 4 bytes
            const int ExpectedPreshutdownSize = 4;
            Assert.Equal(ExpectedPreshutdownSize, sizePreshutdown);

            // SERVICE_STATUS contains 7 DWORD fields (7 * 4 bytes) -> 28 bytes total
            const int ExpectedStatusSize = 28;
            Assert.Equal(ExpectedStatusSize, sizeStatus);
        }

        #endregion

        #region Console & Signal Interface Verification

        [Fact]
        public void SetConsoleCtrlHandler_NullCallbackReference_SucceedsValidly()
        {
            // Passing null removes or sets default configuration components depending on the trailing boolean flag state parameters.
            bool success = NativeMethods.SetConsoleCtrlHandler(null, true);
            try
            {
                Assert.True(success, "Registering the default Ctrl handler (null callback, add=true) should succeed.");
            }
            finally
            {
                NativeMethods.SetConsoleCtrlHandler(null, false); // restore process-global state regardless
            }
        }

        #endregion

        #region LSA & Security Token Policies

        [Fact]
        public void LsaPolicy_HandleManagement_ReturnsInvalidSecurityErrorsOnStandardContexts()
        {
            // Standard runner environments run on least-privilege tokens.
            // Opening LSA policies safely fails on STATUS_ACCESS_DENIED or executes safely if elevated.
            var objectAttributes = new NativeMethods.LSA_OBJECT_ATTRIBUTES
            {
                Length = Marshal.SizeOf<NativeMethods.LSA_OBJECT_ATTRIBUTES>()
            };

            // Act
            int ntStatus = NativeMethods.LsaOpenPolicy(
                IntPtr.Zero,
                ref objectAttributes,
                NativeMethods.POLICY_ACCESS.POLICY_LOOKUP_NAMES,
                out IntPtr policyHandle);

            if (ntStatus == 0) // STATUS_SUCCESS (Running elevated/admin)
            {
                Assert.NotEqual(IntPtr.Zero, policyHandle);
                NativeMethods.LsaClose(policyHandle);
            }
            else
            {
                int win32Error = NativeMethods.LsaNtStatusToWinError(ntStatus);
                Assert.True(win32Error > 0);
            }
        }

        [Fact]
        public void LsaFreeMemory_NullPointerPassed_ExecutesSafelyWithoutCrashing()
        {
            // Act & Assert
            int result = NativeMethods.LsaFreeMemory(IntPtr.Zero);

            // LsaFreeMemory returns status codes; verifying zero-pointers are swallowed without memory segmentation faults
            Assert.True(result >= 0);
        }

        #endregion
    }
}