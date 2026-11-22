using System.Globalization;

namespace Servy.Core.Resources
{
    /// <summary>
    /// Selects the language of the Servy.Core resource strings for the current process.
    /// </summary>
    public static class ResourceLanguage
    {
        /// <summary>
        /// Pins the Servy.Core resource strings to the neutral (English) resources, whatever the Windows
        /// display language is.
        /// </summary>
        /// <remarks>
        /// Called first thing by the processes whose output is read by scripts and by people who support the
        /// installation: the CLI, the service wrapper, the host and the restarter. Their console output, log
        /// files and event log entries stay in English even when a translated satellite assembly of Servy.Core
        /// sits next to the executable. The desktop applications do not call it; they follow the selected
        /// display language.
        /// </remarks>
        public static void UseEnglish()
        {
            Strings.Culture = CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// Lets the Servy.Core resource strings follow the UI culture of the calling thread again.
        /// </summary>
        public static void FollowUiCulture()
        {
            Strings.Culture = null;
        }
    }
}
