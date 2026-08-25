using Newtonsoft.Json;
using Servy.Core.Config;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides centralized security configurations and settings for JSON serialization tasks.
    /// </summary>
    public static class JsonSecurity
    {
        /// <summary>
        /// The canonical hardened configuration for processing untrusted JSON: no type-name handling,
        /// a bounded depth, metadata properties ignored, nulls omitted, and unknown members rejected.
        /// </summary>
        /// <remarks>
        /// Returns a NEW instance on every access, deliberately. <see cref="JsonSerializerSettings"/> is
        /// mutable and this member is public, so a shared instance could be weakened process-wide by any
        /// consumer - see the fix for #3235. Do not convert this to a static readonly field.
        /// </remarks>
        public static JsonSerializerSettings UntrustedDataSettings => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = AppConfig.UntrustedJsonMaxDepth,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
        };
    }
}
