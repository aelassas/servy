using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides a base class for serializing and deserializing <see cref="ServiceDto"/> objects.
    /// </summary>
    public abstract class ServiceDtoSerializer
    {
        /// <summary>
        /// Gets the name of the data format (e.g., "JSON", "XML") used in log messages.
        /// </summary>
        protected abstract string FormatName { get; }

        /// <summary>
        /// When overridden in a derived class, executes the format-specific parsing mechanics to reconstruct a DTO from a string representation.
        /// </summary>
        /// <param name="content">The formatted string containing the service definition data.</param>
        /// <returns>A populated <see cref="ServiceDto"/> instance if successful; otherwise, <c>null</c>.</returns>
        protected abstract ServiceDto? DeserializeCore(string content);

        /// <summary>
        /// When overridden in a derived class, executes the format-specific serialization mechanics to transform a DTO into its string expression.
        /// </summary>
        /// <param name="dto">The DTO to serialize.</param>
        /// <returns>A formatted representation string of the service definition.</returns>
        protected abstract string? SerializeCore(ServiceDto dto);

        /// <summary>
        /// Extracts and formats line, index, or position metadata from a format-specific processing exception.
        /// </summary>
        /// <param name="ex">The thrown format runtime exception.</param>
        /// <returns>A formatted string detailing the exact exception location context, or an empty string if unavailable.</returns>
        protected virtual string FormatLineInfo(Exception ex) => string.Empty;

        /// <summary>
        /// Deserializes a format-specific textual stream representation into a structured <see cref="ServiceDto"/>.
        /// </summary>
        /// <param name="input">The raw text block content.</param>
        /// <returns>The DTO, or <c>null</c> if the input is null, empty, or whitespace-only, or parsing fails.</returns>
        public ServiceDto? Deserialize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            try
            {
                var dto = DeserializeCore(input);

                // Hydrate defaults for absent optional fields, then apply the Global Identity Reset on Import
                // policy: RunAsLocalSystem/UserAccount/Password are forced to the password-less LocalSystem
                // baseline. Id is kept out of the payload by [JsonIgnore]/[XmlIgnore] on ServiceDto.Id, not here.
                if (dto != null)
                {
                    ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);
                }

                return dto;
            }
            catch (Exception ex)
            {
                var lineInfoMessage = FormatLineInfo(ex);

                if (!string.IsNullOrEmpty(lineInfoMessage))
                {
                    Logger.Error($"{FormatName} Deserialization failed{lineInfoMessage}.", ex);
                }
                else
                {
                    Logger.Error($"{FormatName} Deserialization encountered a failure.", ex);
                }

                return null;
            }
        }

        /// <summary>
        /// Serializes a structured <see cref="ServiceDto"/> instance into its corresponding format-specific textual expression.
        /// </summary>
        /// <param name="dto">The DTO to serialize.</param>
        /// <returns>The serialized string, or <c>null</c> if the DTO is null or serialization fails.</returns>
        public string? Serialize(ServiceDto? dto)
        {
            if (dto == null)
                return null;

            try
            {
                return SerializeCore(dto);
            }
            catch (Exception ex)
            {
                Logger.Error($"{FormatName} Serialization failed for service: {dto.Name}", ex);
                return null;
            }
        }
    }
}
