using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Infrastructure.Helpers;

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
        /// Calls <see cref="DatabaseValidator.IsSqliteVersionSafe"/>.
        /// </summary>
        /// <param name="detectedVersion">
        /// When this method returns, the version string that was detected, or <see langword="null"/> when
        /// it could not be determined.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the detected version is safe to use; otherwise <see langword="false"/>.
        /// </returns>
        public bool IsSqliteVersionSafe(out string detectedVersion)
        {
            return DatabaseValidator.IsSqliteVersionSafe(out detectedVersion);
        }
    }
}
