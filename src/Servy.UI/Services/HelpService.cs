using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Diagnostics;
using System.Net.Http;

namespace Servy.UI.Services
{
    /// <summary>
    /// Provides help-related functionality such as opening documentation,
    /// checking for updates, and displaying the About dialog.
    /// </summary>
    public class HelpService : IHelpService
    {
        private readonly IMessageBoxService _messageBoxService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpService"/> class.
        /// </summary>
        /// <param name="messageBoxService">The message box service used for UI dialogs.</param>
        public HelpService(IMessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
        }

        /// <inheritdoc />
        public void OpenDocumentation()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppConfig.DocumentationLink,
                UseShellExecute = true
            });
        }

        /// <inheritdoc />
        public async Task CheckUpdates(string caption)
        {
            const string noUpdate = "No updates currently available.";
            const string updateAvailable = "A new version of Servy is available. Do you want to download it?";
            const string timeoutMessage = "The update check timed out. Please try again later.";

            try
            {
                // 10 seconds is the ideal 'patience window' for a manual UI trigger
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("ServyApp");

                    var url = "https://api.github.com/repos/aelassas/servy/releases/latest";

                    // GetAsync responds to the CancellationToken
                    var response = await http.GetAsync(url, cts.Token);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<JObject>(content);
                    string tagName = json?["tag_name"]?.ToString();

                    if (string.IsNullOrEmpty(tagName))
                    {
                        await _messageBoxService.ShowInfoAsync(noUpdate, caption);
                        return;
                    }

                    var latestVersion = Helper.ParseVersion(tagName);
                    var currentVersion = Helper.ParseVersion(AppConfig.Version);

                    if (latestVersion > currentVersion)
                    {
                        var res = await _messageBoxService.ShowConfirmAsync(updateAvailable, caption);
                        if (res)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = AppConfig.LatestReleaseLink,
                                UseShellExecute = true
                            });
                        }
                    }
                    else
                    {
                        await _messageBoxService.ShowInfoAsync(noUpdate, caption);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Specific handling for the 10-second timeout
                Logger.Warn("Update check timed out.");
                await _messageBoxService.ShowErrorAsync(timeoutMessage, caption);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check for updates", ex);
                await _messageBoxService.ShowErrorAsync("Failed to check updates: " + ex.Message, caption);
            }
        }

        /// <inheritdoc />
        public async Task OpenAboutDialog(string about, string caption)
        {
            await _messageBoxService.ShowInfoAsync(about, caption);
        }
    }
}
