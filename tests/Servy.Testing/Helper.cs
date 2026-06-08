using Servy.Core.Native;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
        private static bool _isExtracted = false;

        /// <summary>
        /// Extracts handle64.exe from the assembly's embedded resources to the base directory.
        /// </summary>
        public static void ExtractHandleExe()
        {
            // Fast path: if already extracted in this process, skip immediately
            if (_isExtracted || File.Exists(HandleExePath)) return;

            // Static lock prevents multiple class instances from extracting simultaneously
            lock (_extractionLock)
            {
                // Double-check pattern: the file might have been created while we waited for the lock
                if (_isExtracted || File.Exists(HandleExePath))
                {
                    _isExtracted = true;
                    return;
                }

                var assembly = Assembly.GetExecutingAssembly();
                // Resource names usually follow: ProjectNamespace.Folder.FileName.Extension
                // Dynamically select the resource manifest lookup string matching the target platform asset
                string targetFileName = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "handle64a.exe" : "handle64.exe";
                string resourceName = $"Servy.Core.IntegrationTests.Resources.{targetFileName}";

                using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        // Fallback: try to find the resource by name if the full namespace path is unknown
                        var actualName = assembly.GetManifestResourceNames()
                            .FirstOrDefault(n => n.EndsWith(targetFileName));

                        if (actualName == null) return;

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

                _isExtracted = true;
            }
        }

        private static void WriteResourceToDisk(Stream? stream)
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

        public static void RunOnSTA(Action action, bool createApp = false)
        {
            Exception? threadException = null;
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

        public static async Task RunOnSTA(Func<Task> action, bool createApp = false)
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

    }
}
