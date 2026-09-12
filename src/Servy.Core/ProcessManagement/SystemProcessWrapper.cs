using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.ProcessManagement
{
    /// <summary>
    /// Concrete wrapper over <see cref="Process"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemProcessWrapper : ISystemProcess
    {
        private readonly Process _process;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemProcessWrapper"/> class.
        /// </summary>
        /// <param name="process">The underlying managed <see cref="Process"/> instance.</param>
        public SystemProcessWrapper(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        /// <inheritdoc />
        public int Id => _process.Id;

        /// <inheritdoc />
        public string ProcessName => _process.ProcessName;

        /// <inheritdoc />
        public DateTime StartTime => _process.StartTime;

        /// <inheritdoc />
        public bool HasExited => _process.HasExited;

        /// <inheritdoc />
        public void Kill() => _process.Kill();

        /// <inheritdoc />
        public bool WaitForExit(int milliseconds) => _process.WaitForExit(milliseconds);

        /// <inheritdoc />
        public void Dispose() => _process.Dispose();
    }
}
