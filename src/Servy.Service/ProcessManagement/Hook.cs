using System;
using System.Diagnostics;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Represents a lifecycle hook and its associated operating system process.
    /// </summary>
    public class Hook : IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// The logical name of the hook operation (for example, Pre-Launch or Post-Stop).
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// The process started by this hook.
        /// </summary>
        public Process Process { get; set; }

        /// <summary>
        /// Public implementation of Dispose pattern.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // Request that the GC not call the finalizer for this object.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from Finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Process?.Dispose();

                _disposed = true;
            }
        }
    }
}
