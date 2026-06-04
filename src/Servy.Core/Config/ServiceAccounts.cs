using System.Collections.Immutable;

namespace Servy.Core.Config
{
    /// <summary>
    /// Contains the internal account names utilized by the Windows Service Control Manager (SCM). 
    /// These values represent the raw strings returned by the Win32 Service Control Manager API, which are distinct from the localized display names used in the user interface.
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

        /// <summary>
        /// A collection of standard aliases used to identify the Windows 'LocalSystem' account.
        /// </summary>
        public static readonly ImmutableHashSet<string> LocalSystemAliases = ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "LocalSystem", @".\LocalSystem", @"NT AUTHORITY\SYSTEM", "SYSTEM"
        );

        /// <summary>
        /// A collection of well-known alias strings representing the 'LocalService' account,
        /// used for normalizing service configuration security identifiers.
        /// </summary>
        public static readonly ImmutableHashSet<string> LocalServiceAliases = ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "LocalService", @".\LocalService", @"NT AUTHORITY\LocalService");

        /// <summary>
        /// A collection of well-known alias strings representing the 'NetworkService' account,
        /// used for normalizing service configuration security identifiers.
        /// </summary>
        public static readonly ImmutableHashSet<string> NetworkServiceAliases = ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "NetworkService", @".\NetworkService", @"NT AUTHORITY\NetworkService");
    }
}