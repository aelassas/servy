namespace Servy.UI
{
    /// <summary>
    /// Process-global flag that switches the UI layer into headless mode.
    /// </summary>
    /// <remarks>
    /// Set this in automated UI tests and CI runs, where no interactive desktop session is available.
    /// </remarks>
    public static class UiHeadless
    {
        /// <summary>
        /// Gets or sets a value indicating whether headless mode is enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to write interactive prompts to the console instead of showing them;
        /// otherwise, <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// When <see langword="true"/>:
        /// <list type="bullet">
        /// <item><description><see cref="Services.HelpService"/> logs the target URL instead of launching a browser.</description></item>
        /// <item><description><see cref="Services.MessageBoxService"/> writes the message to the console; confirmations auto-answer <c>Yes</c>.</description></item>
        /// </list>
        /// </remarks>
        public static bool IsEnabled { get; set; }
    }
}
