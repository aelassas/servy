using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Applies the UI culture used for localized resource lookup.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class CultureHelper
    {
        private const string CultureEnvVar = "SERVY_UI_CULTURE";

        /// <summary>
        /// Sets <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> (and current-thread UI culture)
        /// from <c>SERVY_UI_CULTURE</c> when present, otherwise leaves the OS UI culture unchanged
        /// so satellite assemblies such as <c>zh-Hans</c> load automatically on Chinese systems.
        /// </summary>
        public static void ApplyUiCulture()
        {
            var overrideName = Environment.GetEnvironmentVariable(CultureEnvVar);
            if (string.IsNullOrWhiteSpace(overrideName))
            {
                return;
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(overrideName.Trim());
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (CultureNotFoundException)
            {
                // Ignore invalid override; keep OS UI culture.
            }
        }
    }
}
