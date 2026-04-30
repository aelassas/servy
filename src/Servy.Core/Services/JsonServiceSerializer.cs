using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using System;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class JsonServiceSerializer : IJsonServiceSerializer
    {
        /// <inheritdoc />
        public ServiceDto Deserialize(string json)
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
                // LOGIC: In Newtonsoft.Json, the base JsonException does not contain line info.
                // We cast to IJsonLineInfo to safely access coordinates if the exception provides them.
                var lineInfo = ex as IJsonLineInfo;
                int lineNumber = (lineInfo != null && lineInfo.HasLineInfo()) ? lineInfo.LineNumber : 0;
                int linePosition = (lineInfo != null && lineInfo.HasLineInfo()) ? lineInfo.LinePosition : 0;
                var lineInfoMessage = (lineNumber > 0 && linePosition > 0) ? $" at line {lineNumber}, position {linePosition}" : string.Empty;

                Logger.Error($"JSON Deserialization failed{lineInfoMessage}.", ex);

                // Returning null fulfills the contract: "returns null if deserialization fails"
                return null;
            }
        }

        /// <inheritdoc />
        public string Serialize(ServiceDto dto)
        {
            if (dto == null)
                return null;

            try
            {
                // Use the exact same settings as deserialization to guarantee round-trip symmetry,
                // while adding Formatting.Indented for human-readable output files.
                return JsonConvert.SerializeObject(dto, Formatting.Indented, JsonSecurity.UntrustedDataSettings);
            }
            catch (Exception ex)
            {
                Logger.Error($"JSON Serialization failed for service: {dto.Name}", ex);
                return null;
            }
        }
    }
}