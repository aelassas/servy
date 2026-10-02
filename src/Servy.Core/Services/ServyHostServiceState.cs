namespace Servy.Core.Services
{
    /// <summary>
    /// What is registered under the Servy host service name (<see cref="Config.AppConfig.ServyHostServiceName"/>).
    /// </summary>
    public enum ServyHostServiceState
    {
        /// <summary>No service has the name.</summary>
        NotInstalled,

        /// <summary>The service runs a Servy host executable (<c>Servy.Host.exe</c> or <c>Servy.Host.Net48.exe</c>).</summary>
        ServyHost,

        /// <summary>The service runs another program, for example one a user installed under the name before it was reserved.</summary>
        Foreign,

        /// <summary>The service exists, but its executable could not be read.</summary>
        Unknown,
    }
}
