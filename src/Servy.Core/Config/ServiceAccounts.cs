namespace Servy.Core.Config
{
    /// <summary>
    /// Contains the internal account names utilized by the Windows Service Control Manager (SCM). 
    /// These values represent the raw strings returned by WMI and the Win32 API, which are distinct from the localized display names used in the user interface.
    /// </summary>
    public static class ServiceAccounts
    {
        /// <summary>
        /// The internal SCM name for the highly-privileged LocalSystem account.
        /// </summary>
        public const string LocalSystem = "LocalSystem";

        /// <summary>
        /// The internal SCM name for the NT AUTHORITY\LocalService account, which has minimum privileges on the local computer.
        /// </summary>
        public const string LocalService = @"NT AUTHORITY\LocalService";

        /// <summary>
        /// The internal SCM name for the NT AUTHORITY\NetworkService account, which has minimum privileges on the local computer and acts as the computer on the network.
        /// </summary>
        public const string NetworkService = @"NT AUTHORITY\NetworkService";
    }
}