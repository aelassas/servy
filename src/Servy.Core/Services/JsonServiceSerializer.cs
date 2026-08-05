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
            // Reuse the hardened deserialization settings for consistent property handling,
            // while adding Formatting.Indented for human-readable output files. Note that
            // NullValueHandling.Ignore omits null properties from the generated JSON output.
            return JsonConvert.SerializeObject(dto, Formatting.Indented, JsonSecurity.UntrustedDataSettings);
        }

        /// <inheritdoc />
        protected override string FormatLineInfo(Exception ex)
        {
            int lineNumber = 0;
            int linePosition = 0;

            switch (ex)
            {
                case JsonReaderException readerEx:
                    lineNumber = readerEx.LineNumber;
                    linePosition = readerEx.LinePosition;
                    break;
                case JsonSerializationException serializationEx:
                    lineNumber = serializationEx.LineNumber;
                    linePosition = serializationEx.LinePosition;
                    break;
            }

            if (lineNumber > 0 && linePosition > 0)
            {
                return $" at line {lineNumber}, position {linePosition}";
            }

            return string.Empty;
        }
    }
}
