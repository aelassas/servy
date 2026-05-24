using Servy.Core.Native;
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Servy.Testing
{
    public class Helper
    {
        public static void RunInSTA(Action action)
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

        public static async Task RunInSTAContext(Func<Task> action)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    // Initialize the Dispatcher for the STA thread
                    var dispatcher = Dispatcher.CurrentDispatcher;

                    // FIX: Queue the test execution onto the STA thread's dispatcher.
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
