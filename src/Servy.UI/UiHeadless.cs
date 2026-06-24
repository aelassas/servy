namespace Servy.UI
{
    /// <summary>
    /// Provides a centralized, process-global orchestration switch to control headless execution behavior across the UI layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This utility unifies previously decoupled test hooks across individual subsystem presentation layers 
    /// (such as localized help actions and modal message box descriptors) into a single atomic source of truth.
    /// </para>
    /// <para>
    /// Enabling this mode acts as a safety boundary when executing automated UI test harnesses, integration passes, 
    /// or server-side continuous integration (CI) pipelines. When active, it suppresses non-blocking side-effects—such as 
    /// launching external system web browsers—and automatically neutralizes or auto-answers modal dialog prompt containers.
    /// </para>
    /// </remarks>
    public static class UiHeadless
    {
        /// <summary>
        /// Gets or sets a value indicating whether headless automation mode is actively enforced across UI services.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if interactive and hardware-dependent side-effects are intercepted and mocked 
        /// to standard console outputs; otherwise, <see langword="false"/> for normal user-facing desktop application execution loops.
        /// </value>
        /// <remarks>
        /// When toggled to <see langword="true"/>, the following subsystems mutate behavior:
        /// <list type="bullet">
        /// <item>
        /// <description><b>Help &amp; Browser Redirection:</b> Blocks native process shell execution calls to prevent system browsers from opening.</description>
        /// </item>
        /// <item>
        /// <description><b>Message Box Services:</b> Suppresses blocking modal prompt dialog windows, auto-resolving choices to benign affirmative selections while emitting logs directly to standard out streams.</description>
        /// </item>
        /// </list>
        /// </remarks>
        public static bool IsEnabled { get; set; }
    }
}