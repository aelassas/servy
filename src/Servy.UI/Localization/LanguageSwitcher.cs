using Servy.Core.Logging;
using Servy.UI.Resources;
using Servy.UI.Services;
using System;
using System.Threading.Tasks;

namespace Servy.UI.Localization
{
    /// <summary>
    /// Changes the display language from a language menu: asks the user to confirm the restart, stores the
    /// language for both applications and restarts.
    /// </summary>
    public static class LanguageSwitcher
    {
        /// <summary>
        /// Asks the user to confirm, then stores the language chosen in the menu and restarts the application.
        /// </summary>
        /// <param name="settingsPath">The full path of the application's settings file.</param>
        /// <param name="current">The language currently selected in the menu.</param>
        /// <param name="parameter">The command parameter of the menu item, an <see cref="AppLanguage"/> value.</param>
        /// <param name="messageBoxService">The service that asks for the confirmation and shows a failure.</param>
        /// <param name="caption">The application name, used as the dialog caption and in the confirmation text.</param>
        /// <param name="restart">Restarts the application; called only after the language was stored.</param>
        /// <returns>
        /// The language now selected: the requested one when the user confirmed and it was stored, otherwise
        /// <paramref name="current"/>.
        /// </returns>
        /// <remarks>
        /// The resources already loaded stay in the language the application started with, so a change needs a
        /// restart. The confirmation says that input which is not saved will be lost. When the user declines,
        /// nothing is stored and nothing changes. The language is stored for the Desktop app and for the Manager
        /// (see <see cref="StartupCulture.WriteLanguageToAll"/>); a failure to store it for the other
        /// application is logged and does not stop the change.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="messageBoxService"/> or <paramref name="restart"/> is <see langword="null"/>.</exception>
        public static async Task<AppLanguage> ChangeAsync(
            string settingsPath,
            AppLanguage current,
            object parameter,
            IMessageBoxService messageBoxService,
            string caption,
            Action restart)
        {
            if (messageBoxService == null) throw new ArgumentNullException(nameof(messageBoxService));
            if (restart == null) throw new ArgumentNullException(nameof(restart));

            if (!(parameter is AppLanguage) || (AppLanguage)parameter == current)
            {
                return current;
            }

            var requested = (AppLanguage)parameter;

            var confirmed = await messageBoxService.ShowConfirmAsync(
                string.Format(Strings.Msg_LanguageRestartConfirm, LanguageDisplayNames.For(requested), caption),
                caption);

            if (!confirmed)
            {
                return current;
            }

            try
            {
                foreach (var failure in StartupCulture.WriteLanguageToAll(settingsPath, requested))
                {
                    Logger.Warn($"Failed to save the {StartupCulture.LanguageSettingKey} setting to {failure}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to save the {StartupCulture.LanguageSettingKey} setting: {ex.Message}");
                await messageBoxService.ShowErrorAsync(string.Format(Strings.Msg_LanguageSaveFailed, ex.Message), caption);
                return current;
            }

            Logger.Info($"Saved {StartupCulture.LanguageSettingKey} setting: {LocalizationCulture.Serialize(requested)}; restarting to apply it");
            restart();
            return requested;
        }
    }
}
