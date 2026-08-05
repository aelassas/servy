using System;
using System.Threading;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides a centralized, thread-safe base class that implements an Interlocked-guarded
    /// disposable and finalizer pattern designed for purging sensitive cryptographic assets.
    /// </summary>
    public abstract class SecureDisposable : IDisposable
    {
        private int _disposed;

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this instance has already been disposed.
        /// </summary>
        /// <remarks>
        /// Utilizes <see cref="Volatile.Read(ref int)"/> to ensure the disposal state is accurately
        /// synchronized across CPU caches without the overhead of a full lock.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the provider has been disposed.</exception>
        protected void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// When overridden in a derived class, executes finalizer-safe, non-allocating memory
        /// purging of highly sensitive cryptographic items.
        /// </summary>
        protected abstract void ZeroSensitiveData();

        /// <summary>
        /// Performs strict memory-zeroing of the cached cryptographic material.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SecureDisposable"/> class.
        /// </summary>
        /// <remarks>
        /// The finalizer ensures that sensitive cryptographic material is zeroed out in memory
        /// even if the consumer fails to call explicit disposal routines.
        /// </remarks>
        ~SecureDisposable()
        {
            Dispose(false);
        }

        /// <summary>
        /// Zeroes the sensitive material exactly once, whether invoked explicitly or from the finalizer.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> when called from <see cref="Dispose()"/>, <see langword="false"/> when called
        /// from the finalizer. This base implementation intentionally ignores it: <see cref="ZeroSensitiveData"/>
        /// is finalizer-safe and must run on both paths. Overriders that release managed resources beyond key
        /// material should honour it.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            // 1. ATOMIC GUARD: Flip the flag BEFORE wiping memory.
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            // 2. ZEROING runs in BOTH paths - managed keys are still reachable from the finalizer
            // and zeroing them is non-allocating, finalizer-safe, and idempotent.
            ZeroSensitiveData();
        }
    }
}
