namespace Servy.Manager.Config
{
    /// <summary>
    /// Provides application-wide configuration.
    /// </summary>
    public static class UiAppConfig
    {
        /// <summary>
        /// Caption used in message boxes.
        /// </summary>
        public const string Caption = "Servy Manager";

        /// <summary>
        /// Local System Account name displayed in UI.
        /// </summary>
        public const string LocalSystem = "Local System";

        /// <summary>
        /// Label for the Local Service built-in account, often used for service log-on configurations.
        /// </summary>
        public const string LocalService = "Local Service";

        /// <summary>
        /// Label for the Network Service built-in account, used for services requiring network-aware identity.
        /// </summary>
        public const string NetworkService = "Network Service";
    }
}
