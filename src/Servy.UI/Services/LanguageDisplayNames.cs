using Servy.UI.Localization;
using Servy.UI.Resources;

namespace Servy.UI.Services
{
    /// <summary>
    /// Provides the localized display names of the selectable languages.
    /// </summary>
    public static class LanguageDisplayNames
    {
        /// <summary>
        /// Returns the localized name of <paramref name="language"/>.
        /// </summary>
        public static string For(AppLanguage language)
            => language switch
            {
                AppLanguage.English => Strings.Language_English,
                AppLanguage.ChineseSimplified => Strings.Language_ChineseSimplified,
                _ => Strings.Language_System,
            };
    }
}
