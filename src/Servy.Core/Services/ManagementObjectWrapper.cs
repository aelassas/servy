using System;
using System.Diagnostics.CodeAnalysis;
using System.Management;

namespace Servy.Core.Services
{
    /// <summary>
    /// Wraps a <see cref="ManagementBaseObject"/> instance to provide a testable interface
    /// for accessing service properties such as <see cref="StartMode"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ManagementObjectWrapper : IManagementObject
    {
        private readonly ManagementBaseObject _obj;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagementObjectWrapper"/> class.
        /// </summary>
        /// <param name="obj">The underlying <see cref="ManagementBaseObject"/> to wrap.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> is <c>null</c>.</exception>
        public ManagementObjectWrapper(ManagementBaseObject obj)
        {
            _obj = obj ?? throw new ArgumentNullException(nameof(obj));
        }

        /// <inheritdoc />
        public string StartMode => _obj["StartMode"]?.ToString();
    }
}
