using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class JsonServiceSerializer : IJsonServiceSerializer
    {
        /// <inheritdoc />
        public ServiceDto? Deserialize(string? json)
        {
            // Branch 1: Covered by Deserialize_NullOrEmpty_ReturnsNull
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                // Attempt to deserialize using the secure settings
                var dto = JsonConvert.DeserializeObject<ServiceDto>(json, JsonSecurity.UntrustedDataSettings);

                // If deserialization succeeded, apply defaults (e.g., setting missing timeouts)
                if (dto != null)
                {
                    ServiceDtoHelper.ApplyDefaults(dto);
                }

                return dto;
            }
            catch (JsonException ex)
            {
                string? snippet = json?.Length > 100 ? json.Substring(0, 100) + "..." : json;

                Logger.Error($"JSON Deserialization failed for input starting with: {snippet}", ex);

                // Returning null fulfills the contract: "returns null if deserialization fails"
                return null;
            }
        }

    }
}