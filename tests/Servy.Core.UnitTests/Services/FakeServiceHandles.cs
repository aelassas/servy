using Servy.Core.Native;
using Servy.Testing;
using System.Runtime.InteropServices;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Builds <see cref="SafeScmHandle"/> / <see cref="SafeServiceHandle"/> wrappers around
    /// throw-away unmanaged memory so a valid-looking handle does not fault the runtime on Dispose.
    /// Pass 0 for a handle whose <c>IsInvalid</c> evaluates to true.
    /// </summary>
    /// <remarks>
    /// Shared by every test class in this project that needs a fake native handle, so the
    /// allocation bookkeeping exists once instead of once per class. Owners hold one instance and
    /// forward their own <see cref="IDisposable.Dispose"/> to it.
    /// </remarks>
    internal sealed class FakeServiceHandles : IDisposable
    {
        private readonly List<IntPtr> _allocations = new List<IntPtr>();

        /// <summary>
        /// Creates a Service Control Manager handle wrapper.
        /// </summary>
        /// <param name="value">Any non-zero value yields a valid handle; 0 yields an invalid one.</param>
        /// <returns>The handle wrapper.</returns>
        public SafeScmHandle Scm(int value = 1)
        {
            return Create<SafeScmHandle>(value);
        }

        /// <summary>
        /// Creates a service handle wrapper.
        /// </summary>
        /// <param name="value">Any non-zero value yields a valid handle; 0 yields an invalid one.</param>
        /// <returns>The handle wrapper.</returns>
        public SafeServiceHandle Service(int value = 1)
        {
            return Create<SafeServiceHandle>(value);
        }

        /// <summary>
        /// Frees every pointer this factory handed out.
        /// </summary>
        public void Dispose()
        {
            lock (_allocations)
            {
                foreach (var ptr in _allocations)
                {
                    Marshal.FreeHGlobal(ptr);
                }

                _allocations.Clear();
            }
        }

        private T Create<T>(int value) where T : class
        {
            var handle = Activator.CreateInstance(typeof(T), true) as T;
            if (handle == null)
            {
                throw new InvalidOperationException("Could not construct " + typeof(T).Name + ".");
            }

            // If the caller explicitly passes 0, keep it as IntPtr.Zero so handle.IsInvalid evaluates to true.
            // Otherwise, allocate valid unmanaged space to prevent native Access Violations (0xC0000005) on Dispose.
            IntPtr ptrToInject = IntPtr.Zero;
            if (value != 0)
            {
                ptrToInject = Marshal.AllocHGlobal(64);
                lock (_allocations)
                {
                    _allocations.Add(ptrToInject);
                }
            }

            // TestReflection automatically ascends the inheritance chain to locate and invoke 'SetHandle' on SafeHandle
            TestReflection.InvokeNonPublic(handle, "SetHandle", ptrToInject);

            return handle;
        }
    }
}
