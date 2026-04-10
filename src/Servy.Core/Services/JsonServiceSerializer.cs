using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Security;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides JSON serialization and deserialization for <see cref="ServiceDto"/> objects.
    /// </summary>
    public class JsonServiceSerializer : IJsonServiceSerializer
    {
        /// <inheritdoc />
        public ServiceDto? Deserialize(string? json)
        {
            // Branch 1: Covered by Deserialize_NullOrEmpty_ReturnsNull
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var dto = JsonConvert.DeserializeObject<ServiceDto>(json, JsonSecurity.UntrustedDataSettings);

            // Since we made ApplyDefaults null-safe, we call it directly.
            // This line is now 100% covered.
            ServiceDtoHelper.ApplyDefaults(dto);

            return dto;
        }
    }
}