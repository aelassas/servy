using Servy.Core.Logging;
using Servy.UI.Resources;
using Servy.UI.Services;
using System;
using System.Threading.Tasks;

namespace Servy.UI.Localization
{
    /// <summary>
    /// Stores the language chosen in a language menu and tells the user when it takes effect.
    /// </summary>
    public static class LanguageSwitcher
    {
        /// <summary>
        /// Stores the language chosen in the menu in the application's settings file.
        /// </summary>
        /// <param name="settingsPath">The full path of the application's settings file.</param>
        /// <param name="current">The language currently selected in the menu.</param>
        /// <param name="parameter">The command parameter of the menu item, an <see cref="AppLanguage"/> value.</param>
        /// <param name="messageBoxService">The service that shows the confirmation or the failure.</param>
        /// <param name="caption">The application name, used as the dialog caption and in the confirmation text.</param>
        /// <returns>
        /// The language now selected: the requested one when it was stored, otherwise <paramref name="current"/>.
        /// </returns>
        /// <remarks>
        /// The resources already loaded stay in the language the application started with, so the change takes
        /// effect at the next start. The application is not restarted here: it may be running elevated, and the
        /// user may be in the middle of editing a service.
        /// </remarks>
        public static async Task<AppLanguage> ChangeAsync(
            string settingsPath,
            AppLanguage current,
            object parameter,
            IMessageBoxService messageBoxService,
            string caption)
        {
            if (messageBoxService == null) throw new ArgumentNullException(nameof(messageBoxService));

            if (!(parameter is AppLanguage) || (AppLanguage)parameter == current)
            {
                return current;
            }

            var requested = (AppLanguage)parameter;

            try
            {
                StartupCulture.WriteLanguage(settingsPath, requested);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to save the {StartupCulture.LanguageSettingKey} setting: {ex.Message}");
                await messageBoxService.ShowErrorAsync(string.Format(Strings.Msg_LanguageSaveFailed, ex.Message), caption);
                return current;
            }

            Logger.Info($"Saved {StartupCulture.LanguageSettingKey} setting: {LocalizationCulture.Serialize(requested)}");
            await messageBoxService.ShowInfoAsync(
                string.Format(Strings.Msg_LanguageRestartRequired, LanguageDisplayNames.For(requested), caption),
                caption);
            return requested;
        }
    }
}
