using Newtonsoft.Json;

namespace Servy.Core.Security
{
    public static class JsonSecurity
    {
        /// <summary>
        /// Secure settings for deserializing untrusted user input.
        /// Explicitly disables TypeNameHandling to prevent RCE attacks.
        /// </summary>
        public static readonly JsonSerializerSettings UntrustedDataSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 32, // Added protection against stack overflow from deep nesting
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore
        };
    }
}