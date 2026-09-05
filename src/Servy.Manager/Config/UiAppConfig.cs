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
        /// Local System account name displayed in UI.
        /// </summary>
        public static string LocalSystem => Resources.Strings.Account_LocalSystem;

        /// <summary>
        /// Label for the Local Service built-in account.
        /// </summary>
        public static string LocalService => Resources.Strings.Account_LocalService;

        /// <summary>
        /// Label for the Network Service built-in account.
        /// </summary>
        public static string NetworkService => Resources.Strings.Account_NetworkService;
    }
}
