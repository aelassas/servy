using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.UI.Resources;
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
        /// Shared instance to prevent socket exhaustion and allow connection pooling.
        /// </summary>
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Configures the shared HttpClient exactly once.
        /// </summary>
        static HelpService()
        {
            // Setting headers here ensures they are only set once for the entire app life.
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Servy");

            // Centralized timeout for the static client
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpService"/> class.
        /// </summary>
        /// <param name="messageBoxService">The message box service used for UI dialogs.</param>
        public HelpService(IMessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        /// <inheritdoc />
        public async Task OpenDocumentation(string caption)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = AppConfig.DocumentationLink,
                    UseShellExecute = true
                };

                // Process.Start can return null if it hands off to an existing browser instance
                var process = Process.Start(psi);
                if (process != null)
                {
                    using (process)
                    {
                        // Native handle is closed immediately after launch 
                        // to prevent leaks in the Manager UI.
                    }
                }
                else
                {
                    Logger.Debug("Documentation link opened in an existing process.");
                }
            }
            catch (Exception ex)
            {
                // Prevent UI crash and notify the user if the browser cannot be launched
                Logger.Error($"Failed to open documentation link: {AppConfig.DocumentationLink}", ex);

                string errorMessage = string.Format(Strings.Msg_DocumentationOpenFailed, ex.Message);
                await _messageBoxService.ShowErrorAsync(errorMessage, caption);
            }
        }

        /// <inheritdoc />
        public async Task CheckUpdates(string caption)
        {
            try
            {
                // 10 seconds is the ideal 'patience window' for a manual UI trigger
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var url = "https://api.github.com/repos/aelassas/servy/releases/latest";

                    var response = await _httpClient.GetAsync(url, cts.Token);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<JObject>(content);
                    string? tagName = json?["tag_name"]?.ToString();

                    if (string.IsNullOrEmpty(tagName))
                    {
                        await _messageBoxService.ShowInfoAsync(Strings.Msg_NoUpdatesAvailable, caption);
                        return;
                    }

                    var latestVersion = Helper.ParseVersion(tagName);
                    var currentVersion = Helper.ParseVersion(AppConfig.Version);

                    if (latestVersion > currentVersion)
                    {
                        var res = await _messageBoxService.ShowConfirmAsync(Strings.Msg_UpdateAvailablePrompt, caption);
                        if (res)
                        {
                            using (Process.Start(new ProcessStartInfo
                            {
                                FileName = AppConfig.LatestReleaseLink,
                                UseShellExecute = true
                            }))
                            {
                                // The using block ensures the native process handle is closed 
                                // immediately after the process is launched, preventing a 
                                // handle leak in the calling application.
                            }
                        }
                    }
                    else
                    {
                        await _messageBoxService.ShowInfoAsync(Strings.Msg_NoUpdatesAvailable, caption);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Specific handling for the 10-second timeout
                Logger.Warn("Update check timed out.");
                await _messageBoxService.ShowErrorAsync(Strings.Msg_UpdateCheckTimeout, caption);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check for updates", ex);
                // Use string.Format to ensure the error prefix is also localized
                string errorMessage = string.Format(Strings.Msg_UpdateCheckFailed, ex.Message);
                await _messageBoxService.ShowErrorAsync(errorMessage, caption);
            }
        }

        /// <inheritdoc />
        public async Task OpenAboutDialog(string about, string caption)
        {
            await _messageBoxService.ShowInfoAsync(about, caption);
        }
    }
}
