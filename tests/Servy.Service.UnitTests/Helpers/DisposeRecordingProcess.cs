using System.ComponentModel;
using System.Diagnostics;

namespace Servy.Service.UnitTests.Helpers
{
    /// <summary>
    /// A <see cref="Process"/> that records explicit disposal, so a test can assert that a caller released
    /// the instance exactly once without starting an OS process.
    /// </summary>
    internal sealed class DisposeRecordingProcess : Process
    {
        /// <summary>Gets the number of explicit <see cref="Component.Dispose()"/> calls this instance has seen.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>Gets a value indicating whether the instance has been explicitly disposed at least once.</summary>
        public bool Disposed => DisposeCount > 0;

        /// <summary>
        /// Counts explicit disposal only; the finalizer path is passed through uncounted.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Component.Dispose()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }
}
