using Microsoft.Win32;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Restarter.Bootstrap
{
    /// <summary>
    /// The production <see cref="IRestarterBootstrapEnvironment"/>: every member is a forward to the call
    /// <see cref="Program.Run"/> made inline before the seam existed.
    /// </summary>
    /// <remarks>
    /// Nothing here holds state and nothing here decides anything. The arguments, the order and the
    /// exception behaviour are those of the inline calls, so an unconfigured
    /// <see cref="Program.Run"/> starts up exactly as it did before.
    /// </remarks>
    internal sealed class RestarterBootstrapEnvironment : IRestarterBootstrapEnvironment
    {
        /// <summary>
        /// Calls <see cref="Helper.EnsureEventSourceExists"/>.
        /// </summary>
        public void EnsureEventSourceExists()
        {
            Helper.EnsureEventSourceExists();
        }

        /// <summary>
        /// Creates an <see cref="EventLogLogger"/> on <see cref="AppConfig.EventSource"/>, leaving the log
        /// level and the prefix at their defaults, exactly as the two inline <c>new</c> expressions did.
        /// </summary>
        /// <param name="isEventLogEnabled">Whether the logger may write to the Windows Event Log.</param>
        /// <returns>The created logger.</returns>
        public IServyLogger CreateEventLogLogger(bool isEventLogEnabled)
        {
            return new EventLogLogger(AppConfig.EventSource, isEventLogEnabled: isEventLogEnabled);
        }

        /// <summary>
        /// Reads <c>ImagePath</c> from the service's key under
        /// <c>HKLM\SYSTEM\CurrentControlSet\Services</c>, which every account can read.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The command line, or <see langword="null"/> when the service is not installed or it cannot be read.</returns>
        [ExcludeFromCodeCoverage]
        public string? GetServiceImagePath(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName) || serviceName.IndexOfAny(new[] { '\\', '/' }) >= 0)
                return null;

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, writable: false))
                {
                    return key?.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
                Logger.Warn($"Could not read the executable of service '{serviceName}'.", ex);
                return null;
            }
        }
    }
}
