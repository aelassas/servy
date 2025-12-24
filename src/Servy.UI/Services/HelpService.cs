using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

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
            try
            {
                const string noUpdate = "No updates currently available.";
                const string updateAvailable = "A new version of Servy is available. Do you want to download it?";

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("ServyApp");

                    // Get latest release from GitHub API
                    var url = "https://api.github.com/repos/aelassas/servy/releases/latest";
                    var response = await http.GetStringAsync(url);

                    // Parse JSON response
                    var json = JsonConvert.DeserializeObject<JObject>(response);
                    string tagName = json?["tag_name"]?.ToString();

                    if (string.IsNullOrEmpty(tagName))
                    {
                        await _messageBoxService.ShowInfoAsync(noUpdate, caption);
                        return;
                    }

                    // Convert version tag to double (e.g., "v1.2" -> 1.2)
                    var latestVersion = Helper.ParseVersion(tagName);
                    var currentVersion = Helper.ParseVersion(AppConfig.Version);

                    if (latestVersion > currentVersion)
                    {
                        var res =await _messageBoxService.ShowConfirmAsync(updateAvailable, caption);

                        if (res)
                        {
                            // Open latest release page
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
            catch (Exception ex)
            {
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
