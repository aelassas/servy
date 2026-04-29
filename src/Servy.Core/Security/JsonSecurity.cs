using Newtonsoft.Json;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides centralized security configurations and settings for JSON serialization tasks.
    /// </summary>
    public static class JsonSecurity
    {
        /// <summary>
        /// A canonical settings object used for processing untrusted data, configured to prevent deep recursion, ignore metadata properties, and omit null values for cleaner output.
        /// </summary>
        public static readonly JsonSerializerSettings UntrustedDataSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 32,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}