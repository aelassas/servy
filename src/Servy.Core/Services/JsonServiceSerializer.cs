using System;
using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Security;

namespace Servy.Core.Services
{
    /// <inheritdoc cref="IJsonServiceSerializer" />
    public class JsonServiceSerializer : ServiceDtoSerializer, IJsonServiceSerializer
    {
        /// <inheritdoc />
        protected override string FormatName => "JSON";

        /// <inheritdoc />
        protected override ServiceDto? DeserializeCore(string content)
        {
            // Attempt to deserialize using the secure settings
            return JsonConvert.DeserializeObject<ServiceDto>(content, JsonSecurity.UntrustedDataSettings);
        }

        /// <inheritdoc />
        protected override string? SerializeCore(ServiceDto dto)
        {
            // Use the exact same settings as deserialization to guarantee round-trip symmetry,
            // while adding Formatting.Indented for human-readable output files.
            return JsonConvert.SerializeObject(dto, Formatting.Indented, JsonSecurity.UntrustedDataSettings);
        }

        /// <inheritdoc />
        protected override string FormatLineInfo(Exception ex)
        {
            if (ex is JsonException jsonEx)
            {
                // Newtonsoft's base JsonException does not carry line info.
                // We cast to IJsonLineInfo to safely access coordinates if the exception provides them.
                var lineInfo = jsonEx as IJsonLineInfo;
                int lineNumber = (lineInfo != null && lineInfo.HasLineInfo()) ? lineInfo.LineNumber : 0;
                int linePosition = (lineInfo != null && lineInfo.HasLineInfo()) ? lineInfo.LinePosition : 0;

                if (lineNumber > 0 && linePosition > 0)
                {
                    return $" at line {lineNumber}, position {linePosition}";
                }
            }

            return string.Empty;
        }
    }
}