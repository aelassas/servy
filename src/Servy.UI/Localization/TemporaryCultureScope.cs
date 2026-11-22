using System.Globalization;

namespace Servy.UI.Localization
{
    /// <summary>
    /// Applies a culture for the duration of a scope and restores the previous values on dispose. By default it also
    /// switches the defaults new threads inherit; pass <c>processWide: false</c> to switch only the calling thread.
    /// </summary>
    /// <remarks>
    /// Tests that only need a different culture on their own thread pass <c>processWide: false</c>, so a thread the test
    /// host starts cannot observe the switch. The product applies its culture through <see cref="Apply"/> at start-up.
    /// </remarks>
    public sealed class TemporaryCultureScope : IDisposable
    {
        private readonly CultureInfo _previousUiCulture;
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo? _previousDefaultUiCulture;
        private readonly CultureInfo? _previousDefaultCulture;
        private readonly bool _processWide;
        private bool _disposed;

        /// <summary>
        /// Applies the culture with the given name.
        /// </summary>
        /// <param name="cultureName">The culture name, empty for the neutral culture.</param>
        /// <param name="processWide">Whether the defaults new threads inherit are switched too.</param>
        public TemporaryCultureScope(string? cultureName, bool processWide = true)
            : this(LocalizationCulture.ToCulture(cultureName), processWide)
        {
        }

        /// <summary>
        /// Applies the given culture.
        /// </summary>
        /// <param name="culture">The culture to apply.</param>
        /// <param name="processWide">Whether the defaults new threads inherit are switched too.</param>
        public TemporaryCultureScope(CultureInfo culture, bool processWide = true)
        {
            if (culture == null) throw new ArgumentNullException(nameof(culture));

            _processWide = processWide;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            _previousCulture = CultureInfo.CurrentCulture;
            _previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            _previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;

            Apply(culture, processWide);
        }

        /// <summary>
        /// Applies a culture to the current thread and, unless asked otherwise, to the defaults new threads inherit.
        /// </summary>
        /// <param name="culture">The culture to apply.</param>
        /// <param name="processWide">Whether the defaults new threads inherit are switched too.</param>
        public static void Apply(CultureInfo culture, bool processWide = true)
        {
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;

            if (!processWide)
            {
                return;
            }

            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }

        /// <summary>
        /// Restores the cultures in effect before this scope was created.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            CultureInfo.CurrentUICulture = _previousUiCulture;
            CultureInfo.CurrentCulture = _previousCulture;

            if (!_processWide)
            {
                return;
            }

            CultureInfo.DefaultThreadCurrentUICulture = _previousDefaultUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = _previousDefaultCulture;
        }
    }
}
