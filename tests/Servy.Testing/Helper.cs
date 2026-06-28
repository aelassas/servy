using Microsoft.Win32;
using Servy.Core.Native;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;

namespace Servy.Testing
{
    /// <summary>
    /// Provides cross-cutting infrastructure utilities for the testing suite, 
    /// including resource management, STA-thread execution scaffolding, and environment privilege validation.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Gets the absolute filesystem path for the Sysinternals handle utility.
        /// Dynamically selects the native binary based on runtime architecture to support ARM64 agents natively.
        /// </summary>
        public static readonly string HandleExePath = RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handle64a.exe")
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handle64.exe");

        private static readonly object _extractionLock = new object();
        private static readonly object _applicationLock = new object();
        private static bool _applicationCreated;

        /// <summary>
        /// Extracts the architecture-appropriate Sysinternals handle binary
        /// (handle64.exe, or handle64a.exe on ARM64) from embedded resources to the base directory.
        /// </summary>
        /// <exception cref="FileNotFoundException">Thrown when the embedded resource cannot be located in the manifest.</exception>
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

                using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
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

        /// <summary>
        /// Writes the provided resource stream containing the Sysinternals binary out to the physical disk location.
        /// </summary>
        /// <param name="stream">The embedded resource data stream to extract. If null, the operation returns immediately.</param>
        /// <remarks>
        /// This method enforces pessimistic file existence checks and leverages <see cref="FileMode.CreateNew"/> combined 
        /// with <see cref="FileShare.ReadWrite"/> to safely navigate file-locking races caused by real-time background scanners, 
        /// security software, or parallel test execution threads on continuous integration (CI) agents.
        /// </remarks>
        private static void WriteResourceToDisk(Stream? stream)
        {
            try
            {
                if (stream == null) return;

                // The file may already have been created by a concurrent extraction (or a previous run).
                // Only write when it is not physically there; a lost race surfaces as ERROR_FILE_EXISTS below.
                if (File.Exists(HandleExePath)) return;

                // FileShare.ReadWrite. On CI, Antivirus or Windows Indexer 
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

        /// <summary>
        /// Ensures a WPF <see cref="Application"/> instance exists in the current AppDomain to support
        /// STA-thread-bound UI components, converters, or resource dictionaries during testing.
        /// </summary>
        /// <remarks>
        /// This method employs a double-checked locking pattern on <see cref="_applicationLock"/> 
        /// to ensure thread-safe, singleton initialization of the WPF application context, preventing 
        /// <see cref="InvalidOperationException"/> when UI-dependent code executes in isolated test environments.
        /// </remarks>
        private static void EnsureApplication()
        {
            lock (_applicationLock)
            {
                if (_applicationCreated)
                    return;

                if (Application.Current == null)
                {
                    new Application
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                }

                _applicationCreated = true;
            }
        }

        /// <summary>
        /// Executes a synchronous operation on a newly spawned Single-Threaded Apartment (STA) thread.
        /// </summary>
        /// <param name="action">The synchronous operation to execute.</param>
        /// <param name="createApp">If true, initializes a WPF <see cref="Application"/> instance within the thread.</param>
        public static void RunOnSTA(Action action, bool createApp = false)
        {
            RunOnSTA<object?>(() => { action(); return null; }, createApp);
        }

        /// <summary>
        /// Executes a synchronous value-returning function on a newly spawned Single-Threaded Apartment (STA) thread.
        /// </summary>
        /// <typeparam name="T">The return type value of the function.</typeparam>
        /// <param name="func">The synchronous function to execute.</param>
        /// <param name="createApp">If true, initializes a WPF <see cref="Application"/> instance within the thread.</param>
        /// <returns>The value returned by <paramref name="func"/>, evaluated on the STA thread.</returns>
        public static T RunOnSTA<T>(Func<T> func, bool createApp = false)
        {
            T result = default!;
            ExceptionDispatchInfo? capturedException = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (createApp)
                        EnsureApplication();

                    result = func();
                }
                catch (Exception ex)
                {
                    capturedException = ExceptionDispatchInfo.Capture(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            capturedException?.Throw(); // rethrows with the original stack trace intact
            return result;
        }

        /// <summary>
        /// Executes an asynchronous operation on a newly spawned Single-Threaded Apartment (STA) thread,
        /// ensuring a message pump is maintained for the duration of the task.
        /// </summary>
        /// <param name="action">The asynchronous task to execute.</param>
        /// <param name="createApp">If true, initializes a WPF <see cref="Application"/> instance within the thread.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
                            if (createApp)
                                EnsureApplication();

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
        /// Proactively probes the system's LSA policy subsystem to determine if the current runner 
        /// process has sufficient security tokens to perform policy-level operations.
        /// </summary>
        /// <returns>True if the process can open LSA policy with lookup permissions; otherwise, false.</returns>
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