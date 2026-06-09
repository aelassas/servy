using Microsoft.Win32;
using Servy.Core.Native;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Servy.Testing
{
    public static class Helper
    {
        /// <summary>
        /// Handle Exe Path.
        /// Dynamically select the native Sysinternals binary based on runtime architecture to support ARM64 agents natively.
        /// </summary>
        public static readonly string HandleExePath = RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handle64a.exe")
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handle64.exe");

        private static readonly object _extractionLock = new object();

        /// <summary>
        /// Extracts handle64.exe from the assembly's embedded resources to the base directory.
        /// </summary>
        public static void ExtractHandleExe()
        {
            // Check if the file physically exists on the disk frame right now
            if (File.Exists(HandleExePath)) return;

            lock (_extractionLock)
            {
                // Double-check lock validation step
                if (File.Exists(HandleExePath)) return;

                var assembly = Assembly.GetExecutingAssembly();
                string targetFileName = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "handle64a.exe" : "handle64.exe";
                string resourceName = $"Servy.Core.IntegrationTests.Resources.{targetFileName}";

                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        var actualName = assembly.GetManifestResourceNames()
                            .FirstOrDefault(n => n.EndsWith(targetFileName));

                        if (actualName == null)
                            throw new FileNotFoundException($"Embedded resource metadata mapping for '{targetFileName}' was not found in the manifest layout.");

                        using (var fallbackStream = assembly.GetManifestResourceStream(actualName))
                        {
                            WriteResourceToDisk(fallbackStream);
                        }
                    }
                    else
                    {
                        WriteResourceToDisk(resourceStream);
                    }
                }
            }
        }

        private static void WriteResourceToDisk(Stream stream)
        {
            try
            {
                if (stream == null) return;

                // If the file somehow exists but _isExtracted was false, 
                // FileMode.Create would fail if another process is even just reading it.
                // We only write if the file isn't physically there.
                if (File.Exists(HandleExePath)) return;

                // Added FileShare.ReadWrite. On CI, Antivirus or Windows Indexer 
                // often grab handles the millisecond a file is created.
                using (FileStream fileStream = new FileStream(HandleExePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
                {
                    stream.CopyTo(fileStream);
                }
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x80070050)) // ERROR_FILE_EXISTS
            {
                // If we hit a race where the file was created between our check and our open, 
                // it's a win-the file is there.
            }
        }

        /// <summary>
        /// Programs the current user registry environment to suppress the Sysinternals graphical license box prompt.
        /// </summary>
        public static void AcceptSysinternalsEula()
        {
            try
            {
                // Sysinternals tools check for acceptance under HKCU\Software\Sysinternals\Handle
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Sysinternals\Handle"))
                {
                    if (key != null)
                    {
                        key.SetValue("EulaAccepted", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to pre-seed EulaAccepted registry key. Details: {ex.Message}");
            }
        }

        public static void RunOnSTA(Action action)
        {
            Exception threadException = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    threadException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (threadException != null)
            {
                throw threadException;
            }
        }

        public static async Task RunOnSTA(Func<Task> action)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    // Initialize the Dispatcher for the STA thread
                    var dispatcher = Dispatcher.CurrentDispatcher;

                    // Queue the test execution onto the STA thread's dispatcher.
                    // This guarantees that Dispatcher.CurrentDispatcher inside the test 
                    // resolves to THIS dispatcher, which has an active message pump.
                    dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await action();
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                        finally
                        {
                            // Shut down the dispatcher loop to exit the thread cleanly
                            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                        }
                    });

                    // Start the message pump. It will process the InvokeAsync above.
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait for the test (and the STA thread lifecycle) to complete
            await tcs.Task;
        }

        /// <summary>
        /// Checks if the current process is running with administrative privileges by examining the Windows identity and principal.
        /// </summary>
        /// <returns>Returns true if the process is running with administrative privileges; otherwise, false.</returns>
        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Proactively probes the system's LSA policy subsystem to see if the runner process 
        /// has security tokens capable of opening policy access mappings.
        /// </summary>
        public static bool CheckLsaPolicyAccess()
        {
            // Emulates an LSA open loop using minimum query flags to detect if the kernel drops an access error
            var oa = new NativeMethods.LSA_OBJECT_ATTRIBUTES
            {
                Length = Marshal.SizeOf<NativeMethods.LSA_OBJECT_ATTRIBUTES>()
            };

            IntPtr policyHandle = IntPtr.Zero;
            try
            {
                // Request lookup names permissions to check if policy manipulation can structurally be performed
                int status = NativeMethods.LsaOpenPolicy(IntPtr.Zero, ref oa, NativeMethods.POLICY_ACCESS.POLICY_LOOKUP_NAMES, out policyHandle);
                return status == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (policyHandle != IntPtr.Zero)
                {
                    NativeMethods.LsaClose(policyHandle);
                }
            }
        }

        /// <summary>
        /// Creates an NTFS directory junction point using the Windows native command shell utility.
        /// Directory junctions do not require elevated administrative privileges to implement.
        /// </summary>
        /// <param name="junctionPath">The target path where the physical directory junction link will be established.</param>
        /// <param name="targetDir">The fully qualified destination directory path that the junction point references.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="junctionPath"/> or <paramref name="targetDir"/> is null, empty, or whitespace.</exception>
        /// <exception cref="IOException">Thrown when the native command utility returns a non-zero exit code or the validation step detects that the generated object does not contain structural reparse point metadata properties.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the verification checkpoint evaluates that the newly established filesystem entry is physically missing from disk storage arrays.</exception>
        public static void CreateJunction(string junctionPath, string targetDir)
        {
            if (string.IsNullOrWhiteSpace(junctionPath))
                throw new ArgumentException("Junction path cannot be null or empty.", nameof(junctionPath));
            if (string.IsNullOrWhiteSpace(targetDir))
                throw new ArgumentException("Target directory cannot be null or empty.", nameof(targetDir));

            using (var process = new Process())
            {
                process.StartInfo.FileName = "cmd.exe";
                // /C runs the string command and terminates. Quotes wrap paths securely against space separation.
                process.StartInfo.Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetDir}\"";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new IOException($"mklink failed to create junction point with exit code {process.ExitCode}");
                }
            }

            // Explicitly verify the junction using fresh state retrieval
            var dirInfo = new DirectoryInfo(junctionPath);

            // CRITICAL: Force the state machine to flush its attribute cache
            dirInfo.Refresh();

            if (!dirInfo.Exists)
            {
                throw new DirectoryNotFoundException($"Junction verification failed: Directory does not exist at {junctionPath}.");
            }

            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                throw new IOException($"Junction validation failed at {junctionPath}. Target exists but lacks the explicit FileAttributes.ReparsePoint flag.");
            }
        }

        /// <summary>
        /// Creates an NTFS file symbolic link pointing to a canonical reference target configuration.
        /// File-level symbolic links typically require elevated execution tokens (Administrative privileges) 
        /// or an active global Windows configuration enabling Developer Mode.
        /// </summary>
        /// <param name="symlinkFilePath">The target destination path where the new file symbolic link item will materialize.</param>
        /// <param name="targetFilePath">The canonical baseline target data file location that the symbolic pointer addresses.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="symlinkFilePath"/> or <paramref name="targetFilePath"/> is null, empty, or whitespace.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if the runtime operating architecture security layout rejects execution due to missing operational deployment privileges or non-active system developer settings.</exception>
        /// <exception cref="FileNotFoundException">Thrown if validation processing finds the virtual reparse endpoint point asset is completely absent from the host volume index hierarchy.</exception>
        /// <exception cref="IOException">Thrown when low-level file attribution flags do not match the expected structural characteristics of an operating system reparse token constraint.</exception>
        public static void CreateFileSymlink(string symlinkFilePath, string targetFilePath)
        {
            if (string.IsNullOrWhiteSpace(symlinkFilePath))
                throw new ArgumentException("Symlink path cannot be null or empty.", nameof(symlinkFilePath));
            if (string.IsNullOrWhiteSpace(targetFilePath))
                throw new ArgumentException("Target file path cannot be null or empty.", nameof(targetFilePath));

            using (var process = new Process())
            {
                process.StartInfo.FileName = "cmd.exe";
                // NOTE: No /J flag here because it's a file, not a directory.
                process.StartInfo.Arguments = $"/c mklink \"{symlinkFilePath}\" \"{targetFilePath}\"";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    // If it fails because of permissions (common on non-admin environments), 
                    // throw a specific exception we can handle or skip gracefully in the unit test.
                    throw new UnauthorizedAccessException($"mklink failed with exit code {process.ExitCode}. This usually indicates the process lacks administrative privileges or Developer Mode is disabled.");
                }
            }

            // Explicitly verify the symlink using fresh state retrieval
            var fileInfo = new FileInfo(symlinkFilePath);

            // CRITICAL FOR .NET 4.8: Flush the internal attribute snapshot cache
            fileInfo.Refresh();

            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException($"Symlink verification failed: File does not exist at {symlinkFilePath}.");
            }

            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                throw new IOException($"Symlink validation failed at {symlinkFilePath}. The file was created but lacks the ReparsePoint attribute.");
            }
        }
    }
}
