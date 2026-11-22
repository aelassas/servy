using System.Globalization;

namespace Servy.UI.Localization
{
    /// <summary>
    /// What <see cref="StartupCulture.Apply(string, IEnumerable{string}, System.Globalization.CultureInfo)"/> selected and what it could not do.
    /// </summary>
    public sealed class StartupCultureResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StartupCultureResult"/> class.
        /// </summary>
        /// <param name="language">The selected language.</param>
        /// <param name="culture">The UI culture that was applied, or <see langword="null"/> when it was left as Windows set it.</param>
        /// <param name="rejectedArgument">The unsupported <c>--lang</c> value that was ignored, or <see langword="null"/>.</param>
        /// <param name="syncError">Why the <c>--lang</c> value could not be written to the settings file, or <see langword="null"/>.</param>
        public StartupCultureResult(AppLanguage language, CultureInfo? culture, string? rejectedArgument, string? syncError)
        {
            Language = language;
            Culture = culture;
            RejectedArgument = rejectedArgument;
            SyncError = syncError;
        }

        /// <summary>Gets the selected language.</summary>
        public AppLanguage Language { get; }

        /// <summary>Gets the UI culture that was applied, or <see langword="null"/> when it was left as Windows set it.</summary>
        public CultureInfo? Culture { get; }

        /// <summary>Gets the unsupported <c>--lang</c> value that was ignored, or <see langword="null"/>.</summary>
        public string? RejectedArgument { get; }

        /// <summary>Gets why the <c>--lang</c> value could not be written to the settings file, or <see langword="null"/>.</summary>
        public string? SyncError { get; }
    }
}
