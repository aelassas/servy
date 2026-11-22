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
        /// Returns the name of <paramref name="language"/> as shown in the language menu and in messages.
        /// </summary>
        /// <param name="language">The language to name.</param>
        /// <returns>The display name, in the current display language.</returns>
        public static string For(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English:
                    return Strings.Language_English;
                case AppLanguage.ChineseSimplified:
                    return Strings.Language_ChineseSimplified;
                default:
                    return Strings.Language_System;
            }
        }
    }
}
