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
        /// <summary>
        /// A hook for integration tests to prevent the browser from opening.
        /// </summary>
        public static bool IsHeadlessMode { get; set; }

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
            _httpClient.Timeout = TimeSpan.FromSeconds(AppConfig.UpdateCheckHttpTimeoutSeconds);
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
                const string headlessLabel = "OpenDocumentation";
                const string fallbackDebug = "Documentation link opened in an existing process.";
                OpenExternalUrl(AppConfig.DocumentationLink, headlessLabel, fallbackDebug);
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
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConfig.UpdateCheckTimeoutSeconds)))
                {
                    var response = await _httpClient.GetAsync(AppConfig.LatestReleaseApiUrl, cts.Token);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    var json = JsonConvert.DeserializeObject<JObject>(content);
                    string? tagName = json?["tag_name"]?.ToString();

                    if (string.IsNullOrEmpty(tagName))
                    {
                        await _messageBoxService.ShowInfoAsync(Strings.Msg_NoUpdatesAvailable, caption);
                        return;
                    }

                    var latestVersion = Helper.ParseVersion(tagName);
                    var currentVersion = Helper.ParseVersion(AppConfig.Version);

                    // ROBUSTNESS: Normalize both Version instances to 4 components before executing comparison.
                    // This prevents false positives caused by System.Version evaluating missing fields (-1) as less than 0.
                    var normalizedLatest = NormalizeVersion(latestVersion);
                    var normalizedCurrent = NormalizeVersion(currentVersion);

                    if (normalizedLatest > normalizedCurrent)
                    {
                        var res = await _messageBoxService.ShowConfirmAsync(Strings.Msg_UpdateAvailablePrompt, caption);
                        if (res)
                        {
                            const string headlessLabel = "CheckUpdates";
                            const string fallbackDebug = "Release link opened in an existing process.";
                            OpenExternalUrl(AppConfig.LatestReleaseLink, headlessLabel, fallbackDebug);
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

        /// <summary>
        /// Normalizes a System.Version instance by padding uninitialized components (-1) out to 0.
        /// This enforces reliable semantic version checks when comparing 2-part, 3-part, or 4-part strings.
        /// </summary>
        private static Version NormalizeVersion(Version version)
        {
            if (version == null)
            {
                return new Version(0, 0, 0, 0);
            }

            int major = version.Major;
            int minor = version.Minor;
            int build = version.Build < 0 ? 0 : version.Build;
            int revision = version.Revision < 0 ? 0 : version.Revision;

            return new Version(major, minor, build, revision);
        }

        /// <inheritdoc />
        public async Task OpenAboutDialog(string about, string caption)
        {
            await _messageBoxService.ShowInfoAsync(about, caption);
        }

        /// <summary>
        /// Opens a URL using the system's default browser or logs the action if running in headless mode.
        /// </summary>
        /// <param name="url">The external web address or file path to open.</param>
        /// <param name="headlessLabel">A descriptive label used for console output when <see cref="IsHeadlessMode"/> is true.</param>
        /// <param name="fallbackDebug">The debug message to log if the process cannot be tracked (e.g., when handed off to an existing browser instance).</param>
        /// <remarks>
        /// This method handles the non-deterministic return value of <see cref="Process.Start(ProcessStartInfo)"/>. 
        /// When a URL is handed off to an existing browser process, the method immediately logs 
        /// the <paramref name="fallbackDebug"/> message. In UI environments, any returned process 
        /// handle is disposed immediately to prevent resource leaks in the Manager interface.
        /// </remarks>
        private static void OpenExternalUrl(string url, string headlessLabel, string fallbackDebug)
        {
            var psi = new ProcessStartInfo { FileName = url, UseShellExecute = true };
            if (IsHeadlessMode)
            {
                Console.WriteLine($"[HEADLESS INFO] {headlessLabel}: opening browser URL {psi.FileName}");
                return;
            }

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
                Logger.Debug(fallbackDebug);
            }
        }

    }
}
